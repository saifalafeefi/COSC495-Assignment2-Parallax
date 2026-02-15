using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManagerX : MonoBehaviour
{
    public GameObject enemyPrefab;
    public GameObject powerupPrefab;
    public GameObject smashPowerupPrefab;

    private float spawnRangeX = 10;
    private float spawnZMin = 15;
    private float spawnZMax = 25;

    public int enemyCount;
    public int waveCount = 1;

    private bool spawnSmashNext = false; // toggles which powerup type spawns next

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

        if (enemyCount == 0)
        {
            SpawnEnemyWave(waveCount);
        }
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
        // spawn a powerup if none exist, alternating type each wave
        // uses the prefab's own transform.position as offset so you can adjust it per prefab
        bool hasRegularPowerup = GameObject.FindGameObjectsWithTag("KnockbackPowerup").Length > 0;
        bool hasSmashPowerup = GameObject.FindGameObjectsWithTag("SmashPowerup").Length > 0;

        if (!hasRegularPowerup && !hasSmashPowerup)
        {
            if (spawnSmashNext && smashPowerupPrefab != null)
            {
                Instantiate(smashPowerupPrefab, GenerateSpawnPosition() + smashPowerupPrefab.transform.position, smashPowerupPrefab.transform.rotation);
            }
            else
            {
                Instantiate(powerupPrefab, GenerateSpawnPosition() + powerupPrefab.transform.position, powerupPrefab.transform.rotation);
            }
            spawnSmashNext = !spawnSmashNext;
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
