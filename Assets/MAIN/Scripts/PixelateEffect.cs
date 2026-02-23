using UnityEngine;
using UnityEngine.Rendering.Universal;

// pixelates the screen by lowering URP's render scale + forcing point filtering
// attach to any GameObject — no camera setup, no RawImage, no extra objects needed
// pixel size is shared via static so MenuSettings can control it from any scene
// persists between sessions via PlayerPrefs
public class PixelateEffect : MonoBehaviour
{
    [Tooltip("Pixel size. 1 = normal, higher = chunkier pixels")]
    [Range(1, 16)] public int pixelSize = 3;

    // static shared pixel size — survives across scenes, writable from anywhere
    public static int SharedPixelSize = 3;

    private const string PREFS_KEY = "PixelSize";

    private float originalRenderScale;
    private UniversalRenderPipelineAsset urpAsset;

    // load saved setting as early as possible (before any Start runs)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadSavedSettings()
    {
        SharedPixelSize = PlayerPrefs.GetInt(PREFS_KEY, 3);
    }

    public static void Save()
    {
        PlayerPrefs.SetInt(PREFS_KEY, SharedPixelSize);
        PlayerPrefs.Save();
    }

    void Start()
    {
        urpAsset = UniversalRenderPipeline.asset;
        if (urpAsset != null)
            originalRenderScale = urpAsset.renderScale;

        // pick up whatever the shared value is (loaded from prefs or set from menu)
        pixelSize = SharedPixelSize;
    }

    void Update()
    {
        if (urpAsset == null) return;

        // sync — static changes (from menu) update the instance
        if (pixelSize != SharedPixelSize)
            pixelSize = SharedPixelSize;

        float scale = 1f / Mathf.Max(1, pixelSize);
        urpAsset.renderScale = scale;

        // point filtering gives the hard pixel edges (no bilinear smoothing)
        urpAsset.upscalingFilter = UpscalingFilterSelection.Point;
    }

    void OnDestroy()
    {
        // restore original render scale so it doesn't persist after play mode
        if (urpAsset != null)
            urpAsset.renderScale = originalRenderScale;
    }

    void OnDisable()
    {
        if (urpAsset != null)
            urpAsset.renderScale = originalRenderScale;
    }
}
