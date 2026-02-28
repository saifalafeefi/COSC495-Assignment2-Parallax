using UnityEngine;

// attach to the GRASS_MANAGER GameObject
// toggles PointGrassRenderer scripts only (not the manager itself, since
// PointGrassDisplacementManager doesn't handle re-enable gracefully)
public class GrassToggle : MonoBehaviour
{
    public static bool SharedEnabled { get; set; } = true;
    static GrassToggle activeInstance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadSetting()
    {
        SharedEnabled = PlayerPrefs.GetInt("GrassEnabled", 1) == 1;
    }

    public static void Save()
    {
        PlayerPrefs.SetInt("GrassEnabled", SharedEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    // call from MenuSettings when toggle changes
    public static void Apply()
    {
        var renderers = FindObjectsByType<MicahW.PointGrass.PointGrassRenderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = SharedEnabled;
    }

    void Start()
    {
        activeInstance = this;
        Apply();
    }

    void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
    }
}
