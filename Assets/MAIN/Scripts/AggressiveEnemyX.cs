using UnityEngine;

public class AggressiveEnemyX : EnemyX
{
    [Header("Aggressive")]
    [SerializeField] private float aggroDetectRadius = 8f;
    [SerializeField] private float aggroHomingForce = 80f;    // force toward player when in range
    [SerializeField] private float aggroHomingSpeed = 15f;    // max speed while homing toward player
    [SerializeField] private float aggroHitKnockback = 25f;   // impulse applied to player on contact
    [SerializeField] private float aggroAcceleration = 100f;
    [SerializeField] private float aggroCooldown = 3f;        // cooldown after hitting player before homing again
    private float aggroCooldownTimer;
    private bool isHoming;

    protected override void Move()
    {
        Vector3 toGoal = playerGoal.transform.position - transform.position;
        toGoal.y = 0f;
        Vector3 goalDirection = toGoal.normalized;
        float forceScale = GetPostStunForceScale();

        aggroCooldownTimer -= Time.deltaTime;

        // check if player is in range and not on cooldown
        bool playerInRange = false;
        if (aggroCooldownTimer <= 0f && cachedPlayerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, cachedPlayerTransform.position);
            playerInRange = dist <= aggroDetectRadius;
        }

        if (playerInRange)
        {
            // home toward player
            isHoming = true;
            Vector3 toPlayer = cachedPlayerTransform.position - transform.position;
            toPlayer.y = 0f;
            enemyRb.AddForce(toPlayer.normalized * aggroHomingForce * forceScale, ForceMode.Force);
        }
        else
        {
            isHoming = false;
            // head toward goal normally
            enemyRb.AddForce(goalDirection * aggroAcceleration * forceScale, ForceMode.Force);
        }

        // use aggro speed cap when homing, normal speed otherwise
        float originalSpeed = speed;
        if (isHoming) speed = aggroHomingSpeed;
        ClampSpeed();
        speed = originalSpeed;
    }

    protected override void OnCollisionEnter(Collision other)
    {
        // always run base collision (stun, haunt spread, etc.)
        base.OnCollisionEnter(other);

        if (!other.gameObject.CompareTag("Player")) return;
        if (!isHoming) return;

        // strong knockback on the player
        Rigidbody playerRb = other.gameObject.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            Vector3 knockDir = (other.gameObject.transform.position - transform.position).normalized;
            playerRb.AddForce(knockDir * aggroHitKnockback, ForceMode.Impulse);
        }

        // go on cooldown, stop homing
        aggroCooldownTimer = aggroCooldown;
        isHoming = false;
    }

    // aggressive detect radius gizmo (only when selected in Scene view)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, aggroDetectRadius);
    }
}
