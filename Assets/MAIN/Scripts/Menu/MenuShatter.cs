using UnityEngine;
using TMPro;

public class MenuShatter : MonoBehaviour
{
    [SerializeField] int gridSize = 3;
    [SerializeField] float explosionForce = 15f;
    [SerializeField] float fragmentLifetime = 3f;
    [SerializeField] float fragmentScaleJitter = 0.3f;

    // cache the primitive cube mesh (shared across all fragments)
    static Mesh cubeMesh;

    public void Shatter(Vector3 impactPoint)
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        Collider col = GetComponentInChildren<Collider>();
        if (rend == null) return;

        // grab bounds + material before hiding
        Bounds bounds = rend.bounds;
        Material mat = rend.material;

        // hide the original target
        rend.enabled = false;
        if (col != null) col.enabled = false;

        // detach any TMP text children so they fly off with the explosion
        foreach (var tmp in GetComponentsInChildren<TextMeshPro>())
        {
            tmp.transform.SetParent(null);
            // add a collider roughly matching the text bounds
            BoxCollider textCol = tmp.gameObject.AddComponent<BoxCollider>();
            textCol.size = tmp.bounds.size;
            Rigidbody textRb = tmp.gameObject.AddComponent<Rigidbody>();
            Vector3 dir = (tmp.transform.position - impactPoint).normalized;
            textRb.AddForce(dir * explosionForce, ForceMode.Impulse);
            textRb.AddTorque(Random.insideUnitSphere * explosionForce * 0.5f, ForceMode.Impulse);
            Destroy(tmp.gameObject, fragmentLifetime);
        }

        // lazy-load primitive cube mesh
        if (cubeMesh == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
        }

        Vector3 cellSize = bounds.size / gridSize;

        for (int x = 0; x < gridSize; x++)
        for (int y = 0; y < gridSize; y++)
        for (int z = 0; z < gridSize; z++)
        {
            // position each fragment in its grid cell
            Vector3 pos = bounds.min + new Vector3(
                (x + 0.5f) * cellSize.x,
                (y + 0.5f) * cellSize.y,
                (z + 0.5f) * cellSize.z
            );

            // randomize size a bit so it doesn't look like a perfect grid
            float jitter = 1f + Random.Range(-fragmentScaleJitter, fragmentScaleJitter);
            Vector3 scale = cellSize * jitter;

            GameObject frag = new GameObject("Fragment");
            frag.transform.position = pos;
            frag.transform.localScale = scale;
            frag.transform.rotation = Random.rotation;

            frag.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
            frag.AddComponent<MeshRenderer>().material = mat;
            frag.AddComponent<BoxCollider>();

            Rigidbody rb = frag.AddComponent<Rigidbody>();
            // explode outward from impact point
            Vector3 dir = (pos - impactPoint).normalized;
            rb.AddForce(dir * explosionForce, ForceMode.Impulse);
            // add a bit of random torque for tumble
            rb.AddTorque(Random.insideUnitSphere * explosionForce * 0.5f, ForceMode.Impulse);

            Destroy(frag, fragmentLifetime);
        }
    }
}
