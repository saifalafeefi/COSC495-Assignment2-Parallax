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
    public float gravityMultiplier = 2.5f; // extra gravity so the ball doesn't feel floaty

    public int knockbackStacks;
    public GameObject powerupIndicator;
    public int powerUpDuration = 5;
    public float turboStrength = 15.0f;
    public ParticleSystem turboParticle;

    private float normalStrength = 10; // normal knockback
    private float powerupStrength = 25; // boosted knockback
    private Coroutine knockbackCoroutine; // tracked so we can reset the timer on re-pickup
    public Vector3 knockbackStackMultiplier = Vector3.one; // scales stackSpacing for this powerup

    // smash powerup
    public int smashPowerupStacks;
    private bool isSmashing;
    private bool isGrounded;
    public GameObject smashPowerupIndicator;
    public float smashJumpForce = 15f;
    public float smashRadius = 15f;
    public float maxSmashForce = 50f;
    public float minSmashForce = 10f;
    public Vector3 smashStackMultiplier = Vector3.one; // scales stackSpacing for this powerup

    // shield powerup
    public int shieldStacks;
    public GameObject shieldIndicator;
    public float shieldRadius = 4f;       // how far the shield reaches
    public int shieldMaxHits = 3;         // enemies destroyed before shield breaks
    public float shieldShrinkDuration = 0.4f; // how long enemies shrink before vanishing
    private int shieldHitsRemaining;
    public Vector3 shieldStackMultiplier = new Vector3(0.3f, 0.3f, 0.3f); // scales stackSpacing for this powerup

    // giant powerup
    public int giantStacks;
    public GameObject giantPowerupIndicator;
    public float giantScale = 2f;              // how big the player gets
    public float giantDuration = 10f;          // how long the player stays giant
    public float giantShrinkBackDuration = 3f; // how long shrink-back takes
    public float squishDuration = 1f;          // how long enemies flatten
    public float squishGroundOffset = 0.03f;   // vertical offset above detected ground for squished enemies
    public Vector3 giantStackMultiplier = Vector3.one;
    private Vector3 originalPlayerScale;
    private bool isGiant;
    private bool isShrinkingBack;
    private Vector3 shrinkBackStartScale;
    private float shrinkBackElapsed;
    private Coroutine giantCoroutine;

    // stacking visuals — each stack spawns a slightly larger copy of the indicator
    // X/Z = scale growth per stack, Y = vertical position offset per stack
    public Vector3 stackSpacing = new Vector3(0.3f, 0.15f, 0.3f);
    private List<GameObject> knockbackIndicators = new List<GameObject>();
    private List<GameObject> smashIndicators = new List<GameObject>();
    private List<GameObject> shieldIndicators = new List<GameObject>();
    private List<GameObject> giantIndicators = new List<GameObject>();
    private Vector3 knockbackIndicatorBaseScale;
    private Vector3 smashIndicatorBaseScale;
    private Vector3 shieldIndicatorBaseScale;
    private Vector3 giantIndicatorBaseScale;

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

        // save base scales for cloning extra stacks later
        if (powerupIndicator != null)
            knockbackIndicatorBaseScale = powerupIndicator.transform.lossyScale;
        if (smashPowerupIndicator != null)
            smashIndicatorBaseScale = smashPowerupIndicator.transform.lossyScale;
        if (shieldIndicator != null)
            shieldIndicatorBaseScale = new Vector3(shieldRadius * 2f, shieldRadius * 2f, shieldRadius * 2f);
        if (giantPowerupIndicator != null)
            giantIndicatorBaseScale = giantPowerupIndicator.transform.lossyScale;

        // save original player scale for giant powerup
        originalPlayerScale = transform.localScale;

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

    void FixedUpdate()
    {
        // apply extra gravity so the ball falls snappier
        if (playerRb.useGravity && !isGrounded)
        {
            playerRb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
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
        else if (Input.GetKeyDown(KeyCode.F) && smashPowerupStacks > 0 && !isSmashing)
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

        // giant shrink-back: lerp player scale back to normal
        if (isShrinkingBack)
        {
            shrinkBackElapsed += Time.deltaTime;
            float t = shrinkBackElapsed / giantShrinkBackDuration;
            transform.localScale = Vector3.Lerp(shrinkBackStartScale, originalPlayerScale, t);
            if (t >= 1f)
            {
                transform.localScale = originalPlayerScale;
                isShrinkingBack = false;
                isGiant = false;
            }
        }

        // position originals + extra clones under the player (Y = vertical offset per stack)
        Vector3 indicatorPos = transform.position + new Vector3(0, -0.6f, 0);

        if (powerupIndicator != null)
            powerupIndicator.transform.position = indicatorPos;
        for (int i = 0; i < knockbackIndicators.Count; i++)
            if (knockbackIndicators[i] != null)
                knockbackIndicators[i].transform.position = indicatorPos + new Vector3(0, (i + 1) * stackSpacing.y * knockbackStackMultiplier.y, 0);

        if (smashPowerupIndicator != null)
            smashPowerupIndicator.transform.position = indicatorPos;
        for (int i = 0; i < smashIndicators.Count; i++)
            if (smashIndicators[i] != null)
                smashIndicators[i].transform.position = indicatorPos + new Vector3(0, (i + 1) * stackSpacing.y * smashStackMultiplier.y, 0);

        if (shieldIndicator != null)
        {
            shieldIndicator.transform.position = transform.position;
            float diameter = shieldRadius * 2f;
            shieldIndicator.transform.localScale = new Vector3(diameter, diameter, diameter);
        }
        for (int i = 0; i < shieldIndicators.Count; i++)
            if (shieldIndicators[i] != null)
                shieldIndicators[i].transform.position = transform.position + new Vector3(0, (i + 1) * stackSpacing.y * shieldStackMultiplier.y, 0);

        if (giantPowerupIndicator != null)
            giantPowerupIndicator.transform.position = indicatorPos;
        for (int i = 0; i < giantIndicators.Count; i++)
            if (giantIndicators[i] != null)
                giantIndicators[i].transform.position = indicatorPos + new Vector3(0, (i + 1) * stackSpacing.y * giantStackMultiplier.y, 0);

        // giant: squish enemies on contact (overlap, not collision, so no physics push)
        if (isGiant)
        {
            float giantRadius = transform.localScale.x * 0.5f + 1f; // catch enemies just before they physically collide
            Collider[] giantHits = Physics.OverlapSphere(transform.position, giantRadius);
            foreach (Collider c in giantHits)
            {
                if (c.CompareTag("Enemy"))
                {
                    if (GameManagerX.Instance != null)
                        GameManagerX.Instance.EnemyScored();

                    c.gameObject.tag = "Untagged";
                    EnemyX enemyAI = c.GetComponent<EnemyX>();
                    if (enemyAI != null) enemyAI.enabled = false;
                    c.enabled = false;
                    Rigidbody eRb = c.GetComponent<Rigidbody>();
                    if (eRb != null)
                    {
                        eRb.linearVelocity = Vector3.zero;
                        eRb.angularVelocity = Vector3.zero;
                        eRb.useGravity = false;
                        eRb.isKinematic = true;
                    }
                    StartCoroutine(SquishAndDestroy(c.gameObject));
                }
            }
        }

        // shield: vaporize enemies that get too close
        if (shieldStacks > 0)
        {
            Collider[] nearby = Physics.OverlapSphere(transform.position, shieldRadius);
            foreach (Collider col in nearby)
            {
                if (col.CompareTag("Enemy"))
                {
                    if (GameManagerX.Instance != null)
                    {
                        GameManagerX.Instance.EnemyScored();
                    }

                    // untag so it won't be detected again, disable AI, then shrink away
                    col.gameObject.tag = "Untagged";
                    EnemyX enemyAI = col.GetComponent<EnemyX>();
                    if (enemyAI != null) enemyAI.enabled = false;
                    Rigidbody enemyRb = col.GetComponent<Rigidbody>();
                    if (enemyRb != null) enemyRb.isKinematic = true;
                    StartCoroutine(ShrinkAndDestroy(col.gameObject, shieldShrinkDuration));

                    shieldHitsRemaining--;
                    if (shieldHitsRemaining <= 0)
                    {
                        shieldStacks--;
                        if (shieldStacks <= 0)
                        {
                            shieldStacks = 0;
                            ClearExtraIndicators(shieldIndicators);
                            if (shieldIndicator != null) shieldIndicator.SetActive(false);
                            break;
                        }
                        else
                        {
                            RemoveExtraIndicator(shieldIndicators);
                            shieldHitsRemaining = shieldMaxHits;
                        }
                    }
                }
            }
        }
    }

    // pick up powerups on contact — all powerups stack
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("KnockbackPowerup"))
        {
            Destroy(other.gameObject);
            knockbackStacks++;

            if (knockbackStacks == 1)
                powerupIndicator.SetActive(true);   // first stack = show original
            else
                SpawnExtraIndicator(powerupIndicator, knockbackIndicatorBaseScale, knockbackIndicators, false, knockbackStackMultiplier);

            // restart the timer on each pickup
            if (knockbackCoroutine != null)
                StopCoroutine(knockbackCoroutine);
            knockbackCoroutine = StartCoroutine(PowerupCooldown());
        }

        if (other.gameObject.CompareTag("SmashPowerup"))
        {
            Destroy(other.gameObject);
            smashPowerupStacks++;

            if (smashPowerupStacks == 1)
            {
                if (smashPowerupIndicator != null) smashPowerupIndicator.SetActive(true);
            }
            else
                SpawnExtraIndicator(smashPowerupIndicator, smashIndicatorBaseScale, smashIndicators, false, smashStackMultiplier);
        }

        if (other.gameObject.CompareTag("ShieldPowerup"))
        {
            Destroy(other.gameObject);
            shieldStacks++;
            if (shieldStacks == 1 || shieldHitsRemaining <= 0)
                shieldHitsRemaining = shieldMaxHits;

            if (shieldStacks == 1)
            {
                if (shieldIndicator != null) shieldIndicator.SetActive(true);
            }
            else
                SpawnExtraIndicator(shieldIndicator, shieldIndicatorBaseScale, shieldIndicators, true, shieldStackMultiplier);
        }

        if (other.gameObject.CompareTag("GiantPowerup"))
        {
            Destroy(other.gameObject);
            giantStacks++;

            if (giantStacks == 1)
            {
                if (giantPowerupIndicator != null) giantPowerupIndicator.SetActive(true);
            }
            else
                SpawnExtraIndicator(giantPowerupIndicator, giantIndicatorBaseScale, giantIndicators, false, giantStackMultiplier);

            // grow instantly, reset duration timer
            transform.localScale = originalPlayerScale * giantScale;
            isGiant = true;
            isShrinkingBack = false;

            if (giantCoroutine != null)
                StopCoroutine(giantCoroutine);
            giantCoroutine = StartCoroutine(GiantCooldown());
        }
    }

    // spawn an extra indicator clone for stacks beyond the first (bigger each time)
    void SpawnExtraIndicator(GameObject template, Vector3 baseWorldScale, List<GameObject> list, bool scaleAllAxes, Vector3 multiplier)
    {
        if (template == null) return;

        GameObject copy = Instantiate(template, template.transform.position, template.transform.rotation, template.transform.parent);
        copy.SetActive(true);

        // apply per-powerup multiplier to the shared stackSpacing
        int step = list.Count + 1;
        float scaleX = step * stackSpacing.x * multiplier.x;
        float scaleZ = step * stackSpacing.z * multiplier.z;
        if (scaleAllAxes)
        {
            float scaleY = step * stackSpacing.x * multiplier.y;
            copy.transform.localScale = template.transform.localScale + new Vector3(scaleX, scaleY, scaleZ);
        }
        else
            copy.transform.localScale = template.transform.localScale + new Vector3(scaleX, 0, scaleZ);

        list.Add(copy);
    }

    // remove the largest (last) extra indicator clone
    void RemoveExtraIndicator(List<GameObject> list)
    {
        if (list.Count == 0) return;
        int last = list.Count - 1;
        if (list[last] != null) Destroy(list[last]);
        list.RemoveAt(last);
    }

    // destroy all extra indicator clones
    void ClearExtraIndicators(List<GameObject> list)
    {
        foreach (GameObject ind in list)
            if (ind != null) Destroy(ind);
        list.Clear();
    }

    // knockback wears off one stack at a time
    IEnumerator PowerupCooldown()
    {
        yield return new WaitForSeconds(powerUpDuration);
        knockbackStacks--;
        if (knockbackStacks <= 0)
        {
            knockbackStacks = 0;
            ClearExtraIndicators(knockbackIndicators);
            if (powerupIndicator != null) powerupIndicator.SetActive(false);
        }
        else
        {
            RemoveExtraIndicator(knockbackIndicators);
        }
    }

    // giant duration expires, start shrinking back to normal
    IEnumerator GiantCooldown()
    {
        yield return new WaitForSeconds(giantDuration);

        giantStacks--;
        if (giantStacks <= 0)
        {
            giantStacks = 0;
            ClearExtraIndicators(giantIndicators);
            if (giantPowerupIndicator != null) giantPowerupIndicator.SetActive(false);
        }
        else
        {
            RemoveExtraIndicator(giantIndicators);
        }

        // begin shrink-back lerp (handled in Update)
        shrinkBackStartScale = transform.localScale;
        shrinkBackElapsed = 0f;
        isShrinkingBack = true;
    }

    // find the ground directly below a squished enemy so flattening always lands on the floor
    float GetSquishGroundY(GameObject target)
    {
        if (target == null) return 0f;

        Vector3 pos = target.transform.position;
        Vector3 rayOrigin = pos + Vector3.up * 2f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null)
            return targetCollider.bounds.min.y;

        return pos.y;
    }

    // sink enemy into the ground and shrink it away
    IEnumerator SquishAndDestroy(GameObject target)
    {
        if (target == null) yield break;

        // parent at ground height so flattened enemy always ends on the floor
        float groundY = GetSquishGroundY(target);
        Vector3 targetPos = target.transform.position;
        GameObject squishParent = new GameObject("SquishParent");
        squishParent.transform.position = new Vector3(targetPos.x, groundY + squishGroundOffset, targetPos.z);
        squishParent.transform.rotation = Quaternion.identity;
        target.transform.SetParent(squishParent.transform, true);

        // instant flatten (no tweening)
        squishParent.transform.localScale = new Vector3(1f, 0f, 1f);
        yield return new WaitForSeconds(Mathf.Max(0f, squishDuration));
        if (squishParent != null) Destroy(squishParent);
    }

    // shrink enemy down to nothing then destroy it
    IEnumerator ShrinkAndDestroy(GameObject target, float duration)
    {
        Vector3 startScale = target.transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            target.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        if (target != null) Destroy(target);
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

        // consume one stack
        smashPowerupStacks--;
        if (smashPowerupStacks <= 0)
        {
            smashPowerupStacks = 0;
            ClearExtraIndicators(smashIndicators);
            if (smashPowerupIndicator != null) smashPowerupIndicator.SetActive(false);
        }
        else
        {
            RemoveExtraIndicator(smashIndicators);
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

                    // stun so they drift toward their own goal
                    EnemyX enemy = col.GetComponent<EnemyX>();
                    if (enemy != null) enemy.Stun();
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
            // giant handles enemies via OverlapSphere, skip knockback
            if (isGiant) return;

            Rigidbody enemyRigidbody = other.gameObject.GetComponent<Rigidbody>();
            Vector3 awayFromPlayer = (other.gameObject.transform.position - transform.position).normalized;

            if (knockbackStacks > 0)
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
