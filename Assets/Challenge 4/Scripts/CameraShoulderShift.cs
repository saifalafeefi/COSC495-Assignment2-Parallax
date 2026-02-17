using UnityEngine;
using Unity.Cinemachine;

public class CameraShoulderShift : MonoBehaviour
{
    public Rigidbody playerRb;
    public Transform focalPoint;
    public float shiftSpeed = 2f;         // how fast it transitions between sides
    public float maxSpeed = 8f;           // velocity at which shoulder fully commits to one side
    public float deadZone = 1.5f;         // ignore sideways speed below this to prevent flipping
    public float tweenDuration = 0.4f;    // how long the override tween takes
    public EasingType tweenEasing = EasingType.EaseInOut;

    private CinemachineThirdPersonFollow thirdPersonFollow;
    private float currentTarget = 0.5f;   // start centered
    private bool sideOverridden;
    private float overrideSideValue;
    private float tweenFrom;
    private float tweenProgress;

    // offset override
    private bool offsetOverridden;
    private Vector3 originalOffset;
    private Vector3 targetOffset;
    private float offsetTweenProgress = 1f; // start done so it doesn't tween on startup

    public void ForceCameraSide(float side)
    {
        sideOverridden = true;
        overrideSideValue = Mathf.Clamp01(side);
        tweenFrom = thirdPersonFollow != null ? thirdPersonFollow.CameraSide : 0.5f;
        tweenProgress = 0f;
    }

    public void ReleaseCameraSide()
    {
        sideOverridden = false;
    }

    public void ForceShoulderOffset(Vector3 offset)
    {
        offsetOverridden = true;
        if (thirdPersonFollow != null)
            originalOffset = thirdPersonFollow.ShoulderOffset;
        targetOffset = offset;
        offsetTweenProgress = 0f;
    }

    public void ReleaseShoulderOffset()
    {
        if (offsetOverridden)
        {
            offsetOverridden = false;
            targetOffset = originalOffset;
            offsetTweenProgress = 0f;
        }
    }

    void Start()
    {
        thirdPersonFollow = GetComponent<CinemachineThirdPersonFollow>();

        // save the original offset so the startup tween doesn't zero it out
        if (thirdPersonFollow != null)
        {
            originalOffset = thirdPersonFollow.ShoulderOffset;
            targetOffset = originalOffset;
        }
    }

    void LateUpdate()
    {
        if (thirdPersonFollow == null || playerRb == null || focalPoint == null) return;

        // don't shift while paused or game over
        if (GameManagerX.Instance != null && (GameManagerX.Instance.isPaused || GameManagerX.Instance.isGameOver))
            return;

        // tween shoulder offset (runs during both override and normal)
        if (offsetOverridden || offsetTweenProgress < 1f)
        {
            offsetTweenProgress = Mathf.MoveTowards(offsetTweenProgress, 1f, Time.unscaledDeltaTime / tweenDuration);
            float eased = Easing.Evaluate(tweenEasing, offsetTweenProgress);
            thirdPersonFollow.ShoulderOffset = Vector3.Lerp(thirdPersonFollow.ShoulderOffset, targetOffset, eased);
        }

        if (sideOverridden)
        {
            // ease-in-out tween, unscaled so slow-mo doesn't affect it
            tweenProgress = Mathf.MoveTowards(tweenProgress, 1f, Time.unscaledDeltaTime / tweenDuration);
            float eased = Easing.Evaluate(tweenEasing, tweenProgress);
            thirdPersonFollow.CameraSide = Mathf.Lerp(tweenFrom, overrideSideValue, eased);
            return;
        }

        // how fast the player is moving sideways relative to camera
        float sidewaysSpeed = Vector3.Dot(playerRb.linearVelocity, focalPoint.right);

        // only shift if moving sideways hard enough, prevents flipping on 180 turns
        if (Mathf.Abs(sidewaysSpeed) > deadZone)
        {
            float influence = Mathf.Clamp01((Mathf.Abs(sidewaysSpeed) - deadZone) / (maxSpeed - deadZone));
            currentTarget = sidewaysSpeed > 0
                ? Mathf.Lerp(0.5f, 1f, influence)
                : Mathf.Lerp(0.5f, 0f, influence);
        }
        else
        {
            // ease back to center when not moving sideways
            currentTarget = Mathf.MoveTowards(currentTarget, 0.5f, 0.5f * Time.deltaTime);
        }

        // smooth transition
        thirdPersonFollow.CameraSide = Mathf.Lerp(thirdPersonFollow.CameraSide, currentTarget, shiftSpeed * Time.deltaTime);
    }
}
