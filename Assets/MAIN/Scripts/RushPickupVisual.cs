using UnityEngine;

// animated chaotic rainbow for rush pickup mesh/glow
public class RushPickupVisual : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private bool autoFindRenderers = true;
    [SerializeField] private bool includeChildren = true;

    [Header("Hue Motion")]
    [SerializeField] private float baseHueCycleSpeed = 0.35f;
    [SerializeField] private float chaosFrequency = 3.8f;
    [SerializeField] private float chaosAmplitude = 0.25f;
    [SerializeField] private float phaseOffsetPerRenderer = 0.09f;

    [Header("Color")]
    [SerializeField, Range(0f, 1f)] private float saturation = 0.95f;
    [SerializeField, Range(0f, 1f)] private float value = 1f;
    [SerializeField] private float emissionIntensity = 1.75f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Material[] mats;

    void Start()
    {
        CacheRenderers();
    }

    void Update()
    {
        if (mats == null || mats.Length == 0) return;

        // use scaled time so hue freezes when the game is paused (timeScale = 0)
        float t = Time.time;
        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null) continue;

            float phase = i * phaseOffsetPerRenderer;
            float baseHue = Mathf.Repeat(t * baseHueCycleSpeed + phase, 1f);
            float chaosA = (Mathf.PerlinNoise(t * chaosFrequency + phase, phase * 3.1f) - 0.5f) * 2f;
            float chaosB = Mathf.Sin((t + phase * 2f) * chaosFrequency * 1.37f);
            float chaos = (chaosA * 0.7f + chaosB * 0.3f) * chaosAmplitude;
            float hue = Mathf.Repeat(baseHue + chaos, 1f);
            Color c = Color.HSVToRGB(hue, saturation, value);

            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, c * emissionIntensity);
        }
    }

    void CacheRenderers()
    {
        if ((targetRenderers == null || targetRenderers.Length == 0) && autoFindRenderers)
        {
            targetRenderers = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();
        }

        if (targetRenderers == null)
        {
            mats = System.Array.Empty<Material>();
            return;
        }

        var matList = new System.Collections.Generic.List<Material>(targetRenderers.Length * 2);
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] == null) continue;
            Material[] rendererMats = targetRenderers[i].materials;
            if (rendererMats == null) continue;

            // include every material slot so both base gem + glow can rainbow-cycle
            for (int m = 0; m < rendererMats.Length; m++)
            {
                if (rendererMats[m] != null)
                    matList.Add(rendererMats[m]);
            }
        }

        mats = matList.ToArray();
    }

    void OnDestroy()
    {
        if (mats == null) return;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] != null) Destroy(mats[i]);
        }
    }
}
