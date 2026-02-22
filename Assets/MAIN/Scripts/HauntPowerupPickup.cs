using UnityEngine;

public class HauntPowerupPickup : MonoBehaviour
{
    public float hauntDuration = 8f;          // how long the player buff lasts
    public float hauntSpeed = 15f;            // how fast haunted enemies home toward their goal
    public float hauntEffectDuration = 5f;    // how long the haunt effect lasts on each enemy
    public GameObject indicatorPrefab;
    public Vector3 stackMultiplier = Vector3.one;
}
