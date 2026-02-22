using UnityEngine;

public class MenuSmashTarget : MonoBehaviour
{
    [SerializeField] MenuOption optionType;
    [SerializeField] Renderer targetRenderer;
    [SerializeField] Material highlightMaterial;
    [SerializeField, Range(0f, 1f)] float highlightOpacity = 0.3f;

    public MenuOption OptionType => optionType;

    Material originalMaterial;

    void Start()
    {
        // auto-capture whatever material is on the renderer as the default
        if (targetRenderer != null)
            originalMaterial = targetRenderer.material;
    }

    public void Highlight()
    {
        if (targetRenderer == null || highlightMaterial == null) return;

        // blend highlight color with original using opacity slider
        Material blended = new Material(originalMaterial);
        Color highlightColor = highlightMaterial.color;
        Color original = originalMaterial.color;
        blended.color = Color.Lerp(original, highlightColor, highlightOpacity);
        targetRenderer.material = blended;
    }

    public void Unhighlight()
    {
        if (targetRenderer != null && originalMaterial != null)
            targetRenderer.material = originalMaterial;
    }
}
