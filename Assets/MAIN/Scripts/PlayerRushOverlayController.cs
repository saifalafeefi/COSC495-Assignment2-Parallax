using UnityEngine;

// runtime overlay material for rush mode with smooth in/out tween and rainbow hue motion
public class PlayerRushOverlayController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerControllerX player;
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private bool autoFindRenderers = true;
    [SerializeField] private bool includeChildren = false;

    [Header("Overlay Material")]
    [SerializeField] private Shader overlayShader;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float enterDuration = 0.25f;
    [SerializeField, Min(0f)] private float exitDuration = 0.35f;
    [SerializeField] private AnimationCurve enterCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve exitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Rainbow")]
    [SerializeField] private float baseHueCycleSpeed = 0.22f;
    [SerializeField] private float chaosFrequency = 2.6f;
    [SerializeField] private float chaosAmplitude = 0.2f;
    [SerializeField, Range(0f, 1f)] private float saturation = 0.95f;
    [SerializeField, Range(0f, 1f)] private float value = 1f;
    [SerializeField] private float tintAlpha = 0.75f;

    [Header("Overlay Noise")]
    [SerializeField] private float overlayNoiseScale = 2f;
    [SerializeField] private float overlayFlowSpeed = 0.8f;

    [Header("Rush Weight")]
    [SerializeField, Range(0f, 1f)] private float rushWeightMax = 1f;

    private static readonly int Color6Id = Shader.PropertyToID("_Color6");
    private static readonly int Weight6Id = Shader.PropertyToID("_Weight6");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");

    private struct OverlayTarget
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material overlayMaterial;
    }

    private OverlayTarget[] overlays;
    private float rushBlend;
    private bool idleStateApplied;

    void Start()
    {
        if (player == null)
            player = GetComponent<PlayerControllerX>();

        EnsureTargets();
        BuildOverlayMaterials();
    }

    void Update()
    {
        if (overlays == null || overlays.Length == 0 || player == null) return;

        bool active = player.IsRushActive;
        if (!active && rushBlend <= 0.0001f)
        {
            if (!idleStateApplied)
            {
                ApplyIdleState();
                idleStateApplied = true;
            }
            return;
        }

        idleStateApplied = false;
        float duration = active ? enterDuration : exitDuration;
        float delta = duration > 0.0001f ? Time.deltaTime / duration : 1f;
        rushBlend = Mathf.Clamp01(rushBlend + (active ? delta : -delta));
        AnimationCurve curve = active ? enterCurve : exitCurve;
        float curveBlend = curve != null ? Mathf.Clamp01(curve.Evaluate(rushBlend)) : rushBlend;
        float weight = curveBlend * rushWeightMax;

        // scaled time so hue motion pauses with gameplay pause
        float t = Time.time;
        float baseHue = Mathf.Repeat(t * baseHueCycleSpeed, 1f);
        float chaosA = (Mathf.PerlinNoise(t * chaosFrequency, 2.37f) - 0.5f) * 2f;
        float chaosB = Mathf.Sin(t * chaosFrequency * 1.41f);
        float hue = Mathf.Repeat(baseHue + (chaosA * 0.7f + chaosB * 0.3f) * chaosAmplitude, 1f);
        Color rushColor = Color.HSVToRGB(hue, saturation, value);
        rushColor.a = tintAlpha;

        for (int i = 0; i < overlays.Length; i++)
        {
            Material m = overlays[i].overlayMaterial;
            if (m == null) continue;
            m.SetColor(Color6Id, rushColor);
            m.SetFloat(Weight6Id, weight);
            m.SetFloat(NoiseScaleId, overlayNoiseScale);
            m.SetFloat(FlowSpeedId, overlayFlowSpeed);
        }
    }

    void ApplyIdleState()
    {
        for (int i = 0; i < overlays.Length; i++)
        {
            Material m = overlays[i].overlayMaterial;
            if (m == null) continue;
            m.SetFloat(Weight6Id, 0f);
        }
    }

    void EnsureTargets()
    {
        if ((targetRenderers == null || targetRenderers.Length == 0) && autoFindRenderers)
        {
            targetRenderers = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();
        }
    }

    void BuildOverlayMaterials()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            overlays = System.Array.Empty<OverlayTarget>();
            return;
        }

        if (overlayShader == null)
            overlayShader = Shader.Find("Custom/PowerupOverlay");

        if (overlayShader == null)
        {
            Debug.LogError("PlayerRushOverlayController: Custom/PowerupOverlay shader not found.");
            overlays = System.Array.Empty<OverlayTarget>();
            return;
        }

        overlays = new OverlayTarget[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer r = targetRenderers[i];
            if (r == null) continue;

            Material[] mats = r.materials;
            if (mats == null || mats.Length == 0) continue;

            Material overlay = new Material(overlayShader)
            {
                name = "RushOverlayRuntime"
            };

            Material[] updated = new Material[mats.Length + 1];
            for (int m = 0; m < mats.Length; m++)
                updated[m] = mats[m];
            updated[updated.Length - 1] = overlay;
            r.materials = updated;

            overlays[i] = new OverlayTarget
            {
                renderer = r,
                originalMaterials = mats,
                overlayMaterial = overlay
            };
        }
    }

    void OnDestroy()
    {
        if (overlays == null) return;

        for (int i = 0; i < overlays.Length; i++)
        {
            OverlayTarget entry = overlays[i];
            if (entry.renderer != null && entry.originalMaterials != null && entry.originalMaterials.Length > 0)
                entry.renderer.materials = entry.originalMaterials;

            if (entry.overlayMaterial != null)
                Destroy(entry.overlayMaterial);
        }
    }
}
