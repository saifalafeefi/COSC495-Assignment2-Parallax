using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyX : MonoBehaviour
{
    public float speed;
    private Rigidbody enemyRb;
    public GameObject playerGoal;

    // Start is called before the first frame update
    void Start()
    {
        enemyRb = GetComponent<Rigidbody>();

        if (speed <= 0)
        {
            speed = 10f;
        }

        // Auto-assign Player Goal if it was not set in the Inspector.
        if (playerGoal == null)
        {
            playerGoal = GameObject.Find("Player Goal");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (playerGoal == null)
        {
            return;
        }

        // Set enemy direction towards player goal and move there
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
    }

}
