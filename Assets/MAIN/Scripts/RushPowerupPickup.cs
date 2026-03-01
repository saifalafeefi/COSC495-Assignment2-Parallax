using UnityEngine;

public class RushPowerupPickup : MonoBehaviour
{
    [Header("Duration")]
    [Min(0f)] public float rushDuration = 8f;

    [Header("Player Boost")]
    [Min(0f)] public float playerSpeedMultiplier = 1.6f;
    [Min(0f)] public float playerMaxSpeedMultiplier = 1.35f;

    [Header("Enemy Slow")]
    [Min(0f)] public float enemySpeedMultiplier = 0.5f;

    [Header("Indicator")]
    public GameObject indicatorPrefab;
    public Vector3 stackMultiplier = Vector3.one;
}
