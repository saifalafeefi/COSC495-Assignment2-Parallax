using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuSmashController : MonoBehaviour
{
    enum MenuState { Aiming, Diving, Impact, TransitionOut, Settings, Skins, BiomeSelection }

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

    [Header("Skins View")]
    [Tooltip("Optional player transform while previewing skins. If empty, current position is used.")]
    [SerializeField] Transform skinsPlayerPoint;
    [Tooltip("Optional camera point for skins preview view. If empty, camera keeps its current path.")]
    [SerializeField] Transform skinsCameraPoint;
    [Tooltip("Canvas/panel for skin controls (left/right/back).")]
    [SerializeField] GameObject skinsPanel;
    [Tooltip("Delay after smashing the skins button before entering skins view.")]
    [SerializeField] float skinsDelay = 0.8f;
    [Tooltip("How fast the player rotates while previewing skins.")]
    [SerializeField] float skinsSpinSpeed = 25f;
    [Tooltip("Keeps the player locked to SkinsPlayerPoint during skins preview.")]
    [SerializeField] bool lockPlayerToSkinsPoint = true;
    [SerializeField] PlayerSkinApplier skinPreview;
    [SerializeField] TextMeshProUGUI skinNameLabel;

    [Header("Biome Selection")]
    [SerializeField] Transform biomeCameraPoint;
    [SerializeField] GameObject biomePanel;
    [SerializeField] float biomeDelay = 0.8f;
    [SerializeField] List<BiomeEntry> biomes = new List<BiomeEntry>();

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
    Quaternion playerStartRot;
    readonly List<Renderer> forcedHiddenRenderers = new List<Renderer>();
    readonly List<Collider> forcedHiddenColliders = new List<Collider>();

    void Start()
    {
        if (playerRb != null)
        {
            playerRb.useGravity = false;
            playerRb.linearVelocity = Vector3.zero;
        }

        if (player != null)
        {
            playerStartPos = player.position;
            playerStartRot = player.rotation;
        }

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
        if (skinsPanel != null)
            skinsPanel.SetActive(false);
        if (biomePanel != null)
            biomePanel.SetActive(false);

        if (skinPreview == null)
            skinPreview = FindAnyObjectByType<PlayerSkinApplier>();

        ResetMenuShatters();

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
        else if (state == MenuState.Skins)
            UpdateSkins();
        else if (state == MenuState.BiomeSelection)
            UpdateBiomeSelection();
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
        ResetMenuShatters();

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

    IEnumerator TransitionToSkins()
    {
        // let the shatter play out before moving to skins view
        yield return new WaitForSeconds(skinsDelay);
        EnsureSelectedTargetShattered();
        // while in skins view: reset everything except the smashed skins button
        ResetMenuShatters(GetSelectedTargetShatter());
        ForceHideSelectedTargetWhileInSkins();

        state = MenuState.Skins;

        // freeze and position the ball at the preview point
        if (playerRb != null)
        {
            playerRb.useGravity = false;
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        if (player != null && skinsPlayerPoint != null)
        {
            player.position = skinsPlayerPoint.position;
            player.rotation = skinsPlayerPoint.rotation;
        }

        // tween camera to skins preview point
        if (skinsCameraPoint != null && menuCamera != null)
        {
            tweenFromPos = menuCamera.transform.position;
            tweenFromRot = menuCamera.transform.rotation;
            tweenToPos = skinsCameraPoint.position;
            tweenToRot = skinsCameraPoint.rotation;
            tweenProgress = 0f;
        }

        if (skinsPanel != null)
            skinsPanel.SetActive(true);

        if (skinPreview != null)
        {
            skinPreview.ApplySelectedSkin();
            UpdateSkinLabel();
        }

        // show cursor for buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void UpdateSettings()
    {
        // Escape or Backspace to go back to menu
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            ExitSettings();
    }

    void UpdateSkins()
    {
        if (player != null)
        {
            // keep preview stable even if physics/contact tries to push the ball
            if (lockPlayerToSkinsPoint && skinsPlayerPoint != null)
            {
                if (playerRb != null)
                    playerRb.position = skinsPlayerPoint.position;
                else
                    player.position = skinsPlayerPoint.position;
            }

            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
            }

            player.Rotate(0f, skinsSpinSpeed * Time.unscaledDeltaTime, 0f, Space.World);
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            OnSkinLeft();

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            OnSkinRight();

        // Escape or Backspace to go back to menu
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            ExitSkins();
    }

    void ExitSettings()
    {
        state = MenuState.Aiming;
        ResetMenuShatters(GetSelectedTargetShatter());

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // reset ball to original position first so camera computes correctly
        if (player != null && playerRb != null)
            TeleportPlayer(playerStartPos, playerStartRot);

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

    void ExitSkins()
    {
        Vector3 beforePos = player != null ? player.position : Vector3.zero;
        Quaternion beforeRot = player != null ? player.rotation : Quaternion.identity;
        Vector3 beforeVel = playerRb != null ? playerRb.linearVelocity : Vector3.zero;
        Vector3 beforeAngVel = playerRb != null ? playerRb.angularVelocity : Vector3.zero;
        Debug.Log(
            $"[Menu][ExitSkins] BEGIN frame={Time.frameCount} state={state} " +
            $"beforePos={beforePos} beforeRotY={beforeRot.eulerAngles.y:F2} " +
            $"beforeVel={beforeVel} beforeAngVel={beforeAngVel}");

        state = MenuState.Aiming;
        // when leaving skins: fully reset, including the skins button
        ResetMenuShatters();
        RestoreForcedHiddenSelectedTarget();

        if (skinsPanel != null)
            skinsPanel.SetActive(false);

        // reset ball to original position/rotation
        if (player != null && playerRb != null)
            TeleportPlayer(playerStartPos, playerStartRot);

        Debug.Log(
            $"[Menu][ExitSkins] AFTER_RESET frame={Time.frameCount} " +
            $"targetPos={playerStartPos} targetRotY={playerStartRot.eulerAngles.y:F2} " +
            $"actualPos={(player != null ? player.position : Vector3.zero)} " +
            $"actualRotY={(player != null ? player.rotation.eulerAngles.y : 0f):F2} " +
            $"rbUseGravity={(playerRb != null ? playerRb.useGravity : false)} " +
            $"rbVel={(playerRb != null ? playerRb.linearVelocity : Vector3.zero)}");

        StartCoroutine(LogExitSkinsFollowup());

        // recompute camera for current target from the reset player position
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

    IEnumerator LogExitSkinsFollowup()
    {
        yield return null; // next Update
        Debug.Log(
            $"[Menu][ExitSkins] NEXT_UPDATE frame={Time.frameCount} " +
            $"pos={(player != null ? player.position : Vector3.zero)} " +
            $"rotY={(player != null ? player.rotation.eulerAngles.y : 0f):F2} " +
            $"vel={(playerRb != null ? playerRb.linearVelocity : Vector3.zero)}");

        yield return new WaitForFixedUpdate();
        Debug.Log(
            $"[Menu][ExitSkins] AFTER_FIXED frame={Time.frameCount} " +
            $"pos={(player != null ? player.position : Vector3.zero)} " +
            $"rotY={(player != null ? player.rotation.eulerAngles.y : 0f):F2} " +
            $"vel={(playerRb != null ? playerRb.linearVelocity : Vector3.zero)} " +
            $"angVel={(playerRb != null ? playerRb.angularVelocity : Vector3.zero)}");
    }

    void TeleportPlayer(Vector3 targetPos, Quaternion targetRot)
    {
        if (playerRb != null)
        {
            playerRb.useGravity = false;
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
            playerRb.position = targetPos;
            playerRb.rotation = targetRot;
            playerRb.Sleep();
            Physics.SyncTransforms();
        }

        if (player != null)
        {
            player.position = targetPos;
            player.rotation = targetRot;
        }
    }

    // called by settings UI back button
    public void OnSettingsBack()
    {
        if (state == MenuState.Settings)
            ExitSettings();
    }

    // called by skins UI back button
    public void OnSkinsBack()
    {
        if (state == MenuState.Skins)
            ExitSkins();
    }

    // called by skins UI left button (or keyboard in skins state)
    public void OnSkinLeft()
    {
        if (state != MenuState.Skins) return;
        if (skinPreview == null) skinPreview = FindAnyObjectByType<PlayerSkinApplier>();
        if (skinPreview == null) return;
        skinPreview.PreviousSkin();
        UpdateSkinLabel();
    }

    // called by skins UI right button (or keyboard in skins state)
    public void OnSkinRight()
    {
        if (state != MenuState.Skins) return;
        if (skinPreview == null) skinPreview = FindAnyObjectByType<PlayerSkinApplier>();
        if (skinPreview == null) return;
        skinPreview.NextSkin();
        UpdateSkinLabel();
    }

    void UpdateSkinLabel()
    {
        if (skinNameLabel == null || skinPreview == null) return;
        skinNameLabel.text = $"Skin: {skinPreview.GetSkinName(skinPreview.SelectedSkinIndex)}";
    }

    MenuShatter GetSelectedTargetShatter()
    {
        if (selectedTarget == null) return null;
        MenuShatter shatter = selectedTarget.GetComponent<MenuShatter>();
        if (shatter == null)
            shatter = selectedTarget.GetComponentInChildren<MenuShatter>();
        if (shatter == null)
            shatter = selectedTarget.GetComponentInParent<MenuShatter>();
        return shatter;
    }

    void EnsureSelectedTargetShattered()
    {
        MenuShatter shatter = GetSelectedTargetShatter();
        if (shatter != null)
        {
            Vector3 impactPoint = player != null ? player.position : Vector3.zero;
            shatter.Shatter(impactPoint);
            return;
        }

        // fallback for targets without MenuShatter component
        if (selectedTarget != null)
        {
            Renderer[] rends = selectedTarget.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
                rends[i].enabled = false;

            Collider[] cols = selectedTarget.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                cols[i].enabled = false;
        }
    }

    void ForceHideSelectedTargetWhileInSkins()
    {
        forcedHiddenRenderers.Clear();
        forcedHiddenColliders.Clear();
        if (selectedTarget == null) return;

        Renderer[] rends = selectedTarget.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;
            forcedHiddenRenderers.Add(rends[i]);
            rends[i].enabled = false;
        }

        Collider[] cols = selectedTarget.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            forcedHiddenColliders.Add(cols[i]);
            cols[i].enabled = false;
        }
    }

    void RestoreForcedHiddenSelectedTarget()
    {
        for (int i = 0; i < forcedHiddenRenderers.Count; i++)
        {
            if (forcedHiddenRenderers[i] != null)
                forcedHiddenRenderers[i].enabled = true;
        }
        forcedHiddenRenderers.Clear();

        for (int i = 0; i < forcedHiddenColliders.Count; i++)
        {
            if (forcedHiddenColliders[i] != null)
                forcedHiddenColliders[i].enabled = true;
        }
        forcedHiddenColliders.Clear();
    }

    void ResetMenuShatters(MenuShatter skip = null)
    {
        MenuShatter[] shatters = FindObjectsByType<MenuShatter>(FindObjectsSortMode.None);
        for (int i = 0; i < shatters.Length; i++)
        {
            if (shatters[i] == skip) continue;
            shatters[i].ResetShatter();
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

        // settings has its own independent delay, skip transitionDelay for it
        if (selectedTarget.OptionType == MenuOption.Settings)
        {
            StartCoroutine(TransitionToSettings());
            yield break;
        }

        // skins has its own independent delay, skip transitionDelay for it
        if (selectedTarget.OptionType == MenuOption.Skins)
        {
            StartCoroutine(TransitionToSkins());
            yield break;
        }

        // start goes to biome selection if biomes are configured
        if (selectedTarget.OptionType == MenuOption.Start && biomes.Count > 0 && biomePanel != null)
        {
            StartCoroutine(TransitionToBiomeSelection());
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

            case MenuOption.Skins:
                StartCoroutine(TransitionToSkins());
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

    // -- biome selection --

    IEnumerator TransitionToBiomeSelection()
    {
        yield return new WaitForSeconds(biomeDelay);
        ResetMenuShatters();

        state = MenuState.BiomeSelection;

        // freeze the ball
        if (playerRb != null)
        {
            playerRb.useGravity = false;
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        // tween camera to biome view
        if (biomeCameraPoint != null && menuCamera != null)
        {
            tweenFromPos = menuCamera.transform.position;
            tweenFromRot = menuCamera.transform.rotation;
            tweenToPos = biomeCameraPoint.position;
            tweenToRot = biomeCameraPoint.rotation;
            tweenProgress = 0f;
        }

        if (biomePanel != null)
            biomePanel.SetActive(true);

        // wire event triggers on each biome thumbnail at runtime
        for (int i = 0; i < biomes.Count; i++)
        {
            if (biomes[i].thumbnail == null) continue;

            var trigger = biomes[i].thumbnail.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = biomes[i].thumbnail.gameObject.AddComponent<EventTrigger>();

            trigger.triggers.Clear();

            int index = i; // capture for closures

            // pointer enter — highlight
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener(_ => OnBiomeHover(index));
            trigger.triggers.Add(enterEntry);

            // pointer exit — unhighlight
            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener(_ => OnBiomeUnhover(index));
            trigger.triggers.Add(exitEntry);

            // click — load scene
            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener(_ => OnBiomeClick(index));
            trigger.triggers.Add(clickEntry);

            // disable outline by default
            if (biomes[i].outline != null)
                biomes[i].outline.enabled = false;
        }

        // unlock cursor for interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void UpdateBiomeSelection()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            ExitBiomeSelection();
    }

    void OnBiomeHover(int index)
    {
        // disable all outlines, enable hovered one
        for (int i = 0; i < biomes.Count; i++)
        {
            if (biomes[i].outline != null)
                biomes[i].outline.enabled = (i == index);
        }
    }

    void OnBiomeUnhover(int index)
    {
        if (index >= 0 && index < biomes.Count && biomes[index].outline != null)
            biomes[index].outline.enabled = false;
    }

    void OnBiomeClick(int index)
    {
        if (index < 0 || index >= biomes.Count) return;
        SceneManager.LoadScene(biomes[index].sceneName);
    }

    void ExitBiomeSelection()
    {
        state = MenuState.Aiming;
        ResetMenuShatters();

        if (biomePanel != null)
            biomePanel.SetActive(false);

        // reset ball to original position
        if (player != null && playerRb != null)
            TeleportPlayer(playerStartPos, playerStartRot);

        // recompute camera for the current target
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

    // called by biome panel back button
    public void OnBiomeSelectionBack()
    {
        if (state == MenuState.BiomeSelection)
            ExitBiomeSelection();
    }
}
