using UnityEngine;

public class MusicManager : MonoBehaviour
{
    static MusicManager instance;

    [SerializeField] AudioClip musicClip;
    [SerializeField] [Range(0f, 1f)] float volume = 0.5f;

    AudioSource audioSource;

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
        audioSource.volume = SharedVolume;
        audioSource.playOnAwake = false;
        audioSource.Play();
    }

    // keep volume in sync if changed at runtime
    public void SetVolume(float vol)
    {
        if (audioSource != null)
            audioSource.volume = vol;
    }
}
