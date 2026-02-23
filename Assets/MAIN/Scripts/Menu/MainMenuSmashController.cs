using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuSmashController : MonoBehaviour
{
    enum MenuState { Aiming, Diving, Impact, TransitionOut, Settings }

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

    [Header("Handheld Sway")]
    [SerializeField] float swayAmount = 0.03f;
    [SerializeField] float swaySpeed = 1f;
    [SerializeField] float swayRotationAmount = 0.3f;

    [Header("Dive Settings")]
    [SerializeField] float diveSpeed = 40f;
    [SerializeField] float diveAcceleration = 70f;
    [SerializeField] float maxDiveTime = 2.5f;
    [SerializeField] float transitionDelay = 0.3f;

    [Header("Scene")]
    [SerializeField] string gameSceneName = "Challenge 4";

    [Header("Settings View")]
    [Tooltip("Camera position when viewing settings (place above the menu area)")]
    [SerializeField] Transform settingsCameraPoint;
    [Tooltip("Canvas/panel that holds settings UI — enabled when entering settings, disabled when leaving")]
    [SerializeField] GameObject settingsPanel;
    [Tooltip("Delay after smashing the settings button before camera tweens to settings view")]
    [SerializeField] float settingsDelay = 0.8f;

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

    // settings state
    Vector3 playerStartPos;

    void Start()
    {
        if (playerRb != null)
        {
            playerRb.useGravity = false;
            playerRb.linearVelocity = Vector3.zero;
        }

        if (player != null)
            playerStartPos = player.position;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (crosshairIndicator != null)
            crosshairIndicator.SetActive(false);

        if (screenCrosshair != null)
            screenCrosshair.gameObject.SetActive(true);

        if (aimLine != null)
            aimLine.enabled = false;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

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
        else if (state == MenuState.Settings)
            UpdateSettings();
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

        // handheld sway — layered sine waves at irrational frequencies so it never repeats
        {
            float time = Time.unscaledTime * swaySpeed;
            Vector3 posOffset = new Vector3(
                Mathf.Sin(time * 1.0f) * 0.5f + Mathf.Sin(time * 2.37f) * 0.3f + Mathf.Sin(time * 4.13f) * 0.2f,
                Mathf.Sin(time * 0.7f) * 0.5f + Mathf.Sin(time * 1.93f) * 0.3f + Mathf.Sin(time * 3.71f) * 0.2f,
                Mathf.Sin(time * 0.5f) * 0.5f + Mathf.Sin(time * 1.57f) * 0.3f + Mathf.Sin(time * 2.89f) * 0.2f
            ) * swayAmount;

            Vector3 rotOffset = new Vector3(
                Mathf.Sin(time * 0.9f) * 0.5f + Mathf.Sin(time * 2.11f) * 0.3f + Mathf.Sin(time * 3.47f) * 0.2f,
                Mathf.Sin(time * 1.1f) * 0.5f + Mathf.Sin(time * 2.53f) * 0.3f + Mathf.Sin(time * 4.31f) * 0.2f,
                Mathf.Sin(time * 0.6f) * 0.5f + Mathf.Sin(time * 1.79f) * 0.3f + Mathf.Sin(time * 3.17f) * 0.2f
            ) * swayRotationAmount;

            menuCamera.transform.position += posOffset;
            menuCamera.transform.rotation *= Quaternion.Euler(rotOffset);
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

    // -- settings --

    // called after the ball smashes the settings button + delay
    IEnumerator TransitionToSettings()
    {
        // let the shatter play out before moving the camera
        yield return new WaitForSeconds(settingsDelay);

        state = MenuState.Settings;

        // freeze the ball so it doesn't roll away
        if (playerRb != null)
        {
            playerRb.useGravity = false;
            playerRb.linearVelocity = Vector3.zero;
        }

        // tween camera to settings view point
        if (settingsCameraPoint != null && menuCamera != null)
        {
            tweenFromPos = menuCamera.transform.position;
            tweenFromRot = menuCamera.transform.rotation;
            tweenToPos = settingsCameraPoint.position;
            tweenToRot = settingsCameraPoint.rotation;
            tweenProgress = 0f;
        }

        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        // show cursor so player can interact with sliders
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void UpdateSettings()
    {
        // Escape or Backspace to go back to menu
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            ExitSettings();
    }

    void ExitSettings()
    {
        state = MenuState.Aiming;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // reset ball to original position first so camera computes correctly
        if (player != null && playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
            player.position = playerStartPos;
        }

        // recompute camera for the current target from the reset player position
        if (menuCamera != null && targets.Count > 0)
        {
            ComputeCameraForTarget(targets[currentIndex], out Vector3 returnPos, out Quaternion returnRot);
            tweenFromPos = menuCamera.transform.position;
            tweenFromRot = menuCamera.transform.rotation;
            tweenToPos = returnPos;
            tweenToRot = returnRot;
            tweenProgress = 0f;
        }

        HighlightTarget(currentIndex);
        if (screenCrosshair != null) screenCrosshair.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // called by settings UI back button
    public void OnSettingsBack()
    {
        if (state == MenuState.Settings)
            ExitSettings();
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

        // settings has its own independent delay, skip transitionDelay for it
        if (selectedTarget.OptionType == MenuOption.Settings)
        {
            StartCoroutine(TransitionToSettings());
            yield break;
        }

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

            case MenuOption.Settings:
                StartCoroutine(TransitionToSettings());
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
