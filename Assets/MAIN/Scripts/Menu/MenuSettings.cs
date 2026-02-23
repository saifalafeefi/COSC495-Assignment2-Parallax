using UnityEngine;
using UnityEngine.UI;
using TMPro;

// manages settings panel UI — drives PixelateEffect.SharedPixelSize directly
// works even if PixelateEffect is in a different scene
// saves to PlayerPrefs so settings persist between sessions
// attach to the settings panel root GameObject
public class MenuSettings : MonoBehaviour
{
    [Header("Pixel Size")]
    [SerializeField] Slider pixelSizeSlider;
    [SerializeField] TextMeshProUGUI pixelSizeLabel;

    [Header("Skin Selection (Optional)")]
    [SerializeField] PlayerSkinApplier skinPreview;
    [SerializeField] TMP_Dropdown skinDropdown;
    [SerializeField] TextMeshProUGUI skinLabel;

    void OnEnable()
    {
        if (pixelSizeSlider != null)
        {
            pixelSizeSlider.wholeNumbers = true;
            pixelSizeSlider.minValue = 1;
            pixelSizeSlider.maxValue = 16;

            // read current shared value (already loaded from PlayerPrefs on app start)
            pixelSizeSlider.value = PixelateEffect.SharedPixelSize;

            pixelSizeSlider.onValueChanged.AddListener(OnPixelSizeChanged);
            UpdatePixelLabel(pixelSizeSlider.value);
        }

        SetupSkinUI();
    }

    void OnDisable()
    {
        if (pixelSizeSlider != null)
            pixelSizeSlider.onValueChanged.RemoveListener(OnPixelSizeChanged);

        if (skinDropdown != null)
            skinDropdown.onValueChanged.RemoveListener(OnSkinChangedFromDropdown);
    }

    void OnPixelSizeChanged(float value)
    {
        int size = Mathf.RoundToInt(value);

        PixelateEffect.SharedPixelSize = size;
        PixelateEffect.Save();

        // also update any live instance in this scene
        var live = FindAnyObjectByType<PixelateEffect>();
        if (live != null)
            live.pixelSize = size;

        UpdatePixelLabel(value);
    }

    void UpdatePixelLabel(float value)
    {
        if (pixelSizeLabel != null)
            pixelSizeLabel.text = $"Pixel Size: {Mathf.RoundToInt(value)}";
    }

    void SetupSkinUI()
    {
        if (skinPreview == null)
            skinPreview = FindAnyObjectByType<PlayerSkinApplier>();

        if (skinPreview == null || skinPreview.SkinCount <= 0)
        {
            UpdateSkinLabel("Skin: None");
            return;
        }

        if (skinDropdown != null)
        {
            skinDropdown.onValueChanged.RemoveListener(OnSkinChangedFromDropdown);

            var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();
            for (int i = 0; i < skinPreview.SkinCount; i++)
                options.Add(new TMP_Dropdown.OptionData(skinPreview.GetSkinName(i)));

            skinDropdown.ClearOptions();
            skinDropdown.AddOptions(options);
            skinDropdown.SetValueWithoutNotify(skinPreview.SelectedSkinIndex);
            skinDropdown.onValueChanged.AddListener(OnSkinChangedFromDropdown);
        }

        skinPreview.ApplySelectedSkin();
        UpdateSkinLabel($"Skin: {skinPreview.GetSkinName(skinPreview.SelectedSkinIndex)}");
    }

    void OnSkinChangedFromDropdown(int index)
    {
        if (skinPreview == null) return;
        skinPreview.SetSkin(index, true);
        UpdateSkinLabel($"Skin: {skinPreview.GetSkinName(skinPreview.SelectedSkinIndex)}");
    }

    void UpdateSkinLabel(string text)
    {
        if (skinLabel != null)
            skinLabel.text = text;
    }

    // optional button hooks for prev/next controls
    public void OnPrevSkin()
    {
        if (skinPreview == null) return;
        skinPreview.PreviousSkin();
        SyncSkinUiAfterButtonChange();
    }

    public void OnNextSkin()
    {
        if (skinPreview == null) return;
        skinPreview.NextSkin();
        SyncSkinUiAfterButtonChange();
    }

    void SyncSkinUiAfterButtonChange()
    {
        if (skinPreview == null) return;

        if (skinDropdown != null)
            skinDropdown.SetValueWithoutNotify(skinPreview.SelectedSkinIndex);

        UpdateSkinLabel($"Skin: {skinPreview.GetSkinName(skinPreview.SelectedSkinIndex)}");
    }
}
