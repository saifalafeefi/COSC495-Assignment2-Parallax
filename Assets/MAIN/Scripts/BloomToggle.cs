using UnityEngine;

// attach directly to the BLOOM GameObject — enables/disables itself based on saved preference
// tracks the active instance so settings can toggle it live from any scene
public class BloomToggle : MonoBehaviour
{
    public static bool SharedEnabled { get; set; } = true;
    private static BloomToggle activeInstance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadSetting()
    {
        SharedEnabled = PlayerPrefs.GetInt("BloomEnabled", 1) == 1;
    }

    public static void Save()
    {
        PlayerPrefs.SetInt("BloomEnabled", SharedEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    // call from MenuSettings when toggle changes — updates live in current scene
    public static void Apply()
    {
        if (activeInstance != null)
            activeInstance.gameObject.SetActive(SharedEnabled);
    }

    void Awake()
    {
        activeInstance = this;
        gameObject.SetActive(SharedEnabled);
    }

    void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
    }
}
