using UnityEngine;

// put this on the ground object to override player drag for this biome
public class BiomeDrag : MonoBehaviour
{
    [SerializeField] private float playerDrag = 0.5f;
    [SerializeField] private float playerAngularDrag = 0.05f;

    void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.linearDamping = playerDrag;
        rb.angularDamping = playerAngularDrag;
    }
}
