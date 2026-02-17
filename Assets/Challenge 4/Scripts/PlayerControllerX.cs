using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerControllerX : MonoBehaviour
{
    private Rigidbody playerRb;
    public float speed = 15;
    public float maxSpeed = 20f; // top speed the player can reach
    private GameObject focalPoint;
    public float brakingFactor = 5f; // how fast you stop when changing direction

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

    // smash aiming system
    public GameObject landingIndicator;
    public float slowMotionScale = 0.15f;
    public float diveSpeed = 40f;
    public float diveAcceleration = 60f; // how much faster the dive gets per second
    public float launchDuration = 0.5f;
    public Vector3 smashCameraOffset = new Vector3(1f, 2f, -2f);
    private RotateCameraX cameraRotator;
    private CameraShoulderShift shoulderShift;
    private CinemachineBrain cinemachineBrain;
    private Vector3 landingTarget;
    private bool isAiming;
    private bool isDiving;
    private bool diveCollided;
    private bool diveRequested;
    private float originalFixedDeltaTime;
    private LineRenderer aimLine;

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

        // smash aiming setup
        cameraRotator = focalPoint.GetComponent<RotateCameraX>();
        shoulderShift = FindObjectOfType<CameraShoulderShift>();
        cinemachineBrain = FindObjectOfType<CinemachineBrain>();
        originalFixedDeltaTime = Time.fixedDeltaTime;
        if (landingIndicator != null)
        {
            landingIndicator.SetActive(false);
            // prevent the aim raycast from hitting the indicator itself
            landingIndicator.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        // create aim line renderer
        aimLine = gameObject.AddComponent<LineRenderer>();
        aimLine.startWidth = 0.1f;
        aimLine.endWidth = 0.1f;
        aimLine.material = new Material(Shader.Find("Sprites/Default"));
        aimLine.startColor = Color.red;
        aimLine.endColor = Color.red;
        aimLine.positionCount = 2;
        aimLine.enabled = false;
    }

    void Update()
    {
        // always check ground state
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.5f);

        // F key: during aiming phase → dive request; otherwise → start smash
        if (Input.GetKeyDown(KeyCode.F) && isAiming)
        {
            diveRequested = true;
        }
        else if (Input.GetKeyDown(KeyCode.F) && hasSmashPowerup && !isSmashing && isGrounded)
        {
            StartCoroutine(PerformSmashAttack());
        }

        // update landing indicator + aim line during aiming (follows camera direction)
        if (isAiming && landingIndicator != null)
        {
            Ray aimRay = new Ray(transform.position, focalPoint.transform.forward);
            if (Physics.Raycast(aimRay, out RaycastHit hit, 200f))
            {
                landingTarget = hit.point;
                landingIndicator.transform.position = hit.point + new Vector3(0, 0.15f, 0);
            }

            if (aimLine != null)
            {
                aimLine.SetPosition(0, transform.position);
                aimLine.SetPosition(1, landingIndicator.transform.position);
            }
        }

        // disable movement and turbo during smash
        if (!isSmashing)
        {
            // move player relative to camera direction (WASD)
            float verticalInput = Input.GetAxis("Vertical");
            float horizontalInput = Input.GetAxis("Horizontal");
            // flatten to horizontal so looking up/down doesn't launch the player
            Vector3 flatForward = new Vector3(focalPoint.transform.forward.x, 0, focalPoint.transform.forward.z).normalized;
            Vector3 flatRight = new Vector3(focalPoint.transform.right.x, 0, focalPoint.transform.right.z).normalized;
            Vector3 moveDirection = flatForward * verticalInput + flatRight * horizontalInput;

            // brake hard when changing direction, so movement feels snappy
            Vector3 flatVelocity = new Vector3(playerRb.linearVelocity.x, 0, playerRb.linearVelocity.z);
            if (moveDirection != Vector3.zero && Vector3.Dot(flatVelocity, moveDirection) < 0)
            {
                playerRb.linearVelocity = new Vector3(
                    playerRb.linearVelocity.x * (1f - brakingFactor * Time.deltaTime),
                    playerRb.linearVelocity.y,
                    playerRb.linearVelocity.z * (1f - brakingFactor * Time.deltaTime)
                );
            }

            // scale force down as you approach max speed, so acceleration feels gradual
            float currentSpeed = flatVelocity.magnitude;
            float speedFactor = Mathf.Clamp01(1f - currentSpeed / maxSpeed);
            playerRb.AddForce(moveDirection * speed * speedFactor, ForceMode.Force);

            // turbo boost on spacebar
            if (Input.GetKeyDown(KeyCode.Space))
            {
                playerRb.AddForce(focalPoint.transform.forward * turboStrength, ForceMode.Impulse);

                if (turboParticle != null)
                {
                    turboParticle.Play();
                }
            }
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

    // 5-phase smash: launch → auto-aim → slow-mo aiming → dive → impact
    IEnumerator PerformSmashAttack()
    {
        // --- Phase 1: Launch ---
        isSmashing = true;
        diveRequested = false;
        playerRb.AddForce(Vector3.up * smashJumpForce, ForceMode.Impulse);

        // wait for launch duration then transition to aiming
        yield return new WaitForSeconds(launchDuration);

        // --- Phase 2: Auto-aim + camera to right side (tweened together) ---
        Transform closestEnemy = FindClosestEnemy();
        float tweenTime = shoulderShift != null ? shoulderShift.tweenDuration : 0.4f;

        if (closestEnemy != null && cameraRotator != null)
        {
            EasingType easing = shoulderShift != null ? shoulderShift.tweenEasing : EasingType.EaseInOut;
            cameraRotator.TweenToPosition(closestEnemy.position, tweenTime, easing);
        }

        if (shoulderShift != null)
        {
            shoulderShift.ForceCameraSide(1f);
            shoulderShift.ForceShoulderOffset(smashCameraOffset);
        }

        // --- Phase 3: Slow-mo aiming ---
        // freeze player in the air
        playerRb.useGravity = false;
        playerRb.linearVelocity = Vector3.zero;

        // remove pitch clamp so player can aim straight down
        if (cameraRotator != null)
        {
            cameraRotator.RemovePitchClamp();
        }

        isAiming = true;
        if (landingIndicator != null)
        {
            landingIndicator.SetActive(true);
        }
        if (aimLine != null)
        {
            aimLine.enabled = true;
        }

        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = originalFixedDeltaTime * slowMotionScale;

        // wait for player to press F again
        while (!diveRequested)
        {
            yield return null;
        }

        // --- Phase 4: Dive (still in slow-mo) ---
        isAiming = false;

        // restore pitch clamp back to normal
        if (cameraRotator != null)
        {
            cameraRotator.RestorePitchClamp();
        }
        if (shoulderShift != null)
        {
            shoulderShift.ReleaseCameraSide();
            shoulderShift.ReleaseShoulderOffset();
        }

        playerRb.useGravity = true;
        if (landingIndicator != null)
        {
            landingIndicator.SetActive(false);
        }
        if (aimLine != null)
        {
            aimLine.enabled = false;
        }

        // tell cinemachine to ignore timeScale so camera keeps up during slow-mo dive
        if (cinemachineBrain != null) cinemachineBrain.IgnoreTimeScale = true;

        // dive until we hit something (ground, wall, enemy, anything)
        isDiving = true;
        diveCollided = false;
        float currentDiveSpeed = diveSpeed;
        while (!diveCollided)
        {
            if (transform.position.y < -5f)
            {
                break;
            }
            currentDiveSpeed += diveAcceleration * Time.fixedUnscaledDeltaTime;
            Vector3 diveDirection = (landingTarget - transform.position).normalized;
            // compensate for slow-mo so the player dives at full real-time speed
            float timeCompensation = Time.timeScale > 0 ? 1f / Time.timeScale : 1f;
            playerRb.linearVelocity = diveDirection * currentDiveSpeed * timeCompensation;
            yield return new WaitForFixedUpdate();
        }
        isDiving = false;

        // --- Phase 5: Impact — kill momentum, restore time, then smash ---
        playerRb.linearVelocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;
        if (cinemachineBrain != null) cinemachineBrain.IgnoreTimeScale = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = originalFixedDeltaTime;
        ApplySmashImpact();

        // one-time use, consume it
        hasSmashPowerup = false;
        if (smashPowerupIndicator != null)
        {
            smashPowerupIndicator.SetActive(false);
        }
        if (landingIndicator != null)
        {
            landingIndicator.SetActive(false);
        }
        if (aimLine != null)
        {
            aimLine.enabled = false;
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

    // find the closest enemy to the player (for auto-aim at apex)
    Transform FindClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform closest = null;
        float closestDist = Mathf.Infinity;

        foreach (GameObject enemy in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = enemy.transform;
            }
        }

        return closest;
    }

    // safety: restore timeScale and camera if player is destroyed mid-smash
    void OnDisable()
    {
        if (Time.timeScale != 1f)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = originalFixedDeltaTime;
        }
        if (shoulderShift != null)
        {
            shoulderShift.ReleaseCameraSide();
            shoulderShift.ReleaseShoulderOffset();
        }
        if (cameraRotator != null)
        {
            cameraRotator.RestorePitchClamp();
        }
    }

    // bump enemies away on contact
    private void OnCollisionEnter(Collision other)
    {
        // any collision during dive = landed
        if (isDiving)
        {
            diveCollided = true;
        }

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
