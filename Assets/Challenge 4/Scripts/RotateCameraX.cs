using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateCameraX : MonoBehaviour
{
    public float mouseSensitivity = 3f;
    public float verticalSensitivity = 2f;
    public float minVerticalAngle = -30f; // how far down you can look
    public float maxVerticalAngle = 60f;  // how far up you can look
    public GameObject player;

    private float verticalAngle = 0f;
    private bool clampOverridden;
    private float savedMinAngle;
    private float savedMaxAngle;

    // freeze state — camera stops orbiting, stays in place
    private bool isFrozen;

    public void Freeze() { isFrozen = true; }
    public void Unfreeze() { isFrozen = false; }

    // tween state
    private bool isTweening;
    private float tweenStartYaw;
    private float tweenStartPitch;
    private float tweenTargetYaw;
    private float tweenTargetPitch;
    private float tweenDuration;
    private float tweenElapsed;
    private EasingType tweenEasing;

    public void RemovePitchClamp()
    {
        clampOverridden = true;
        savedMinAngle = minVerticalAngle;
        savedMaxAngle = maxVerticalAngle;
        minVerticalAngle = -90f;
        maxVerticalAngle = 90f;
    }

    public void RestorePitchClamp()
    {
        if (clampOverridden)
        {
            minVerticalAngle = savedMinAngle;
            maxVerticalAngle = savedMaxAngle;
            clampOverridden = false;
        }
    }

    // LateUpdate so it runs after physics, prevents jitter
    void LateUpdate()
    {
        // smooth tween overrides normal mouse input while active
        if (isTweening)
        {
            tweenElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(tweenElapsed / tweenDuration);
            float eased = Easing.Evaluate(tweenEasing, t);

            verticalAngle = Mathf.Lerp(tweenStartPitch, tweenTargetPitch, eased);
            float yaw = Mathf.LerpAngle(tweenStartYaw, tweenTargetYaw, eased);

            transform.eulerAngles = new Vector3(verticalAngle, yaw, 0f);
            transform.position = player.transform.position;

            if (t >= 1f) isTweening = false;
            return;
        }

        // when frozen, just follow position — no rotation from mouse
        if (isFrozen)
        {
            transform.position = player.transform.position;
            return;
        }

        // rotate horizontally with mouse X
        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up, mouseX * mouseSensitivity, Space.World);

        // tilt vertically with mouse Y, clamped so you can't flip
        float mouseY = Input.GetAxis("Mouse Y");
        verticalAngle -= mouseY * verticalSensitivity;
        verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);

        Vector3 currentEuler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(verticalAngle, currentEuler.y, 0f);

        // follow the player
        transform.position = player.transform.position;
    }

    /// <summary>
    /// Snaps the camera to look at a world position, keeping verticalAngle in sync.
    /// </summary>
    public void LookAtPosition(Vector3 worldPosition)
    {
        Vector3 dir = (worldPosition - transform.position).normalized;

        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Asin(dir.y) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);

        verticalAngle = pitch;
        transform.eulerAngles = new Vector3(pitch, yaw, 0f);
    }

    /// <summary>
    /// Smoothly tweens the camera to look at a world position over duration seconds.
    /// Uses smoothstep ease-in-out, unscaled time so slow-mo doesn't affect it.
    /// Mouse input is paused during the tween.
    /// </summary>
    public void TweenToPosition(Vector3 worldPosition, float duration, EasingType easing = EasingType.EaseInOut)
    {
        Vector3 dir = (worldPosition - transform.position).normalized;

        tweenTargetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        tweenTargetPitch = -Mathf.Asin(dir.y) * Mathf.Rad2Deg;
        tweenTargetPitch = Mathf.Clamp(tweenTargetPitch, minVerticalAngle, maxVerticalAngle);

        tweenStartYaw = transform.eulerAngles.y;
        tweenStartPitch = verticalAngle;
        tweenDuration = duration;
        tweenElapsed = 0f;
        tweenEasing = easing;
        isTweening = true;
    }
}
