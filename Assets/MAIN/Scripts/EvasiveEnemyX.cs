using UnityEngine;

public class EvasiveEnemyX : EnemyX
{
    [Header("Evasive")]
    [SerializeField] private float dodgeInterval = 3f;
    [SerializeField] private float dodgeDuration = 0.8f;  // how long each dodge lasts
    [SerializeField] private float dodgeStrength = 2f;      // speed multiplier during dodge (1 = normal, higher = faster burst)
    [SerializeField] private float dodgeAngle = 1f;        // how far sideways (0 = straight, 1 = 45°, higher = more sideways)
    private float dodgeTimer;
    private float dodgeActiveTimer;
    private int dodgeDirection; // -1 or 1

    protected override void OnStart()
    {
        // random offset so enemies don't all dodge in sync
        dodgeTimer = Random.Range(0f, dodgeInterval);
        dodgeDirection = Random.value > 0.5f ? 1 : -1;
    }

    protected override void Move()
    {
        Vector3 toGoal = playerGoal.transform.position - transform.position;
        toGoal.y = 0f; // keep movement on the ground plane
        Vector3 goalDirection = toGoal.normalized;
        Vector3 sideways = Vector3.Cross(goalDirection, Vector3.up);
        float forceScale = GetPostStunForceScale();

        // tick dodge cooldown
        dodgeTimer -= Time.deltaTime;
        if (dodgeTimer <= 0f)
        {
            dodgeActiveTimer = dodgeDuration;
            dodgeDirection = Random.value > 0.5f ? 1 : -1;
            dodgeTimer = dodgeInterval;
        }

        // while dodging, burst sideways at boosted speed
        if (dodgeActiveTimer > 0f)
        {
            dodgeActiveTimer -= Time.deltaTime;
            Vector3 dodgeDir = (goalDirection + sideways * dodgeDirection * dodgeAngle).normalized;
            enemyRb.AddForce(dodgeDir * speed * dodgeStrength * forceScale, ForceMode.Force);
        }
        else
        {
            enemyRb.AddForce(goalDirection * speed * forceScale, ForceMode.Force);
        }
        ClampSpeed();
    }
}
