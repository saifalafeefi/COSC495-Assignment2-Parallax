using UnityEngine;

public class MusicManager : MonoBehaviour
{
    static MusicManager instance;
    public static MusicManager Instance => instance;

    [SerializeField] AudioClip musicClip;
    [SerializeField] [Range(0f, 1f)] float volume = 0.5f;

    [Header("Rush Fade")]
    [SerializeField] float rushFadeOutDuration = 0.5f;
    [SerializeField] float rushFadeInDuration = 1f;
    [SerializeField] [Range(0f, 1f)] float rushMutedVolume = 0f;

    AudioSource audioSource;

    // fade state
    float fadeTarget = -1f;
    float fadeSpeed;

    // shared across scenes — MenuSettings writes this, MusicManager reads it
    public static float SharedVolume { get; set; } = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadSavedVolume()
    {
        SharedVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("MusicVolume", SharedVolume);
        PlayerPrefs.Save();
    }

    void Awake()
    {
        // singleton — survive scene loads, destroy duplicates
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = musicClip;
        audioSource.loop = true;
        audioSource.volume = SFXManager.SharedMasterVolume * SharedVolume;
        audioSource.playOnAwake = false;
        audioSource.Play();
    }

    void Update()
    {
        if (fadeTarget < 0f || audioSource == null) return;

        audioSource.volume = Mathf.MoveTowards(audioSource.volume, fadeTarget, fadeSpeed * Time.unscaledDeltaTime);
        if (Mathf.Approximately(audioSource.volume, fadeTarget))
            fadeTarget = -1f;
    }

    // effective volume = master * music
    public void ApplyVolume()
    {
        if (audioSource != null)
        {
            // cancel any active fade so the new volume sticks
            fadeTarget = -1f;
            audioSource.volume = SFXManager.SharedMasterVolume * SharedVolume;
        }
    }

    // keep volume in sync if changed at runtime
    public void SetVolume(float vol)
    {
        if (audioSource != null)
            audioSource.volume = vol;
    }

    // fade music down for RUSH
    public void FadeToRush()
    {
        if (audioSource == null) return;
        float target = SFXManager.SharedMasterVolume * SharedVolume * rushMutedVolume;
        float duration = rushFadeOutDuration > 0.001f ? rushFadeOutDuration : 0.001f;
        fadeSpeed = Mathf.Abs(audioSource.volume - target) / duration;
        fadeTarget = target;
    }

    // fade music back after RUSH ends
    public void FadeFromRush()
    {
        if (audioSource == null) return;
        float target = SFXManager.SharedMasterVolume * SharedVolume;
        float duration = rushFadeInDuration > 0.001f ? rushFadeInDuration : 0.001f;
        fadeSpeed = Mathf.Abs(target - audioSource.volume) / duration;
        fadeTarget = target;
    }
}
