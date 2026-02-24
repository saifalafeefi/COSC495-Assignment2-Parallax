using UnityEngine;

// spinning + bobbing float for powerup pickups
public class PowerupFloat : MonoBehaviour
{
    [Header("Spin")]
    [SerializeField] float spinSpeed = 90f;
    [SerializeField] Vector3 spinAxis = Vector3.up;

    [Header("Bob")]
    [SerializeField] float bobSpeed = 2f;
    [SerializeField] float bobHeight = 0.3f;

    Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // spin
        transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.World);

        // bob up and down
        float offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPos + Vector3.up * offset;
    }
}
