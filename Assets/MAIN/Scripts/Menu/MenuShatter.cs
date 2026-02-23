using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class MenuShatter : MonoBehaviour
{
    [SerializeField] int gridSize = 3;
    [SerializeField] float explosionForce = 15f;
    [SerializeField] float fragmentLifetime = 3f;
    [SerializeField] float fragmentScaleJitter = 0.3f;

    // cache the primitive cube mesh (shared across all fragments)
    static Mesh cubeMesh;
    bool isShattered;
    Renderer[] cachedRenderers;
    Collider[] cachedColliders;
    readonly List<GameObject> spawnedPieces = new List<GameObject>();

    void Awake()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    public void Shatter(Vector3 impactPoint)
    {
        if (isShattered) return;
        float lifetime = Mathf.Max(0f, fragmentLifetime);

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;
        isShattered = true;

        // grab bounds + material before hiding
        Bounds bounds = rend.bounds;
        Material mat = rend.material;

        // hide original object visuals + collision
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = false;
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = false;
        }

        // spawn TMP clones so original hierarchy stays intact for reset
        foreach (var tmp in GetComponentsInChildren<TextMeshPro>())
        {
            GameObject textClone = Instantiate(tmp.gameObject, tmp.transform.position, tmp.transform.rotation);
            textClone.transform.localScale = tmp.transform.lossyScale;

            // remove this component on clones to avoid recursive shatter behavior
            MenuShatter cloneShatter = textClone.GetComponent<MenuShatter>();
            if (cloneShatter != null) Destroy(cloneShatter);

            // add a collider roughly matching the text bounds
            BoxCollider textCol = textClone.AddComponent<BoxCollider>();
            textCol.size = tmp.bounds.size;
            Rigidbody textRb = textClone.AddComponent<Rigidbody>();
            Vector3 dir = (textClone.transform.position - impactPoint).normalized;
            textRb.AddForce(dir * explosionForce, ForceMode.Impulse);
            textRb.AddTorque(Random.insideUnitSphere * explosionForce * 0.5f, ForceMode.Impulse);

            spawnedPieces.Add(textClone);
            Destroy(textClone, lifetime);
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

            spawnedPieces.Add(frag);
            Destroy(frag, lifetime);
        }
    }

    public void ResetShatter()
    {
        for (int i = 0; i < spawnedPieces.Count; i++)
        {
            if (spawnedPieces[i] != null)
                Destroy(spawnedPieces[i]);
        }
        spawnedPieces.Clear();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = true;
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = true;
        }

        isShattered = false;
    }
}
