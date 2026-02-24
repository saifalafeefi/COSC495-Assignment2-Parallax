using UnityEngine;

// attach to the glow child mesh — uses existing material, just sets properties
public class PowerupGlow : MonoBehaviour
{
    [Header("Glow")]
    [SerializeField] Color glowColor = new Color(1f, 1f, 0f, 0.3f);
    [SerializeField] float glowScale = 1.2f;
    [SerializeField] float glowIntensity = 3f;

    [Header("Pulse")]
    [SerializeField] float pulseSpeed = 2f;

    [Header("Alpha")]
    [SerializeField] [Range(0f, 1f)] float alphaMin = 0.05f;
    [SerializeField] [Range(0f, 1f)] float alphaMax = 0.4f;

    [Header("Fresnel")]
    [SerializeField] [Range(0f, 1f)] float centerRadius = 0f;
    [SerializeField] float edgeSharpness = 2f;

    Material mat;

    void Start()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer == null) return;

        mat = renderer.material;
        ApplyProperties();

        transform.localScale = Vector3.one * glowScale;
    }

    void ApplyProperties()
    {
        if (mat == null) return;

        mat.SetColor("_Color", glowColor);
        mat.SetFloat("_Intensity", glowIntensity);
        mat.SetFloat("_PulseSpeed", pulseSpeed);
        mat.SetFloat("_AlphaMin", alphaMin);
        mat.SetFloat("_AlphaMax", alphaMax);
        mat.SetFloat("_FresnelMin", centerRadius);
        mat.SetFloat("_FresnelPower", edgeSharpness);
    }

    public void SetColor(Color color)
    {
        glowColor = color;
        if (mat != null) mat.SetColor("_Color", color);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
