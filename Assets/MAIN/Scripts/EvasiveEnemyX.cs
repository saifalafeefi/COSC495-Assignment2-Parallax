using UnityEngine;

public class EvasiveEnemyX : EnemyX
{
    [Header("Evasive")]
    [SerializeField] private float dodgeInterval = 3f;
    [SerializeField] private float dodgeForce = 30f;
    [SerializeField] private float crawlSpeedMultiplier = 0.4f;
    private float dodgeTimer;

    protected override void OnStart()
    {
        // random offset so enemies don't all dodge in sync
        dodgeTimer = Random.Range(0f, dodgeInterval);
    }

    protected override void Move()
    {
        Vector3 goalDirection = (playerGoal.transform.position - transform.position).normalized;

        // crawl toward goal at reduced speed
        enemyRb.AddForce(goalDirection * speed * crawlSpeedMultiplier * Time.deltaTime);

        // dodge timer — impulse perpendicular to goal direction
        dodgeTimer -= Time.deltaTime;
        if (dodgeTimer <= 0f)
        {
            // perpendicular to goal direction on the XZ plane (random left or right)
            Vector3 dodgeDir = Vector3.Cross(goalDirection, Vector3.up);
            if (Random.value > 0.5f) dodgeDir = -dodgeDir;
            enemyRb.AddForce(dodgeDir * dodgeForce, ForceMode.Impulse);
            dodgeTimer = dodgeInterval;
        }
    }
}
