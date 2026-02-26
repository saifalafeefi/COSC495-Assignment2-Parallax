using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManagerX : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject normalEnemyPrefab;
    [SerializeField] private GameObject aggressiveEnemyPrefab;
    [SerializeField] private GameObject evasiveEnemyPrefab;
    [SerializeField] private GameObject tankEnemyPrefab;

    // backward compat — falls back to this if normalEnemyPrefab isn't assigned
    public GameObject enemyPrefab;

    [Header("Enemy Spawn Chances")]
    [SerializeField, Range(0f, 1f)] private float normalChance = 0.5f;
    [SerializeField, Range(0f, 1f)] private float aggressiveChance = 0.2f;
    [SerializeField, Range(0f, 1f)] private float evasiveChance = 0.15f;
    [SerializeField, Range(0f, 1f)] private float tankChance = 0.15f;

    [Header("Powerup Prefabs")]
    [SerializeField] private GameObject knockbackPowerupPrefab;
    [SerializeField] private GameObject smashPowerupPrefab;
    [SerializeField] private GameObject shieldPowerupPrefab;
    [SerializeField] private GameObject giantPowerupPrefab;
    [SerializeField] private GameObject hauntPowerupPrefab;

    [Header("Powerup Spawn Chances")]
    [SerializeField, Range(0f, 1f)] private float knockbackChance = 0.2f;
    [SerializeField, Range(0f, 1f)] private float smashChance = 0.2f;
    [SerializeField, Range(0f, 1f)] private float shieldChance = 0.2f;
    [SerializeField, Range(0f, 1f)] private float giantChance = 0.2f;
    [SerializeField, Range(0f, 1f)] private float hauntChance = 0.2f;

    // spawn area tuning — exposed so designers can tweak in Inspector
    [Header("Spawn Area")]
    [SerializeField] private float spawnRangeX = 10;
    [SerializeField] private float spawnZMin = 15;
    [SerializeField] private float spawnZMax = 25;

    [Header("Powerup Spawning")]
    [SerializeField] private Transform[] powerupSpawnPoints;       // drop empty GameObjects where you want powerups to appear
    [SerializeField] private float scorePowerupChance = 0.5f;      // 0-1 chance per enemy scored
    [SerializeField] private float powerupDroughtTime = 30f;       // seconds without powerup before auto-spawn
    [SerializeField] private int maxActivePowerups = 2;             // cap on simultaneous powerups

    [Header("Waves")]
    [SerializeField] private int waveCount = 1;
    [SerializeField] private float waveCooldown = 3f; // seconds between waves

    private int enemyCount;
    private bool isCountingDown;
    private float timeSinceLastPowerup;

    // tracks live powerup pickups so we skip FindGameObjectsWithTag each wave
    public static int activePowerupCount;

    [Header("Player")]
    [SerializeField] private GameObject player;
    private Vector3 playerStartPos; // saved at start so player resets independently
    private Rigidbody playerRb;     // cached once instead of GetComponent every wave reset

    void Start()
    {
        // remember where the player started
        playerStartPos = player.transform.position;
        playerRb = player.GetComponent<Rigidbody>();
        timeSinceLastPowerup = 0f;
    }

    void OnEnable()
    {
        GameManagerX.OnEnemyScored += OnEnemyScored;
    }

    void OnDisable()
    {
        GameManagerX.OnEnemyScored -= OnEnemyScored;
    }

    // chance to spawn a powerup when the player scores an enemy
    void OnEnemyScored()
    {
        if (Random.value < scorePowerupChance)
        {
            TrySpawnPowerup();
        }
    }

    void Update()
    {
        // stop spawning when game is over
        if (GameManagerX.Instance != null && GameManagerX.Instance.isGameOver)
        {
            return;
        }

        // use static counter instead of FindGameObjectsWithTag every frame
        enemyCount = EnemyX.aliveCount;

        if (enemyCount == 0 && !isCountingDown)
        {
            StartCoroutine(WaveCountdown());
        }

        // drought timer — auto-spawn a powerup if none exist for too long
        timeSinceLastPowerup += Time.deltaTime;
        if (timeSinceLastPowerup >= powerupDroughtTime && activePowerupCount <= 0)
        {
            TrySpawnPowerup();
        }
    }

    // countdown before next wave, shows timer text on screen
    IEnumerator WaveCountdown()
    {
        isCountingDown = true;

        // show "Wave Clear!" briefly then countdown
        if (GameManagerX.Instance != null)
            GameManagerX.Instance.SetWaveTimer("Wave Clear!");

        float remaining = waveCooldown;
        while (remaining > 0f)
        {
            if (GameManagerX.Instance != null)
                GameManagerX.Instance.SetWaveTimer("Next wave in " + Mathf.CeilToInt(remaining) + "...");
            remaining -= Time.deltaTime;
            yield return null;
        }

        if (GameManagerX.Instance != null)
            GameManagerX.Instance.SetWaveTimer("");

        SpawnEnemyWave(waveCount);
        isCountingDown = false;
    }

    // random position relative to the spawn manager's position (for enemies)
    Vector3 GenerateSpawnPosition()
    {
        float xPos = Random.Range(-spawnRangeX, spawnRangeX);
        float zPos = Random.Range(spawnZMin, spawnZMax);
        return transform.position + new Vector3(xPos, 0, zPos);
    }

    // pick a random designer-placed spawn point for powerups
    Vector3 GetRandomPowerupSpawnPoint()
    {
        if (powerupSpawnPoints == null || powerupSpawnPoints.Length == 0)
        {
            // fallback if no points set — spawn at this object's position
            Debug.LogWarning("SpawnManagerX: no powerup spawn points assigned!");
            return transform.position;
        }
        return powerupSpawnPoints[Random.Range(0, powerupSpawnPoints.Length)].position;
    }

    // pick a random enemy prefab using slider weights
    GameObject PickRandomEnemyPrefab()
    {
        GameObject normalPrefab = normalEnemyPrefab != null ? normalEnemyPrefab : enemyPrefab;

        // only count types that have a prefab assigned
        float wNormal = normalPrefab != null ? normalChance : 0f;
        float wAggressive = aggressiveEnemyPrefab != null ? aggressiveChance : 0f;
        float wEvasive = evasiveEnemyPrefab != null ? evasiveChance : 0f;
        float wTank = tankEnemyPrefab != null ? tankChance : 0f;

        float total = wNormal + wAggressive + wEvasive + wTank;
        if (total <= 0f) return normalPrefab;

        float roll = Random.Range(0f, total);

        if (roll < wNormal) return normalPrefab;
        roll -= wNormal;
        if (roll < wAggressive) return aggressiveEnemyPrefab;
        roll -= wAggressive;
        if (roll < wEvasive) return evasiveEnemyPrefab;
        return tankEnemyPrefab;
    }

    // weighted random pick from all assigned powerup prefabs (same pattern as enemy spawning)
    GameObject PickRandomPowerupPrefab()
    {
        float wKnockback = knockbackPowerupPrefab != null ? knockbackChance : 0f;
        float wSmash = smashPowerupPrefab != null ? smashChance : 0f;
        float wShield = shieldPowerupPrefab != null ? shieldChance : 0f;
        float wGiant = giantPowerupPrefab != null ? giantChance : 0f;
        float wHaunt = hauntPowerupPrefab != null ? hauntChance : 0f;

        float total = wKnockback + wSmash + wShield + wGiant + wHaunt;
        if (total <= 0f) return null;

        float roll = Random.Range(0f, total);

        if (roll < wKnockback) return knockbackPowerupPrefab;
        roll -= wKnockback;
        if (roll < wSmash) return smashPowerupPrefab;
        roll -= wSmash;
        if (roll < wShield) return shieldPowerupPrefab;
        roll -= wShield;
        if (roll < wGiant) return giantPowerupPrefab;
        return hauntPowerupPrefab;
    }

    // spawn a random powerup if under the cap
    void TrySpawnPowerup()
    {
        if (activePowerupCount >= maxActivePowerups) return;

        GameObject prefab = PickRandomPowerupPrefab();
        if (prefab == null) return;

        Vector3 pos = GetRandomPowerupSpawnPoint();
        GameObject spawned = Instantiate(prefab, pos, prefab.transform.rotation);
        spawned.AddComponent<PowerupTracker>();

        timeSinceLastPowerup = 0f;
    }

    void SpawnEnemyWave(int enemiesToSpawn)
    {
        // spawn enemies — each one picks a weighted random type based on current wave
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            GameObject prefab = PickRandomEnemyPrefab();
            if (prefab != null)
            {
                Instantiate(prefab, GenerateSpawnPosition() + prefab.transform.position, prefab.transform.rotation);
            }
        }

        waveCount++;
        ResetPlayerPosition();

        // update HUD with new wave number
        if (GameManagerX.Instance != null)
        {
            GameManagerX.Instance.SetWave(waveCount);
        }
    }

    // reset player to where it was at game start
    void ResetPlayerPosition()
    {
        // cancel any active smash/slow-mo so state doesn't carry over
        var controller = player.GetComponent<PlayerControllerX>();
        if (controller != null)
            controller.ForceResetState();

        // teleport via MovePosition so physics doesn't roll back the position
        playerRb.linearVelocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;
        playerRb.MovePosition(playerStartPos);
        player.transform.position = playerStartPos;
    }

    // reset statics between scenes
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        activePowerupCount = 0;
    }
}

/// <summary>
/// lightweight tracker added to spawned powerups — keeps SpawnManagerX.activePowerupCount
/// in sync without needing FindGameObjectsWithTag each wave
/// </summary>
public class PowerupTracker : MonoBehaviour
{
    void Start() { SpawnManagerX.activePowerupCount++; }
    void OnDestroy() { SpawnManagerX.activePowerupCount--; }
}
