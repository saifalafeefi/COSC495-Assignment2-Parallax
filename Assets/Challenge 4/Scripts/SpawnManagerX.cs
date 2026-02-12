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

    void Update()
    {
        enemyCount = GameObject.FindGameObjectsWithTag("Enemy").Length;

        if (enemyCount == 0)
        {
            SpawnEnemyWave(waveCount);
        }
    }

    // random position in the enemy spawn zone
    Vector3 GenerateSpawnPosition()
    {
        float xPos = Random.Range(-spawnRangeX, spawnRangeX);
        float zPos = Random.Range(spawnZMin, spawnZMax);
        return new Vector3(xPos, 0, zPos);
    }

    void SpawnEnemyWave(int enemiesToSpawn)
    {
        Vector3 powerupSpawnOffset = new Vector3(0, 0, -15); // push powerup toward player side

        // spawn a powerup if none exist on the field, alternating type each wave
        bool hasRegularPowerup = GameObject.FindGameObjectsWithTag("KnockbackPowerup").Length > 0;
        bool hasSmashPowerup = GameObject.FindGameObjectsWithTag("SmashPowerup").Length > 0;

        if (!hasRegularPowerup && !hasSmashPowerup)
        {
            if (spawnSmashNext && smashPowerupPrefab != null)
            {
                Instantiate(smashPowerupPrefab, GenerateSpawnPosition() + powerupSpawnOffset, smashPowerupPrefab.transform.rotation);
            }
            else
            {
                Instantiate(powerupPrefab, GenerateSpawnPosition() + powerupSpawnOffset, powerupPrefab.transform.rotation);
            }
            spawnSmashNext = !spawnSmashNext;
        }

        // spawn enemies based on wave number
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            Instantiate(enemyPrefab, GenerateSpawnPosition(), enemyPrefab.transform.rotation);
        }

        waveCount++;
        ResetPlayerPosition();
    }

    // put player back at start position
    void ResetPlayerPosition()
    {
        player.transform.position = new Vector3(0, 1, -7);
        player.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        player.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
    }
}
