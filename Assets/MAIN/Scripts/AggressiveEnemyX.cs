using UnityEngine;

public class AggressiveEnemyX : EnemyX
{
    [Header("Aggressive")]
    [SerializeField] private float aggroDetectRadius = 8f;
    [SerializeField] private float aggroRamForce = 20f;
    [SerializeField] private float aggroRamCooldown = 2f;
    [SerializeField] private float aggroAcceleration = 100f;
    private float aggroRamTimer;

    protected override void Move()
    {
        Vector3 goalDirection = (playerGoal.transform.position - transform.position).normalized;

        // strong force toward goal, clamped to top speed
        enemyRb.AddForce(goalDirection * aggroAcceleration * Time.deltaTime, ForceMode.Force);
        if (enemyRb.linearVelocity.magnitude > speed)
            enemyRb.linearVelocity = enemyRb.linearVelocity.normalized * speed;

        // ram check — impulse toward player if within range and cooldown expired
        aggroRamTimer -= Time.deltaTime;
        if (aggroRamTimer <= 0f && cachedPlayerTransform != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, cachedPlayerTransform.position);
            if (distToPlayer <= aggroDetectRadius)
            {
                Vector3 toPlayer = (cachedPlayerTransform.position - transform.position).normalized;
                enemyRb.AddForce(toPlayer * aggroRamForce, ForceMode.Impulse);
                aggroRamTimer = aggroRamCooldown;
            }
        }
    }

    // aggressive detect radius gizmo (only when selected in Scene view)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, aggroDetectRadius);
    }
}
