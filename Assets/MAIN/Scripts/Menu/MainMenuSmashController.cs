using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuSmashController : MonoBehaviour
{
    enum MenuState { Aiming, Diving, Impact, TransitionOut }

    [Header("References")]
    [SerializeField] Transform player;
    [SerializeField] Rigidbody playerRb;
    [SerializeField] Camera menuCamera;
    [SerializeField] LayerMask menuTargetMask;

    [Header("Camera Orbit (Aiming)")]
    [SerializeField] float mouseSensitivity = 2f;
    [SerializeField] float verticalSensitivity = 2f;
    [SerializeField] float minPitch = -80f;
    [SerializeField] float maxPitch = 80f;
    [SerializeField] Vector3 shoulderOffset = new Vector3(1f, 2f, -3f);

    [Header("Dive Settings")]
    [SerializeField] float diveSpeed = 40f;
    [SerializeField] float diveAcceleration = 70f;
    [SerializeField] float maxDiveTime = 2.5f;
    [SerializeField] float transitionDelay = 0.3f;

    [Header("Scene")]
    [SerializeField] string gameSceneName = "Challenge 4";

    [Header("Visuals (Optional)")]
    [SerializeField] LineRenderer aimLine;
    [SerializeField] GameObject crosshairIndicator;
    [SerializeField] RectTransform screenCrosshair; // UI crosshair anchored to screen center

    MenuState state = MenuState.Aiming;
    MenuSmashTarget hoveredTarget;
    MenuSmashTarget selectedTarget;
    Vector3 diveTargetPoint; // exact hit point to dive toward
    bool diveCollided;
    float currentDiveSpeed;

    // camera orbit state
    float yaw;
    float pitch;

    void Start()
    {
        // keep the ball in place until player clicks
        if (playerRb != null)
        {
            playerRb.useGravity = false;
            playerRb.linearVelocity = Vector3.zero;
        }

        // lock cursor for mouse-look (like smash aiming)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (crosshairIndicator != null)
            crosshairIndicator.SetActive(false);

        // screen crosshair visible from the start
        if (screenCrosshair != null)
            screenCrosshair.gameObject.SetActive(true);

        if (aimLine != null)
            aimLine.enabled = false;

        // initialize orbit angles from current camera position
        if (menuCamera != null && player != null)
        {
            Vector3 dir = menuCamera.transform.position - player.position;
            yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(dir.normalized.y) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // auto-attach collision forwarder to the player ball
        if (player != null)
        {
            var ballCol = player.GetComponent<MenuBallCollision>();
            if (ballCol == null)
                ballCol = player.gameObject.AddComponent<MenuBallCollision>();
            ballCol.controller = this;
        }
    }

    void Update()
    {
        if (state == MenuState.Aiming)
            UpdateAiming();
    }

    void FixedUpdate()
    {
        if (state == MenuState.Diving)
            UpdateDive();
    }

    void LateUpdate()
    {
        if (menuCamera == null || player == null) return;

        // camera only moves during aiming, stays frozen once you click
        if (state == MenuState.Aiming)
            UpdateCameraOrbit();
    }

    // -- camera --

    void UpdateCameraOrbit()
    {
        // mouse rotates camera around the player (like RotateCameraX)
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        yaw += mouseX * mouseSensitivity;
        pitch -= mouseY * verticalSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // build rotation from yaw/pitch
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // position = player + rotation * shoulderOffset (shoulder-style close cam)
        menuCamera.transform.position = player.position + rotation * shoulderOffset;
        menuCamera.transform.rotation = Quaternion.LookRotation(player.position - menuCamera.transform.position);

        // slightly look past the player (aim direction), not directly at the ball
        menuCamera.transform.rotation = rotation;
    }

    // -- aiming --

    void UpdateAiming()
    {
        // raycast from camera center (crosshair) like smash aiming
        Ray ray = new Ray(menuCamera.transform.position, menuCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, menuTargetMask))
        {
            var target = hit.collider.GetComponent<MenuSmashTarget>();
            if (target == null)
                target = hit.collider.GetComponentInParent<MenuSmashTarget>();

            if (target != null)
            {
                // switched to a new target
                if (target != hoveredTarget)
                {
                    if (hoveredTarget != null) hoveredTarget.Unhighlight();
                    hoveredTarget = target;
                    hoveredTarget.Highlight();
                }

                UpdateAimVisuals(hit.point);

                // click to select and dive toward the exact aim point
                if (Input.GetMouseButtonDown(0))
                {
                    selectedTarget = target;
                    diveTargetPoint = hit.point;
                    StartDive();
                }

                return;
            }
        }

        // not aiming at a target
        ClearHover();
    }

    void ClearHover()
    {
        if (hoveredTarget != null)
        {
            hoveredTarget.Unhighlight();
            hoveredTarget = null;
        }

        if (aimLine != null) aimLine.enabled = false;
        if (crosshairIndicator != null) crosshairIndicator.SetActive(false);
    }

    void UpdateAimVisuals(Vector3 hitPoint)
    {
        if (aimLine != null)
        {
            aimLine.enabled = true;
            aimLine.SetPosition(0, player.position);
            aimLine.SetPosition(1, hitPoint);
        }

        if (crosshairIndicator != null)
        {
            crosshairIndicator.SetActive(true);
            crosshairIndicator.transform.position = hitPoint;
        }
    }

    // -- diving --

    void StartDive()
    {
        state = MenuState.Diving;
        diveCollided = false;
        currentDiveSpeed = diveSpeed;

        // clear visuals + disable highlight so the button doesn't stay lit during dive
        if (hoveredTarget != null) hoveredTarget.Unhighlight();
        hoveredTarget = null;
        if (aimLine != null) aimLine.enabled = false;
        if (crosshairIndicator != null) crosshairIndicator.SetActive(false);
        if (screenCrosshair != null) screenCrosshair.gameObject.SetActive(false);

        playerRb.useGravity = false;
        playerRb.linearVelocity = Vector3.zero;

        StartCoroutine(DiveSafetyTimeout());
    }

    void UpdateDive()
    {
        if (selectedTarget == null) return;

        // accelerate toward the exact point the player was aiming at
        Vector3 dir = (diveTargetPoint - player.position).normalized;
        currentDiveSpeed += diveAcceleration * Time.fixedDeltaTime;
        playerRb.linearVelocity = dir * currentDiveSpeed;

        if (diveCollided)
        {
            // keep momentum and let gravity take over
            playerRb.useGravity = true;
            state = MenuState.Impact;
            StartCoroutine(HandleImpact());
        }
    }

    IEnumerator DiveSafetyTimeout()
    {
        yield return new WaitForSeconds(maxDiveTime);

        // if still diving after timeout, force impact
        if (state == MenuState.Diving)
        {
            playerRb.useGravity = true;
            state = MenuState.Impact;
            StartCoroutine(HandleImpact());
        }
    }

    // called by MenuBallCollision on the player ball (handles both trigger and collision)
    public void NotifyCollision(GameObject hitObject, Vector3 ballPos)
    {
        if (state == MenuState.Diving)
            diveCollided = true;

        // shatter anything the ball hits that has MenuShatter, not just the selected target
        if (hitObject != null)
        {
            var shatter = hitObject.GetComponent<MenuShatter>();
            if (shatter == null) shatter = hitObject.GetComponentInParent<MenuShatter>();
            if (shatter != null)
                shatter.Shatter(ballPos);
        }
    }

    // -- impact + transition --

    IEnumerator HandleImpact()
    {
        state = MenuState.TransitionOut;

        // shattering is handled by NotifyCollision on any hit, no need to duplicate here

        yield return new WaitForSeconds(transitionDelay);
        ExecuteOption(selectedTarget.OptionType);
    }

    void ExecuteOption(MenuOption option)
    {
        switch (option)
        {
            case MenuOption.Start:
                SceneManager.LoadScene(gameSceneName);
                break;

            case MenuOption.Quit:
#if UNITY_EDITOR
                EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }
}
