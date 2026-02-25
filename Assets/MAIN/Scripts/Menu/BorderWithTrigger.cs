using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BorderWithTrigger : MonoBehaviour
{
    [Header("Player/Camera setup")]
    public string playerTag = "Player";
    public float triggerOffset = 0.2f; // how far in front of blocking collider the trigger is placed (local units)
    public float triggerThickness = 0.1f; // thin trigger thickness so it doesn't block

    [Header("Camera raise")]
    public float raiseAmount = 8f;
    public bool oneShot = true;
    public bool additive = true;

    // Optional: name for the automatically created child trigger
    const string kTriggerChildName = "BorderTrigger";

    void Reset()
    {
        // Ensure main collider is not a trigger (blocking)
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = false;
    }

    void Awake()
    {
        // verify there's a non-trigger collider on the border
        var blocking = GetComponent<Collider>();
        if (blocking == null)
        {
            Debug.LogError($"{name}: No collider found. Add a BoxCollider (Is Trigger = false) to act as blocking border.");
            enabled = false;
            return;
        }

        if (blocking.isTrigger)
        {
            Debug.LogWarning($"{name}: Main collider is set to IsTrigger = true. Changing it to IsTrigger = false to block the player.");
            blocking.isTrigger = false;
        }

        // Ensure there is a trigger child to detect the player
        Transform t = transform.Find(kTriggerChildName);
        if (t == null)
            CreateTriggerChild(blocking as BoxCollider);
        else
            EnsureTriggerComponent(t.gameObject, blocking as BoxCollider);
    }

    void CreateTriggerChild(BoxCollider blockingBox)
    {
        GameObject go = new GameObject(kTriggerChildName);
        go.transform.SetParent(transform, false);

        // If blocking collider is a BoxCollider, match size/rotation and offset it
        if (blockingBox != null)
        {
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;

            // Copy size and center
            trigger.size = blockingBox.size;
            trigger.center = blockingBox.center;

            // Move the trigger slightly in front in local Z (forward) direction based on border facing.
            // If the border normal is along its local forward, offset along local forward. We'll offset along local normal from transform.forward
            // Use triggerOffset to push out so camera detection occurs before the physical collision.
            go.transform.localPosition = blockingBox.center + transform.InverseTransformDirection(transform.forward) * triggerOffset;

            // Make the trigger thin so it does not block physics if something goes weird
            trigger.size = new Vector3(trigger.size.x, trigger.size.y, triggerThickness);
        }
        else
        {
            // fallback - add sphere trigger
            var sph = go.AddComponent<SphereCollider>();
            sph.isTrigger = true;
            sph.radius = 0.5f;
        }

        // Add the trigger handler
        go.AddComponent<BorderTriggerHandler>().Init(this);
    }

    void EnsureTriggerComponent(GameObject triggerGo, BoxCollider blockingBox)
    {
        var col = triggerGo.GetComponent<Collider>();
        if (col == null)
        {
            // create a simple thin collider
            var t = triggerGo.AddComponent<BoxCollider>();
            t.isTrigger = true;
            t.size = blockingBox != null ? new Vector3(blockingBox.size.x, blockingBox.size.y, triggerThickness) : Vector3.one * triggerThickness;
            t.center = blockingBox != null ? blockingBox.center : Vector3.zero;
        }
        else
        {
            col.isTrigger = true;
        }

        var handler = triggerGo.GetComponent<BorderTriggerHandler>();
        if (handler == null)
            triggerGo.AddComponent<BorderTriggerHandler>().Init(this);
        else
            handler.parent = this;
    }

    // Called by BorderTriggerHandler when player enters trigger
    internal void OnPlayerTriggered()
    {
        // Raise the camera
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("BorderWithTrigger: No Camera.main found.");
            return;
        }

        var controller = cam.GetComponent<CameraController>();
        if (controller == null)
        {
            Debug.LogWarning("BorderWithTrigger: CameraController not found on main camera. Add CameraController script to the camera.");
            return;
        }

        controller.MoveCameraUp(raiseAmount, additive);

        if (oneShot)
        {
            // disable the trigger child to avoid retriggering
            var t = transform.Find(kTriggerChildName);
            if (t != null)
                t.gameObject.SetActive(false);
        }
    }

    // Debug/usage notes displayed in Editor (optional)
    void OnDrawGizmosSelected()
    {
        var blocking = GetComponent<Collider>();
        if (blocking != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.TransformPoint(Vector3.zero), Vector3.one);
        }
    }
}


/// <summary>
/// Simple handler attached to the child trigger collider that notifies the parent BorderWithTrigger.
/// Kept simple so parent border logic is clean.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BorderTriggerHandler : MonoBehaviour
{
    [HideInInspector] public BorderWithTrigger parent;

    public void Init(BorderWithTrigger p)
    {
        parent = p;
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (parent == null) return;
        if (!other.CompareTag(parent.playerTag)) return;

        // defensive: if the player's rigidbody is very fast set continuous detection (prevents tunneling)
        var rb = other.attachedRigidbody;
        if (rb != null && rb.collisionDetectionMode == CollisionDetectionMode.Discrete)
        {
            // recommended: in the editor set to ContinuousDynamic for fast-moving objects
            // but don't change it silently for all players here; just a debug hint
            // rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // optional
        }

        parent.OnPlayerTriggered();
    }
}