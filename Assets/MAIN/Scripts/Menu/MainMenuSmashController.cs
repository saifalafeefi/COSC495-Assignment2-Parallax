using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
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

    [Header("Targets (ordered left to right)")]
    [Tooltip("Drag menu targets here in left-to-right order. A/Left = previous, D/Right = next.")]
    [SerializeField] List<MenuSmashTarget> targets = new List<MenuSmashTarget>();

    [Header("Camera")]
    [SerializeField] Vector3 shoulderOffset = new Vector3(1f, 2f, -3f);
    [SerializeField] float tweenDuration = 0.4f;
    [SerializeField] EasingType tweenEasing = EasingType.EaseOut;

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
    [SerializeField] RectTransform screenCrosshair;

    MenuState state = MenuState.Aiming;
    MenuSmashTarget selectedTarget;
    Vector3 diveTargetPoint;
    bool diveCollided;
    float currentDiveSpeed;

    // camera tween state
    int currentIndex;
    float tweenProgress = 1f; // start done so no tween on first frame
    Quaternion tweenFromRot;
    Vector3 tweenFromPos;
    Quaternion tweenToRot;
    Vector3 tweenToPos;

    void Start()
    {
        if (playerRb != null)
        {
            playerRb.useGravity = false;
            playerRb.linearVelocity = Vector3.zero;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (crosshairIndicator != null)
            crosshairIndicator.SetActive(false);

        if (screenCrosshair != null)
            screenCrosshair.gameObject.SetActive(true);

        if (aimLine != null)
            aimLine.enabled = false;

        // auto-attach collision forwarder to the player ball
        if (player != null)
        {
            var ballCol = player.GetComponent<MenuBallCollision>();
            if (ballCol == null)
                ballCol = player.gameObject.AddComponent<MenuBallCollision>();
            ballCol.controller = this;
        }

        // start on first target (index 0)
        currentIndex = 0;
        if (targets.Count > 0)
        {
            // snap camera to first target immediately
            ComputeCameraForTarget(targets[currentIndex], out tweenToPos, out tweenToRot);
            if (menuCamera != null)
            {
                menuCamera.transform.position = tweenToPos;
                menuCamera.transform.rotation = tweenToRot;
            }
            HighlightTarget(currentIndex);
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

        // tween camera toward current target
        if (tweenProgress < 1f)
        {
            tweenProgress = Mathf.MoveTowards(tweenProgress, 1f, Time.unscaledDeltaTime / tweenDuration);
            float t = Easing.Evaluate(tweenEasing, tweenProgress);
            menuCamera.transform.position = Vector3.Lerp(tweenFromPos, tweenToPos, t);
            menuCamera.transform.rotation = Quaternion.Slerp(tweenFromRot, tweenToRot, t);
        }
    }

    // -- camera --

    void ComputeCameraForTarget(MenuSmashTarget target, out Vector3 pos, out Quaternion rot)
    {
        // camera aims at target position + per-target offset (visual only, ball still dives at center)
        Vector3 aimPoint = target.transform.position + target.CameraLookOffset;
        Vector3 lookDir = (aimPoint - player.position).normalized;
        float yaw = Mathf.Atan2(lookDir.x, lookDir.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Asin(lookDir.y) * Mathf.Rad2Deg;

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        pos = player.position + rotation * shoulderOffset;
        rot = rotation;
    }

    void TweenToTarget(int index)
    {
        if (menuCamera == null || targets.Count == 0) return;

        // save current as the tween start
        tweenFromPos = menuCamera.transform.position;
        tweenFromRot = menuCamera.transform.rotation;

        ComputeCameraForTarget(targets[index], out tweenToPos, out tweenToRot);
        tweenProgress = 0f;
    }

    // -- aiming --

    void UpdateAiming()
    {
        if (targets.Count == 0) return;

        // A / Left Arrow = previous target
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            NavigateTo(currentIndex - 1);

        // D / Right Arrow = next target
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            NavigateTo(currentIndex + 1);

        // confirm with Space, Enter, or left click
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
        {
            selectedTarget = targets[currentIndex];
            diveTargetPoint = selectedTarget.transform.position;
            StartDive();
        }

        UpdateAimVisuals();
    }

    void NavigateTo(int newIndex)
    {
        if (targets.Count == 0) return;

        // wrap around — going past the end loops back to the start and vice versa
        if (newIndex < 0) newIndex = targets.Count - 1;
        else if (newIndex >= targets.Count) newIndex = 0;
        if (newIndex == currentIndex) return;

        // unhighlight old, highlight new
        UnhighlightTarget(currentIndex);
        currentIndex = newIndex;
        HighlightTarget(currentIndex);

        TweenToTarget(currentIndex);
    }

    void HighlightTarget(int index)
    {
        if (index >= 0 && index < targets.Count && targets[index] != null)
            targets[index].Highlight();
    }

    void UnhighlightTarget(int index)
    {
        if (index >= 0 && index < targets.Count && targets[index] != null)
            targets[index].Unhighlight();
    }

    void UpdateAimVisuals()
    {
        if (targets.Count == 0) return;

        Vector3 targetPos = targets[currentIndex].transform.position;

        if (aimLine != null)
        {
            aimLine.enabled = true;
            aimLine.SetPosition(0, player.position);
            aimLine.SetPosition(1, targetPos);
        }

        if (crosshairIndicator != null)
        {
            crosshairIndicator.SetActive(true);
            crosshairIndicator.transform.position = targetPos;
        }
    }

    // -- diving --

    void StartDive()
    {
        state = MenuState.Diving;
        diveCollided = false;
        currentDiveSpeed = diveSpeed;

        // clear visuals + disable highlight
        UnhighlightTarget(currentIndex);
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

        Vector3 dir = (diveTargetPoint - player.position).normalized;
        currentDiveSpeed += diveAcceleration * Time.fixedDeltaTime;
        playerRb.linearVelocity = dir * currentDiveSpeed;

        if (diveCollided)
        {
            playerRb.useGravity = true;
            state = MenuState.Impact;
            StartCoroutine(HandleImpact());
        }
    }

    IEnumerator DiveSafetyTimeout()
    {
        yield return new WaitForSeconds(maxDiveTime);

        if (state == MenuState.Diving)
        {
            playerRb.useGravity = true;
            state = MenuState.Impact;
            StartCoroutine(HandleImpact());
        }
    }

    // called by MenuBallCollision on the player ball
    public void NotifyCollision(GameObject hitObject, Vector3 ballPos)
    {
        if (state == MenuState.Diving)
            diveCollided = true;

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
