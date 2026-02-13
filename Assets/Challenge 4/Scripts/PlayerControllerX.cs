using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControllerX : MonoBehaviour
{
    private Rigidbody playerRb;
    private float speed = 500;
    private GameObject focalPoint;

    public bool hasPowerup;
    public GameObject powerupIndicator;
    public int powerUpDuration = 5;
    public float turboStrength = 15.0f;
    public ParticleSystem turboParticle;

    private float normalStrength = 10; // normal knockback
    private float powerupStrength = 25; // boosted knockback
    private Coroutine knockbackCoroutine; // tracked so we can reset the timer on re-pickup

    // smash powerup
    public bool hasSmashPowerup;
    private bool isSmashing;
    private bool isGrounded;
    public GameObject smashPowerupIndicator;
    public float smashJumpForce = 15f;
    public float smashRadius = 15f;
    public float maxSmashForce = 50f;
    public float minSmashForce = 10f;

    void Start()
    {
        playerRb = GetComponent<Rigidbody>();
        focalPoint = GameObject.Find("Focal Point");

        // fallback: grab particle from focal point if not assigned
        if (turboParticle == null)
        {
            turboParticle = focalPoint.GetComponentInChildren<ParticleSystem>(true);
        }

        if (turboParticle != null)
        {
            turboParticle.Stop();
        }
    }

    void Update()
    {
        // move player relative to camera direction (WASD)
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");
        Vector3 moveDirection = focalPoint.transform.forward * verticalInput + focalPoint.transform.right * horizontalInput;
        playerRb.AddForce(moveDirection * speed * Time.deltaTime);

        // turbo boost on spacebar
        if (Input.GetKeyDown(KeyCode.Space))
        {
            playerRb.AddForce(focalPoint.transform.forward * turboStrength, ForceMode.Impulse);

            if (turboParticle != null)
            {
                turboParticle.Play();
            }
        }

        // check if player is on the ground
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.5f);

        // press F to smash (only if grounded, has powerup, and not mid-smash)
        if (Input.GetKeyDown(KeyCode.F) && hasSmashPowerup && !isSmashing && isGrounded)
        {
            StartCoroutine(PerformSmashAttack());
        }

        // keep indicators under the player
        powerupIndicator.transform.position = transform.position + new Vector3(0, -0.6f, 0);

        if (smashPowerupIndicator != null)
        {
            smashPowerupIndicator.transform.position = transform.position + new Vector3(0, -0.6f, 0);
        }
    }

    // pick up powerups on contact
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("KnockbackPowerup"))
        {
            Destroy(other.gameObject);
            hasPowerup = true;
            powerupIndicator.SetActive(true);

            // cancel old timer if picking up another knockback while one is active
            if (knockbackCoroutine != null)
            {
                StopCoroutine(knockbackCoroutine);
            }
            knockbackCoroutine = StartCoroutine(PowerupCooldown());
        }

        if (other.gameObject.CompareTag("SmashPowerup"))
        {
            Destroy(other.gameObject);
            hasSmashPowerup = true;
            if (smashPowerupIndicator != null)
            {
                smashPowerupIndicator.SetActive(true);
            }
        }
    }

    // regular powerup wears off after a few seconds
    IEnumerator PowerupCooldown()
    {
        yield return new WaitForSeconds(powerUpDuration);
        hasPowerup = false;
        powerupIndicator.SetActive(false);
    }

    // jump up, wait to land, then slam enemies away
    IEnumerator PerformSmashAttack()
    {
        isSmashing = true;

        // launch into the air
        playerRb.AddForce(Vector3.up * smashJumpForce, ForceMode.Impulse);

        // brief pause to reach the top
        yield return new WaitForSeconds(0.3f);

        // wait until we hit the ground
        while (!isGrounded)
        {
            yield return null;
        }

        // slam down and blast enemies
        ApplySmashImpact();

        // one-time use, consume it
        hasSmashPowerup = false;
        if (smashPowerupIndicator != null)
        {
            smashPowerupIndicator.SetActive(false);
        }

        isSmashing = false;
    }

    // find all enemies in range and push them away — closer = harder hit
    void ApplySmashImpact()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, smashRadius);

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Enemy"))
            {
                Rigidbody enemyRb = col.GetComponent<Rigidbody>();
                if (enemyRb != null)
                {
                    Vector3 awayFromPlayer = (col.transform.position - transform.position);
                    float distance = awayFromPlayer.magnitude;
                    awayFromPlayer.Normalize();

                    // closer enemies get launched harder (linear falloff)
                    float forceMagnitude = Mathf.Lerp(maxSmashForce, minSmashForce, distance / smashRadius);

                    // add some upward pop so they fly, not just slide
                    Vector3 force = awayFromPlayer * forceMagnitude;
                    force.y = forceMagnitude * 0.5f;

                    enemyRb.AddForce(force, ForceMode.Impulse);
                }
            }
        }
    }

    // bump enemies away on contact
    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Enemy"))
        {
            Rigidbody enemyRigidbody = other.gameObject.GetComponent<Rigidbody>();
            Vector3 awayFromPlayer = (other.gameObject.transform.position - transform.position).normalized;

            if (hasPowerup)
            {
                enemyRigidbody.AddForce(awayFromPlayer * powerupStrength, ForceMode.Impulse);
            }
            else
            {
                enemyRigidbody.AddForce(awayFromPlayer * normalStrength, ForceMode.Impulse);
            }
        }
    }
}
