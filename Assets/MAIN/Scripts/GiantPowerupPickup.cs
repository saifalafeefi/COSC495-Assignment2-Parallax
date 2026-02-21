using UnityEngine;

public class GiantPowerupPickup : MonoBehaviour
{
    public float giantScale = 2f;
    public float giantDuration = 10f;
    public float giantShrinkBackDuration = 3f;
    public float squishDuration = 1f;
    public float squishGroundOffset = 0.03f;
    public GameObject indicatorPrefab;
    public Vector3 stackMultiplier = Vector3.one;
}
