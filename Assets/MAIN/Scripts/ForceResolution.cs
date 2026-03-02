using UnityEngine;

// forces 1920x1080 fullscreen regardless of monitor native resolution
// no MonoBehaviour needed — runs automatically before any scene loads
public static class ForceResolution
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
    }
}
