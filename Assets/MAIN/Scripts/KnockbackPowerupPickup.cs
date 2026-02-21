using UnityEngine;

public class KnockbackPowerupPickup : MonoBehaviour
{
    public int durationSeconds = 5;
    public float boostedKnockbackStrength = 25f;
    public GameObject indicatorPrefab;
    public Vector3 stackMultiplier = Vector3.one;
}
