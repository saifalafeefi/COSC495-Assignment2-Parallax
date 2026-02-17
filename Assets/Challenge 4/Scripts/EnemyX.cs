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

    // call this from anywhere to stun the enemy (resets timer if already stunned)
    public void Stun()
    {
        isStunned = true;
        stunTimer = stunDuration;
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
    }

    // blue sphere gizmo when stunned (only visible with Gizmos enabled in Scene/Game view)
    void OnDrawGizmos()
    {
        if (isStunned)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
            Gizmos.DrawSphere(transform.position, 1.5f);
        }
    }
}
