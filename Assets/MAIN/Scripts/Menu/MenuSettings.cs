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
    }

    void OnDisable()
    {
        if (pixelSizeSlider != null)
            pixelSizeSlider.onValueChanged.RemoveListener(OnPixelSizeChanged);
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
}
