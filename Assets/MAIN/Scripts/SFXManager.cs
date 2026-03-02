using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("Player")]
    [SerializeField] AudioClip landingClip;
    [SerializeField, Range(0f, 1f)] float landingVolume = 1f;
    [SerializeField] float minFallHeight = 3f; // minimum Y drop before landing SFX plays

    [Header("Combat")]
    [SerializeField] AudioClip enemyHitClip;       // player bumps into enemy
    [SerializeField, Range(0f, 1f)] float enemyHitVolume = 1f;
    [SerializeField] AudioClip enemyScoreClip;      // enemy knocked into Enemy Goal
    [SerializeField, Range(0f, 1f)] float enemyScoreVolume = 1f;
    [SerializeField] AudioClip playerGoalClip;      // enemy reaches Player Goal (lose life)
    [SerializeField, Range(0f, 1f)] float playerGoalVolume = 1f;

    [Header("Powerups")]
    [SerializeField] AudioClip powerupPickupClip;   // any powerup collected
    [SerializeField, Range(0f, 1f)] float powerupPickupVolume = 1f;

    [Header("Smash")]
    [SerializeField] AudioClip smashLaunchClip;     // phase 1 — jump
    [SerializeField, Range(0f, 1f)] float smashLaunchVolume = 1f;
    [SerializeField] AudioClip smashDiveClip;       // phase 4 — dive starts
    [SerializeField, Range(0f, 1f)] float smashDiveVolume = 1f;
    [SerializeField] AudioClip smashImpactClip;     // phase 5 — landing
    [SerializeField, Range(0f, 1f)] float smashImpactVolume = 1f;

    [Header("Powerup Effects")]
    [SerializeField] AudioClip shieldBreakClip;     // shield destroys an enemy
    [SerializeField, Range(0f, 1f)] float shieldBreakVolume = 1f;
    [SerializeField] AudioClip giantSquishClip;     // giant squishes an enemy
    [SerializeField, Range(0f, 1f)] float giantSquishVolume = 1f;
    [SerializeField] AudioClip hauntApplyClip;      // enemy gets haunted
    [SerializeField, Range(0f, 1f)] float hauntApplyVolume = 1f;
    [SerializeField] AudioClip rushReadyClip;       // rush charge reaches 100%
    [SerializeField, Range(0f, 1f)] float rushReadyVolume = 1f;
    [SerializeField] AudioClip rushActivateClip;    // player triggers rush
    [SerializeField, Range(0f, 1f)] float rushActivateVolume = 1f;
    [SerializeField] AudioClip rushLoopClip;        // looping rush ambience while active
    [SerializeField, Range(0f, 1f)] float rushLoopVolume = 0.85f;
    [SerializeField, Min(0f)] float rushFadeInDuration = 0.25f;
    [SerializeField, Min(0f)] float rushFadeOutDuration = 0.35f;

    [Header("UI / Interaction")]
    [SerializeField] AudioClip uiSelectClip;          // button click, menu selection, any UI interaction
    [SerializeField, Range(0f, 1f)] float uiSelectVolume = 1f;

    [Header("Waves")]
    [SerializeField] AudioClip countdownTickClip;    // each second of the countdown timer
    [SerializeField, Range(0f, 1f)] float countdownTickVolume = 1f;
    [SerializeField] AudioClip waveStartClip;        // new wave begins
    [SerializeField, Range(0f, 1f)] float waveStartVolume = 1f;

    AudioSource audioSource;
    AudioSource rushLoopSource;
    Coroutine rushFadeRoutine;
    bool rushLoopTargetOn;
    bool pausedByMenu;

    // shared across scenes — MenuSettings writes these
    public static float SharedVolume { get; set; } = 1f;
    public static float SharedMasterVolume { get; set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadSavedVolumes()
    {
        SharedVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        SharedMasterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("SFXVolume", SharedVolume);
        PlayerPrefs.SetFloat("MasterVolume", SharedMasterVolume);
        PlayerPrefs.Save();
    }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        rushLoopSource = gameObject.AddComponent<AudioSource>();
        rushLoopSource.playOnAwake = false;
        rushLoopSource.loop = true;
        rushLoopSource.volume = 0f;
    }

    // call after changing master or sfx volume to apply immediately
    public void ApplyVolume()
    {
        if (audioSource != null)
            audioSource.volume = SharedMasterVolume * SharedVolume;

        // keep loop volume in sync with settings changes
        if (rushLoopSource != null && rushLoopSource.isPlaying)
            rushLoopSource.volume = GetRushTargetVolume();
    }

    void Update()
    {
        // pause/unpause SFX only for pause menu (not game over)
        if (audioSource == null) return;

        bool shouldPauseForMenu = GameManagerX.Instance != null && GameManagerX.Instance.isPaused;

        if (shouldPauseForMenu && !pausedByMenu)
        {
            audioSource.Pause();
            if (rushLoopSource != null) rushLoopSource.Pause();
            pausedByMenu = true;
        }
        else if (!shouldPauseForMenu && pausedByMenu)
        {
            audioSource.UnPause();
            if (rushLoopSource != null) rushLoopSource.UnPause();
            pausedByMenu = false;
        }
    }

    // generic play — volume = master * sfx
    public void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, SharedMasterVolume * SharedVolume);
    }

    // play with per-clip volume multiplier
    void Play(AudioClip clip, float clipVolume)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, SharedMasterVolume * SharedVolume * clipVolume);
    }

    // --- named helpers so callers don't need clip references ---

    public void PlayLanding()         => Play(landingClip, landingVolume);
    public void PlayEnemyHit()        => Play(enemyHitClip, enemyHitVolume);
    public void PlayEnemyScore()      => Play(enemyScoreClip, enemyScoreVolume);
    public void PlayPlayerGoal()      => Play(playerGoalClip, playerGoalVolume);
    public void PlayPowerupPickup()   => Play(powerupPickupClip, powerupPickupVolume);
    public void PlaySmashLaunch()     => Play(smashLaunchClip, smashLaunchVolume);
    public void PlaySmashDive()       => Play(smashDiveClip, smashDiveVolume);
    public void PlaySmashImpact()     => Play(smashImpactClip, smashImpactVolume);
    public void PlayShieldBreak()     => Play(shieldBreakClip, shieldBreakVolume);
    public void PlayGiantSquish()     => Play(giantSquishClip, giantSquishVolume);
    public void PlayHauntApply()      => Play(hauntApplyClip, hauntApplyVolume);
    public void PlayRushReady()       => Play(rushReadyClip, rushReadyVolume);
    public void PlayRushActivate()    => Play(rushActivateClip, rushActivateVolume);
    public void PlayUISelect()         => Play(uiSelectClip, uiSelectVolume);
    public void PlayCountdownTick()    => Play(countdownTickClip, countdownTickVolume);
    public void PlayWaveStart()       => Play(waveStartClip, waveStartVolume);

    public void StartRushLoop()
    {
        if (rushLoopClip == null || rushLoopSource == null) return;

        rushLoopTargetOn = true;
        rushLoopSource.clip = rushLoopClip;
        if (!rushLoopSource.isPlaying)
        {
            rushLoopSource.volume = 0f;
            rushLoopSource.Play();
        }

        StartRushFade(GetRushTargetVolume(), rushFadeInDuration, stopAfterFade: false);
    }

    public void StopRushLoop()
    {
        if (rushLoopSource == null) return;

        rushLoopTargetOn = false;
        if (!rushLoopSource.isPlaying)
        {
            rushLoopSource.volume = 0f;
            return;
        }

        StartRushFade(0f, rushFadeOutDuration, stopAfterFade: true);
    }

    // landing needs height tracking — called from PlayerControllerX
    public float MinFallHeight => minFallHeight;

    void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        // don't let one-shots carry across scenes
        if (audioSource == null) return;
        audioSource.Stop();
        if (rushFadeRoutine != null)
        {
            StopCoroutine(rushFadeRoutine);
            rushFadeRoutine = null;
        }
        if (rushLoopSource != null)
        {
            rushLoopSource.Stop();
            rushLoopSource.volume = 0f;
            rushLoopSource.clip = null;
        }
        rushLoopTargetOn = false;
        pausedByMenu = false;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    float GetRushTargetVolume()
    {
        return rushLoopTargetOn ? rushLoopVolume * SharedMasterVolume * SharedVolume : 0f;
    }

    void StartRushFade(float targetVolume, float duration, bool stopAfterFade)
    {
        if (rushFadeRoutine != null)
            StopCoroutine(rushFadeRoutine);
        rushFadeRoutine = StartCoroutine(FadeRushLoopRoutine(targetVolume, duration, stopAfterFade));
    }

    IEnumerator FadeRushLoopRoutine(float targetVolume, float duration, bool stopAfterFade)
    {
        if (rushLoopSource == null) yield break;

        float start = rushLoopSource.volume;
        if (duration <= 0.0001f)
        {
            rushLoopSource.volume = targetVolume;
            if (stopAfterFade && targetVolume <= 0.0001f)
                rushLoopSource.Stop();
            rushFadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rushLoopSource.volume = Mathf.Lerp(start, targetVolume, t);
            yield return null;
        }

        rushLoopSource.volume = targetVolume;
        if (stopAfterFade && targetVolume <= 0.0001f)
            rushLoopSource.Stop();

        rushFadeRoutine = null;
    }
}
