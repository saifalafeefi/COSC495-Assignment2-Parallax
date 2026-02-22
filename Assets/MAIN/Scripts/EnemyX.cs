using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyX : MonoBehaviour
{
    public float speed;
    private Rigidbody enemyRb;
    public GameObject playerGoal;

    // after getting hit, enemy drifts toward its own goal briefly
    public float stunSpeed = 3f;        // how hard it moves toward enemy goal while stunned
    public float stunDuration = 1.5f;   // how long the stun lasts
    private GameObject enemyGoal;
    private bool isStunned;
    private float stunTimer;

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

    void Start()
    {
        enemyRb = GetComponent<Rigidbody>();

        if (speed <= 0)
        {
            speed = 10f;
        }

        if (playerGoal == null)
        {
            playerGoal = GameObject.Find("Player Goal");
        }
        enemyGoal = GameObject.Find("Enemy Goal");
    }

    void Update()
    {
        if (playerGoal == null) return;

        // haunted: aggressively home toward enemy goal (much stronger than stun)
        if (isHaunted)
        {
            hauntTimer -= Time.deltaTime;
            if (hauntTimer <= 0f)
            {
                isHaunted = false;
                if (hauntVfxInstance != null) Destroy(hauntVfxInstance);
            }
            else if (enemyGoal != null)
            {
                Vector3 toGoal = (enemyGoal.transform.position - transform.position).normalized;
                enemyRb.AddForce(toGoal * hauntMoveSpeed, ForceMode.Force);
            }
            return;
        }

        if (isStunned)
        {
            // drift toward enemy goal while stunned
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
            }
            else if (enemyGoal != null)
            {
                Vector3 retreatDirection = (enemyGoal.transform.position - transform.position).normalized;
                enemyRb.AddForce(retreatDirection * stunSpeed * Time.deltaTime);
            }
            return;
        }

        // normal: move toward player goal
        Vector3 lookDirection = (playerGoal.transform.position - transform.position).normalized;
        enemyRb.AddForce(lookDirection * speed * Time.deltaTime);
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.name == "Enemy Goal")
        {
            // player scored — notify game manager
            if (GameManagerX.Instance != null)
            {
                GameManagerX.Instance.EnemyScored();
            }
            Destroy(gameObject);
        }
        else if (other.gameObject.name == "Player Goal")
        {
            // enemy got through — player loses a life
            if (GameManagerX.Instance != null)
            {
                GameManagerX.Instance.EnemyReachedPlayerGoal();
            }
            Destroy(gameObject);
        }
        else if (other.gameObject.CompareTag("Player"))
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
        if (hauntVfxInstance != null) Destroy(hauntVfxInstance);
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
