using UnityEngine;

public class SmashPowerupPickup : MonoBehaviour
{
    public float smashJumpForce = 15f;
    public float smashRadius = 15f;
    public float maxSmashForce = 50f;
    public float minSmashForce = 10f;
    public GameObject indicatorPrefab;
    public Vector3 stackMultiplier = Vector3.one;
}
