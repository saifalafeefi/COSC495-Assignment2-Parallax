using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManagerX : MonoBehaviour
{
    public GameObject enemyPrefab;
    public GameObject knockbackPowerupPrefab;
    public GameObject smashPowerupPrefab;
    public GameObject shieldPowerupPrefab;
    public GameObject giantPowerupPrefab;
    public GameObject hauntPowerupPrefab;

    // spawn area tuning — exposed so designers can tweak in Inspector
    [SerializeField] private float spawnRangeX = 10;
    [SerializeField] private float spawnZMin = 15;
    [SerializeField] private float spawnZMax = 25;

    public int enemyCount;
    public int waveCount = 1;
    public float waveCooldown = 3f; // seconds between waves

    private int powerupCycle = 0; // cycles through: 0=knockback, 1=smash, 2=shield, 3=giant, 4=haunt
    private bool isCountingDown;

    // tracks live powerup pickups so we skip FindGameObjectsWithTag each wave
    public static int activePowerupCount;

    public GameObject player;
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

        // spawn enemies based on wave number, offset by enemy prefab's transform
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            Instantiate(enemyPrefab, GenerateSpawnPosition() + enemyPrefab.transform.position, enemyPrefab.transform.rotation);
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
