using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("Player")]
    [SerializeField] AudioClip landingClip;
    [SerializeField] float minFallHeight = 3f; // minimum Y drop before landing SFX plays

    [Header("Combat")]
    [SerializeField] AudioClip enemyHitClip;       // player bumps into enemy
    [SerializeField] AudioClip enemyScoreClip;      // enemy knocked into Enemy Goal
    [SerializeField] AudioClip playerGoalClip;      // enemy reaches Player Goal (lose life)

    [Header("Powerups")]
    [SerializeField] AudioClip powerupPickupClip;   // any powerup collected

    [Header("Smash")]
    [SerializeField] AudioClip smashLaunchClip;     // phase 1 — jump
    [SerializeField] AudioClip smashDiveClip;       // phase 4 — dive starts
    [SerializeField] AudioClip smashImpactClip;     // phase 5 — landing

    [Header("Powerup Effects")]
    [SerializeField] AudioClip shieldBreakClip;     // shield destroys an enemy
    [SerializeField] AudioClip giantSquishClip;     // giant squishes an enemy
    [SerializeField] AudioClip hauntApplyClip;      // enemy gets haunted

    [Header("Waves")]
    [SerializeField] AudioClip countdownTickClip;    // each second of the countdown timer
    [SerializeField] AudioClip waveStartClip;        // new wave begins

    AudioSource audioSource;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void Update()
    {
        // pause/unpause SFX with game pause (timeScale = 0)
        if (audioSource == null) return;
        bool paused = Time.timeScale == 0f;
        if (paused && audioSource.isPlaying) audioSource.Pause();
        else if (!paused && !audioSource.isPlaying) audioSource.UnPause();
    }

    // generic play — for anything not covered by a named method
    public void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    // --- named helpers so callers don't need clip references ---

    public void PlayLanding()         => Play(landingClip);
    public void PlayEnemyHit()        => Play(enemyHitClip);
    public void PlayEnemyScore()      => Play(enemyScoreClip);
    public void PlayPlayerGoal()      => Play(playerGoalClip);
    public void PlayPowerupPickup()   => Play(powerupPickupClip);
    public void PlaySmashLaunch()     => Play(smashLaunchClip);
    public void PlaySmashDive()       => Play(smashDiveClip);
    public void PlaySmashImpact()     => Play(smashImpactClip);
    public void PlayShieldBreak()     => Play(shieldBreakClip);
    public void PlayGiantSquish()     => Play(giantSquishClip);
    public void PlayHauntApply()      => Play(hauntApplyClip);
    public void PlayCountdownTick()    => Play(countdownTickClip);
    public void PlayWaveStart()       => Play(waveStartClip);

    // landing needs height tracking — called from PlayerControllerX
    public float MinFallHeight => minFallHeight;
}
