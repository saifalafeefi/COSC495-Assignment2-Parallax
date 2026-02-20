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

    private float spawnRangeX = 10;
    private float spawnZMin = 15;
    private float spawnZMax = 25;

    public int enemyCount;
    public int waveCount = 1;
    public float waveCooldown = 3f; // seconds between waves

    private int powerupCycle = 0; // cycles through: 0=knockback, 1=smash, 2=shield, 3=giant
    private bool isCountingDown;

    public GameObject player;
    private Vector3 playerStartPos; // saved at start so player resets independently

    void Start()
    {
        // remember where the player started
        playerStartPos = player.transform.position;
    }

    void Update()
    {
        // stop spawning when game is over
        if (GameManagerX.Instance != null && GameManagerX.Instance.isGameOver)
        {
            return;
        }

        enemyCount = GameObject.FindGameObjectsWithTag("Enemy").Length;

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
        // spawn a powerup if none exist, cycling: knockback → smash → shield
        bool hasKnockback = GameObject.FindGameObjectsWithTag("KnockbackPowerup").Length > 0;
        bool hasSmash = GameObject.FindGameObjectsWithTag("SmashPowerup").Length > 0;
        bool hasShield = GameObject.FindGameObjectsWithTag("ShieldPowerup").Length > 0;
        bool hasGiant = GameObject.FindGameObjectsWithTag("GiantPowerup").Length > 0;

        if (!hasKnockback && !hasSmash && !hasShield && !hasGiant)
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

            // fallback to knockback if the selected prefab isn't assigned
            if (prefab == null && knockbackPowerupPrefab != null)
                prefab = knockbackPowerupPrefab;

            if (prefab != null)
                Instantiate(prefab, GenerateSpawnPosition() + prefab.transform.position, prefab.transform.rotation);

            powerupCycle = (powerupCycle + 1) % 4;
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
        player.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        player.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
    }
}
