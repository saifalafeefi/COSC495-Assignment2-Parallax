using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Follow target (optional)")]
    public Transform followTarget; // if null, camera will not track X/Z automatically
    public Vector3 followOffset = new Vector3(0f, 5f, -10f);

    [Header("Vertical move on border hit")]
    public float moveUpAmount = 8f;
    public float moveDuration = 0.8f; // seconds
    public AnimationCurve moveEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Clamping")]
    public float minY = -10f;
    public float maxY = 100f;

    // internal
    bool isMoving = false;
    Quaternion fixedRotation;

    void Start()
    {
        // Remember camera rotation and lock it
        fixedRotation = transform.rotation;

        // If camera has Rigidbody, freeze rotation so physics doesn't tilt it
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.constraints = rb.constraints | RigidbodyConstraints.FreezeRotation;
    }

    void LateUpdate()
    {
        // Keep rotation constant (avoid tilt)
        transform.rotation = fixedRotation;

        // Optionally follow on X/Z while keeping Y controlled by this script
        if (followTarget != null && !isMoving)
        {
            Vector3 desired = followTarget.position + followOffset;
            Vector3 pos = transform.position;
            transform.position = new Vector3(desired.x, pos.y, desired.z);
        }

        // Clamp Y always
        Vector3 p = transform.position;
        p.y = Mathf.Clamp(p.y, minY, maxY);
        transform.position = p;
    }

    // Public method to call from trigger
    public void MoveCameraUp(float additionalAmount, bool additive = true)
    {
        if (isMoving) return;
        float targetY = transform.position.y + (additive ? additionalAmount : 0f + additionalAmount);
        targetY = Mathf.Clamp(targetY, minY, maxY);
        StartCoroutine(MoveYRoutine(transform.position.y, targetY, moveDuration));
    }

    IEnumerator MoveYRoutine(float startY, float endY, float duration)
    {
        isMoving = true;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            float eased = moveEase.Evaluate(t);
            float newY = Mathf.Lerp(startY, endY, eased);
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, newY, p.z);
            yield return null;
        }
        // ensure final value, and unlock movement
        Vector3 final = transform.position;
        final.y = endY;
        transform.position = final;
        isMoving = false;
    }
}