using UnityEngine;

// adds a transparent multiply overlay material to the player ball
// tints the surface with active powerup colors without touching the original material
public class PlayerPowerupColor : MonoBehaviour
{
    [Header("Overlay Material")]
    [Tooltip("Material using Custom/PowerupOverlay shader. If empty, auto-creates one at runtime.")]
    public Material overlayMaterial;

    [Header("Powerup Colors")]
    public Color knockbackColor = new Color(1f, 0.89f, 0f);
    public Color smashColor = new Color(1f, 0f, 0f);
    public Color shieldColor = new Color(0f, 0.77f, 1f);
    public Color giantColor = new Color(0.11f, 0.53f, 0.22f);
    public Color hauntColor = new Color(0.86f, 0f, 1f);

    [Header("Noise")]
    [Tooltip("Scale of the 3D noise pattern — higher = more detailed/smaller blobs")]
    public float noiseScale = 2f;
    [Tooltip("How fast the colors flow across the surface")]
    public float flowSpeed = 0.8f;

    [Header("Fade")]
    public float fadeSpeed = 3f;

    private PlayerControllerX playerController;
    private Renderer[] targetRenderers;
    private Material overlayInstance; // runtime instance so we don't modify the asset

    // current interpolated weights
    private float w1, w2, w3, w4, w5;

    // cached shader property IDs
    private static readonly int NoiseScaleID = Shader.PropertyToID("_NoiseScale");
    private static readonly int FlowSpeedID = Shader.PropertyToID("_FlowSpeed");
    private static readonly int Color1ID = Shader.PropertyToID("_Color1");
    private static readonly int Color2ID = Shader.PropertyToID("_Color2");
    private static readonly int Color3ID = Shader.PropertyToID("_Color3");
    private static readonly int Color4ID = Shader.PropertyToID("_Color4");
    private static readonly int Color5ID = Shader.PropertyToID("_Color5");
    private static readonly int Weight1ID = Shader.PropertyToID("_Weight1");
    private static readonly int Weight2ID = Shader.PropertyToID("_Weight2");
    private static readonly int Weight3ID = Shader.PropertyToID("_Weight3");
    private static readonly int Weight4ID = Shader.PropertyToID("_Weight4");
    private static readonly int Weight5ID = Shader.PropertyToID("_Weight5");

    void Start()
    {
        playerController = GetComponent<PlayerControllerX>();
        // support visual-only child model swaps (prefabs with meshes under children)
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        var valid = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in allRenderers)
        {
            if (r is ParticleSystemRenderer) continue;
            valid.Add(r);
        }
        targetRenderers = valid.ToArray();
        if (targetRenderers.Length == 0)
        {
            enabled = false;
            return;
        }

        // create overlay material instance if none assigned
        if (overlayMaterial == null)
        {
            Shader overlayShader = Shader.Find("Custom/PowerupOverlay");
            if (overlayShader == null)
            {
                enabled = false;
                return;
            }
            overlayInstance = new Material(overlayShader);
        }
        else
        {
            overlayInstance = new Material(overlayMaterial); // instance so asset isn't modified
        }

        // add overlay as second material slot (slot 0 = original, slot 1 = overlay)
        for (int r = 0; r < targetRenderers.Length; r++)
        {
            Material[] origMats = targetRenderers[r].sharedMaterials;
            Material[] newMats = new Material[origMats.Length + 1];
            for (int i = 0; i < origMats.Length; i++)
                newMats[i] = origMats[i];
            newMats[origMats.Length] = overlayInstance;
            targetRenderers[r].materials = newMats;
        }
    }

    void Update()
    {
        if (playerController == null || overlayInstance == null) return;

        // target weights: 1 if active, 0 if not
        float t1 = playerController.IsKnockbackActive ? 1f : 0f;
        float t2 = playerController.IsSmashActive ? 1f : 0f;
        float t3 = playerController.IsShieldActive ? 1f : 0f;
        float t4 = playerController.IsGiantActive ? 1f : 0f;
        float t5 = playerController.IsHauntActive ? 1f : 0f;

        // smooth fade toward target (unscaled so it works during slow-mo)
        float dt = Time.unscaledDeltaTime * fadeSpeed;
        w1 = Mathf.MoveTowards(w1, t1, dt);
        w2 = Mathf.MoveTowards(w2, t2, dt);
        w3 = Mathf.MoveTowards(w3, t3, dt);
        w4 = Mathf.MoveTowards(w4, t4, dt);
        w5 = Mathf.MoveTowards(w5, t5, dt);

        // set properties directly on the overlay instance
        overlayInstance.SetFloat(NoiseScaleID, noiseScale);
        overlayInstance.SetFloat(FlowSpeedID, flowSpeed);
        overlayInstance.SetColor(Color1ID, knockbackColor);
        overlayInstance.SetColor(Color2ID, smashColor);
        overlayInstance.SetColor(Color3ID, shieldColor);
        overlayInstance.SetColor(Color4ID, giantColor);
        overlayInstance.SetColor(Color5ID, hauntColor);

        overlayInstance.SetFloat(Weight1ID, w1);
        overlayInstance.SetFloat(Weight2ID, w2);
        overlayInstance.SetFloat(Weight3ID, w3);
        overlayInstance.SetFloat(Weight4ID, w4);
        overlayInstance.SetFloat(Weight5ID, w5);
    }

    void OnDestroy()
    {
        // clean up the runtime material instance
        if (overlayInstance != null)
            Destroy(overlayInstance);
    }
}
