using UnityEngine;

public class MenuSmashTarget : MonoBehaviour
{
    [SerializeField] MenuOption optionType;
    [SerializeField] Renderer targetRenderer;
    [SerializeField] Material defaultMaterial;
    [SerializeField] Material highlightMaterial;

    public MenuOption OptionType => optionType;

    public void Highlight()
    {
        if (targetRenderer != null && highlightMaterial != null)
            targetRenderer.material = highlightMaterial;
    }

    public void Unhighlight()
    {
        if (targetRenderer != null && defaultMaterial != null)
            targetRenderer.material = defaultMaterial;
    }
}
