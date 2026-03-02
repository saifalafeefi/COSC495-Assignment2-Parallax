using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyX : MonoBehaviour
{
    [SerializeField] protected float speed = 10f;
    protected Rigidbody enemyRb;
    [SerializeField] protected GameObject playerGoal;

    // static enemy count — lets SpawnManagerX check without FindGameObjectsWithTag every frame
    public static int aliveCount;
    private static float globalSpeedMultiplier = 1f;

    // cached goal references — found once, reused by all enemies
    private static GameObject cachedPlayerGoal;
    protected static GameObject cachedEnemyGoal;

    // cached player transform — found once, reused by subclasses (e.g. aggressive ram)
    protected static Transform cachedPlayerTransform;

    // after getting hit, enemy drifts toward its own goal briefly
    [SerializeField] private float stunSpeed = 3f;
    [SerializeField] private float stunDuration = 1.5f;
    [SerializeField] private float postStunRecoverDuration = 0.55f; // blend time before full steering returns
    [SerializeField] private float overspeedBrake = 16f; // soft braking when above speed cap
    private bool isStunned;
    private float stunTimer;
    private float postStunRecoverTimer;

    // haunted enemies aggressively home toward their own goal
    private bool isHaunted;
    private float hauntTimer;
    private float hauntMoveSpeed;
    private static GameObject hauntVfxPrefab; // stored once from first Haunt() call, reused for spreading
    private GameObject hauntVfxInstance;       // active particle effect on this enemy

    // call this from anywhere to stun the enemy (resets timer if already stunned)
    public void Stun()
    {
        // haunt overrides stun — don't downgrade to weaker drift
        if (isHaunted) return;
        isStunned = true;
        stunTimer = stunDuration;
    }

    // haunted enemies lock onto their own goal hard and fast
    public void Haunt(float speed, float duration, GameObject vfxPrefab = null)
    {
        if (!isHaunted && SFXManager.Instance != null) SFXManager.Instance.PlayHauntApply();
        isHaunted = true;
        isStunned = false; // haunt replaces stun
        hauntMoveSpeed = speed;
        hauntTimer = duration;

        // store prefab once so spreading enemies can spawn VFX too
        if (vfxPrefab != null)
            hauntVfxPrefab = vfxPrefab;

        // spawn/respawn VFX on this enemy
        if (hauntVfxInstance != null) Destroy(hauntVfxInstance);
        if (hauntVfxPrefab != null)
        {
            hauntVfxInstance = Instantiate(hauntVfxPrefab, transform.position, Quaternion.identity, transform);
        }
    }

    bool startedTracking;

    void Start()
    {
        enemyRb = GetComponent<Rigidbody>();
        aliveCount++;
        startedTracking = true;

        // use cached static refs so only the first enemy pays the Find() cost
        if (cachedPlayerGoal == null)
            cachedPlayerGoal = GameObject.Find("Player Goal");
        if (cachedEnemyGoal == null)
            cachedEnemyGoal = GameObject.Find("Enemy Goal");
        if (cachedPlayerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                cachedPlayerTransform = playerObj.transform;
        }

        if (playerGoal == null)
            playerGoal = cachedPlayerGoal;

        OnStart();
    }

    // override in subclasses for type-specific init (e.g. random timer offsets)
    protected virtual void OnStart() { }

    void Update()
    {
        if (playerGoal == null) return;

        // tick timers in Update (time-accurate), forces applied in FixedUpdate
        if (isHaunted)
        {
            hauntTimer -= Time.deltaTime;
            if (hauntTimer <= 0f)
            {
                isHaunted = false;
                if (hauntVfxInstance != null) Destroy(hauntVfxInstance);
            }
            return;
        }

        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                postStunRecoverTimer = postStunRecoverDuration;
            }
            return;
        }

        if (postStunRecoverTimer > 0f)
            postStunRecoverTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (playerGoal == null) return;

        if (isHaunted)
        {
            if (cachedEnemyGoal != null)
            {
                Vector3 toGoal = (cachedEnemyGoal.transform.position - transform.position).normalized;
                enemyRb.AddForce(toGoal * hauntMoveSpeed * globalSpeedMultiplier, ForceMode.Force);
            }
            return;
        }

        if (isStunned)
        {
            if (cachedEnemyGoal != null)
            {
                Vector3 retreatDirection = (cachedEnemyGoal.transform.position - transform.position).normalized;
                enemyRb.AddForce(retreatDirection * stunSpeed * globalSpeedMultiplier);
            }
            return;
        }

        Move();
    }

    protected float GetPostStunForceScale()
    {
        if (postStunRecoverDuration <= 0f) return 1f;
        return postStunRecoverTimer > 0f
            ? 1f - (postStunRecoverTimer / postStunRecoverDuration)
            : 1f;
    }

    // override in subclasses for type-specific movement
    protected virtual void Move()
    {
        Vector3 toGoal = playerGoal.transform.position - transform.position;
        toGoal.y = 0f;
        Vector3 goalDirection = toGoal.normalized;
        enemyRb.AddForce(goalDirection * GetEffectiveSpeed(speed) * GetPostStunForceScale(), ForceMode.Force);
        ClampSpeed();
    }

    // soft cap horizontal speed to avoid hard velocity snaps
    protected void ClampSpeed()
    {
        Vector3 vel = enemyRb.linearVelocity;
        Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);
        float flatSpeed = flatVel.magnitude;
        float speedCap = GetEffectiveSpeed(speed);
        if (flatSpeed <= speedCap) return;

        float excess = flatSpeed - speedCap;
        Vector3 brakeDir = -flatVel.normalized;
        enemyRb.AddForce(brakeDir * excess * overspeedBrake, ForceMode.Acceleration);
    }

    protected float GetEffectiveSpeed(float baseSpeed)
    {
        return baseSpeed * globalSpeedMultiplier;
    }

    protected float GetGlobalSpeedMultiplier()
    {
        return globalSpeedMultiplier;
    }

    public static void SetGlobalSpeedMultiplier(float multiplier)
    {
        globalSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    public static float GlobalSpeedMultiplier => globalSpeedMultiplier;

    // trigger-based goal scoring (goals use isTrigger colliders)
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "Enemy Goal")
        {
            if (GameManagerX.Instance != null)
                GameManagerX.Instance.EnemyScored(this);
            Destroy(gameObject);
        }
        else if (other.gameObject.name == "Player Goal")
        {
            if (GameManagerX.Instance != null)
                GameManagerX.Instance.EnemyReachedPlayerGoal();
            Destroy(gameObject);
        }
    }

    protected virtual void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Stun();
        }
        // spread haunt to normal enemies on contact (shared remaining duration)
        else if (isHaunted && other.gameObject.CompareTag("Enemy"))
        {
            EnemyX otherEnemy = other.gameObject.GetComponent<EnemyX>();
            if (otherEnemy != null && !otherEnemy.isHaunted)
            {
                otherEnemy.Haunt(hauntMoveSpeed, hauntTimer);
            }
        }
    }

    void OnDestroy()
    {
        // only decrement if Start() actually ran (prevents going negative from tutorial previews)
        if (startedTracking) aliveCount--;
        if (hauntVfxInstance != null) Destroy(hauntVfxInstance);
    }

    // reset statics between scenes so stale refs don't carry over
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        aliveCount = 0;
        globalSpeedMultiplier = 1f;
        cachedPlayerGoal = null;
        cachedEnemyGoal = null;
        cachedPlayerTransform = null;
        hauntVfxPrefab = null;
    }

    // colored sphere gizmo for status (only visible with Gizmos enabled in Scene/Game view)
    void OnDrawGizmos()
    {
        if (isHaunted)
        {
            // purple when haunted
            Gizmos.color = new Color(0.6f, 0f, 1f, 0.5f);
            Gizmos.DrawSphere(transform.position, 1.5f);
        }
        else if (isStunned)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
            Gizmos.DrawSphere(transform.position, 1.5f);
        }
    }
}
