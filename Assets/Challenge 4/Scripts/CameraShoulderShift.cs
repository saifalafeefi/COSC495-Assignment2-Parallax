using UnityEngine;
using Unity.Cinemachine;

public class CameraShoulderShift : MonoBehaviour
{
    public Rigidbody playerRb;
    public Transform focalPoint;
    public float shiftSpeed = 2f;         // how fast it transitions between sides
    public float maxSpeed = 8f;           // velocity at which shoulder fully commits to one side
    public float deadZone = 1.5f;         // ignore sideways speed below this to prevent flipping

    private CinemachineThirdPersonFollow thirdPersonFollow;
    private float currentTarget = 0.5f;   // start centered

    void Start()
    {
        thirdPersonFollow = GetComponent<CinemachineThirdPersonFollow>();
    }

    void FixedUpdate()
    {
        if (thirdPersonFollow == null || playerRb == null) return;

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
            currentTarget = Mathf.MoveTowards(currentTarget, 0.5f, 0.5f * Time.fixedDeltaTime);
        }

        // smooth transition
        thirdPersonFollow.CameraSide = Mathf.Lerp(thirdPersonFollow.CameraSide, currentTarget, shiftSpeed * Time.fixedDeltaTime);
    }
}
