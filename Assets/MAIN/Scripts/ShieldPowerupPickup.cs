using UnityEngine;

public class ShieldPowerupPickup : MonoBehaviour
{
    public float shieldRadius = 4f;
    public int shieldMaxHits = 3;
    public float shieldShrinkDuration = 0.4f;
    public GameObject indicatorPrefab;
    public Vector3 stackMultiplier = new Vector3(0.3f, 0.3f, 0.3f);
}
