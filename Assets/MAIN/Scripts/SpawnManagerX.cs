using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct EnemySpawnWeight
{
    public float normal;
    public float aggressive;
    public float evasive;
    public float tank;
}

public class SpawnManagerX : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject normalEnemyPrefab;
    [SerializeField] private GameObject aggressiveEnemyPrefab;
    [SerializeField] private GameObject evasiveEnemyPrefab;
    [SerializeField] private GameObject tankEnemyPrefab;

    // backward compat — falls back to this if normalEnemyPrefab isn't assigned
    public GameObject enemyPrefab;

    [Header("Spawn Weights")]
    [SerializeField] private EnemySpawnWeight baseWeights = new EnemySpawnWeight { normal = 1f, aggressive = 0f, evasive = 0f, tank = 0f };
    [SerializeField] private EnemySpawnWeight weightScalePerWave = new EnemySpawnWeight { normal = 0f, aggressive = 0.15f, evasive = 0.1f, tank = 0.05f };
    [SerializeField] private int aggressiveStartWave = 3;
    [SerializeField] private int evasiveStartWave = 5;
    [SerializeField] private int tankStartWave = 7;

    [Header("Powerup Prefabs")]
    [SerializeField] private GameObject knockbackPowerupPrefab;
    [SerializeField] private GameObject smashPowerupPrefab;
    [SerializeField] private GameObject shieldPowerupPrefab;
    [SerializeField] private GameObject giantPowerupPrefab;
    [SerializeField] private GameObject hauntPowerupPrefab;

    // spawn area tuning — exposed so designers can tweak in Inspector
    [Header("Spawn Area")]
    [SerializeField] private float spawnRangeX = 10;
    [SerializeField] private float spawnZMin = 15;
    [SerializeField] private float spawnZMax = 25;

    [Header("Waves")]
    [SerializeField] private int waveCount = 1;
    [SerializeField] private float waveCooldown = 3f; // seconds between waves

    private int enemyCount;
    private int powerupCycle = 0; // cycles through: 0=knockback, 1=smash, 2=shield, 3=giant, 4=haunt
    private bool isCountingDown;

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

    // random position relative to the spawn manager's position
    Vector3 GenerateSpawnPosition()
    {
        float xPos = Random.Range(-spawnRangeX, spawnRangeX);
        float zPos = Random.Range(spawnZMin, spawnZMax);
        return transform.position + new Vector3(xPos, 0, zPos);
    }

    // pick a random enemy prefab based on wave-scaled weights
    GameObject PickRandomEnemyPrefab()
    {
        // resolve normal prefab (backward compat with old enemyPrefab field)
        GameObject normalPrefab = normalEnemyPrefab != null ? normalEnemyPrefab : enemyPrefab;

        // compute weights for current wave
        float wNormal = normalPrefab != null ? Mathf.Max(0f, baseWeights.normal + waveCount * weightScalePerWave.normal) : 0f;
        float wAggressive = (aggressiveEnemyPrefab != null && waveCount >= aggressiveStartWave)
            ? Mathf.Max(0f, baseWeights.aggressive + (waveCount - aggressiveStartWave) * weightScalePerWave.aggressive)
            : 0f;
        float wEvasive = (evasiveEnemyPrefab != null && waveCount >= evasiveStartWave)
            ? Mathf.Max(0f, baseWeights.evasive + (waveCount - evasiveStartWave) * weightScalePerWave.evasive)
            : 0f;
        float wTank = (tankEnemyPrefab != null && waveCount >= tankStartWave)
            ? Mathf.Max(0f, baseWeights.tank + (waveCount - tankStartWave) * weightScalePerWave.tank)
            : 0f;

        float total = wNormal + wAggressive + wEvasive + wTank;
        if (total <= 0f) return normalPrefab; // safety fallback

        float roll = Random.Range(0f, total);

        if (roll < wNormal) return normalPrefab;
        roll -= wNormal;
        if (roll < wAggressive) return aggressiveEnemyPrefab;
        roll -= wAggressive;
        if (roll < wEvasive) return evasiveEnemyPrefab;
        return tankEnemyPrefab;
    }

    void SpawnEnemyWave(int enemiesToSpawn)
    {
        // spawn a powerup if none exist (static counter replaces 5x FindGameObjectsWithTag)
        if (activePowerupCount <= 0)
        {
            GameObject prefab = null;
            if (powerupCycle == 0 && knockbackPowerupPrefab != null)
                prefab = knockbackPowerupPrefab;
            else if (powerupCycle == 1 && smashPowerupPrefab != null)
                prefab = smashPowerupPrefab;
            else if (powerupCycle == 2 && shieldPowerupPrefab != null)
                prefab = shieldPowerupPrefab;
            else if (powerupCycle == 3 && giantPowerupPrefab != null)
                prefab = giantPowerupPrefab;
            else if (powerupCycle == 4 && hauntPowerupPrefab != null)
                prefab = hauntPowerupPrefab;

            // fallback to knockback if the selected prefab isn't assigned
            if (prefab == null && knockbackPowerupPrefab != null)
                prefab = knockbackPowerupPrefab;

            if (prefab != null)
            {
                GameObject spawned = Instantiate(prefab, GenerateSpawnPosition() + prefab.transform.position, prefab.transform.rotation);
                // attach tracker so activePowerupCount stays in sync when picked up or destroyed
                spawned.AddComponent<PowerupTracker>();
            }

            powerupCycle = (powerupCycle + 1) % 5;
        }

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
        player.transform.position = playerStartPos;
        playerRb.linearVelocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;
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
