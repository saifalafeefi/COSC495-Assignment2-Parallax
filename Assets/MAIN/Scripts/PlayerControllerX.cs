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
    public float jumpForce = 8f; // upward impulse on jump
    public float groundCheckDistance = 1.5f; // raycast length for grounded check

    private int knockbackStacks;
    private GameObject powerupIndicator;
    private int powerUpDuration = 5;
    public float turboStrength = 15.0f;
    public ParticleSystem turboParticle;
    public bool enableTurboVfx = true;
    public int turboSmokeParticles = 8;
    public float turboSmokeMinSpeed = 1.5f;
    public float turboSmokeMaxSpeed = 3.5f;
    public float turboSmokeBackOffset = 0.5f;
    public float turboSmokeUpOffset = 0.15f;
    public float turboSmokeConeSpread = 0.35f;
    public float turboSmokeDuration = 0.6f;
    public float turboSmokeTickInterval = 0.06f;
    private Vector3 lastMoveDirection;
    public ParticleSystem smashImpactSmokePrefab;
    public bool enableSmashImpactSmoke = true;
    public int smashSmokeMinParticles = 14;
    public int smashSmokeMaxParticles = 26;
    public float smashSmokeMinSpeed = 2.5f;
    public float smashSmokeMaxSpeed = 7.5f;
    public float smashSmokeSurfaceOffset = 0.08f;
    public float smashSmokeCornerSpreadBoost = 0.35f;

    private float normalStrength = 10; // normal knockback
    private float powerupStrength = 25; // boosted knockback
    private Coroutine knockbackCoroutine; // tracked so we can reset the timer on re-pickup
    private Vector3 knockbackStackMultiplier = Vector3.one; // scales stackSpacing for this powerup

    // smash powerup
    private int smashPowerupStacks;
    private bool isSmashing;
    private bool isGrounded;
    private GameObject smashPowerupIndicator;
    private float smashJumpForce = 15f;
    private float smashRadius = 15f;
    private float maxSmashForce = 50f;
    private float minSmashForce = 10f;
    private Vector3 smashStackMultiplier = Vector3.one; // scales stackSpacing for this powerup

    // shield powerup
    private int shieldStacks;
    private GameObject shieldIndicator;
    private float shieldRadius = 4f;       // how far the shield reaches
    private int shieldMaxHits = 3;         // enemies destroyed before shield breaks
    private float shieldShrinkDuration = 0.4f; // how long enemies shrink before vanishing
    private int shieldHitsRemaining;
    private Vector3 shieldStackMultiplier = new Vector3(0.3f, 0.3f, 0.3f); // scales stackSpacing for this powerup

    // giant powerup
    private int giantStacks;
    private GameObject giantPowerupIndicator;
    private float giantScale = 2f;
    private float giantCameraDistance = 8f;      // camera distance while giant
    private float giantDuration = 10f;          // how long the player stays giant
    private float giantShrinkBackDuration = 3f; // how long shrink-back takes
    private float squishDuration = 1f;          // how long enemies flatten
    private float squishGroundOffset = 0.03f;   // vertical offset above detected ground for squished enemies
    private Vector3 giantStackMultiplier = Vector3.one;
    private Vector3 originalPlayerScale;
    private bool isGiant;
    private bool isShrinkingBack;
    private Vector3 shrinkBackStartScale;
    private float shrinkBackElapsed;
    private Coroutine giantCoroutine;

    // haunt powerup — touched enemies aggressively home toward their own goal
    private int hauntStacks;
    private float hauntDuration = 8f;
    private float hauntSpeed = 15f;
    private float hauntEffectDuration = 5f;
    private GameObject hauntIndicator;
    private Vector3 hauntStackMultiplier = Vector3.one;
    private Coroutine hauntCoroutine;
    private GameObject hauntEnemyVfxPrefab; // particle effect to spawn on haunted enemies

    // stacking visuals — each stack spawns a slightly larger copy of the indicator
    // X/Z = scale growth per stack, Y = vertical position offset per stack
    public Vector3 stackSpacing = new Vector3(0.3f, 0.15f, 0.3f);
    private List<GameObject> knockbackIndicators = new List<GameObject>();
    private List<GameObject> smashIndicators = new List<GameObject>();
    private List<GameObject> shieldIndicators = new List<GameObject>();
    private List<GameObject> giantIndicators = new List<GameObject>();
    private List<GameObject> hauntIndicators = new List<GameObject>();
    private Vector3 knockbackIndicatorBaseScale;
    private Vector3 smashIndicatorBaseScale;
    private Vector3 shieldIndicatorBaseScale;
    private Vector3 giantIndicatorBaseScale;
    private Vector3 hauntIndicatorBaseScale;

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
    private Vector3 lastSmashImpactPoint;
    private Vector3 lastSmashImpactNormal = Vector3.up;
    private float lastSmashImpactNormalVariance;
    private bool hasSmashImpactData;
    private float originalFixedDeltaTime;
    private LineRenderer aimLine;
    private Coroutine turboSmokeCoroutine;
    private ParticleSystem runtimeTurboSmokeFx;

    void Start()
    {
        playerRb = GetComponent<Rigidbody>();
        focalPoint = GameObject.Find("Focal Point");
        lastMoveDirection = transform.forward;
        lastMoveDirection.y = 0f;
        if (lastMoveDirection.sqrMagnitude < 0.001f) lastMoveDirection = Vector3.forward;
        lastMoveDirection.Normalize();

        // turbo vfx is opt-in to avoid accidentally playing unrelated particle systems
        if (enableTurboVfx && turboParticle != null)
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
        if (hauntIndicator != null)
            hauntIndicatorBaseScale = hauntIndicator.transform.lossyScale;

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
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance);

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
            if (moveDirection.sqrMagnitude > 0.001f)
            {
                moveDirection.Normalize();
                lastMoveDirection = moveDirection;
            }

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

            // jump on space while grounded
            if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            {
                playerRb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }

            // turbo boost on shift, along current movement direction (not camera forward)
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
            {
                Vector3 dashDirection = Vector3.zero;
                if (moveDirection.sqrMagnitude > 0.001f)
                    dashDirection = moveDirection;
                else if (flatVelocity.sqrMagnitude > 0.001f)
                    dashDirection = flatVelocity.normalized;
                else
                    dashDirection = lastMoveDirection;

                playerRb.AddForce(dashDirection * turboStrength, ForceMode.Impulse);

                StartTurboSmoke(dashDirection);
            }
        }

        // giant shrink-back: lerp player scale + camera distance back to normal together
        if (isShrinkingBack)
        {
            shrinkBackElapsed += Time.deltaTime;
            float t = shrinkBackElapsed / giantShrinkBackDuration;
            transform.localScale = Vector3.Lerp(shrinkBackStartScale, originalPlayerScale, t);

            // camera distance follows the same curve so it doesn't snap back early
            if (shoulderShift != null)
                shoulderShift.ForceCameraDistance(Mathf.Lerp(giantCameraDistance, shoulderShift.OriginalDistance, t));

            if (t >= 1f)
            {
                transform.localScale = originalPlayerScale;
                isShrinkingBack = false;
                isGiant = false;
                if (shoulderShift != null)
                    shoulderShift.ReleaseCameraDistance();
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

        if (hauntIndicator != null)
            hauntIndicator.transform.position = indicatorPos;
        for (int i = 0; i < hauntIndicators.Count; i++)
            if (hauntIndicators[i] != null)
                hauntIndicators[i].transform.position = indicatorPos + new Vector3(0, (i + 1) * stackSpacing.y * hauntStackMultiplier.y, 0);

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
        if (other.TryGetComponent(out KnockbackPowerupPickup knockbackPickup))
        {
            ApplyKnockbackPickup(knockbackPickup);
            Destroy(other.gameObject);
            return;
        }

        if (other.TryGetComponent(out SmashPowerupPickup smashPickup))
        {
            ApplySmashPickup(smashPickup);
            Destroy(other.gameObject);
            return;
        }

        if (other.TryGetComponent(out ShieldPowerupPickup shieldPickup))
        {
            ApplyShieldPickup(shieldPickup);
            Destroy(other.gameObject);
            return;
        }

        if (other.TryGetComponent(out GiantPowerupPickup giantPickup))
        {
            ApplyGiantPickup(giantPickup);
            Destroy(other.gameObject);
            return;
        }

        if (other.TryGetComponent(out HauntPowerupPickup hauntPickup))
        {
            ApplyHauntPickup(hauntPickup);
            Destroy(other.gameObject);
            return;
        }

        // fallback while transitioning old prefabs: still support tag-only pickups
        if (other.gameObject.CompareTag("KnockbackPowerup"))
        {
            ApplyKnockbackPickup(null);
            Destroy(other.gameObject);
        }
        else if (other.gameObject.CompareTag("SmashPowerup"))
        {
            ApplySmashPickup(null);
            Destroy(other.gameObject);
        }
        else if (other.gameObject.CompareTag("ShieldPowerup"))
        {
            ApplyShieldPickup(null);
            Destroy(other.gameObject);
        }
        else if (other.gameObject.CompareTag("GiantPowerup"))
        {
            ApplyGiantPickup(null);
            Destroy(other.gameObject);
        }
        else if (other.gameObject.CompareTag("HauntPowerup"))
        {
            ApplyHauntPickup(null);
            Destroy(other.gameObject);
        }
    }

    void EnsureIndicatorInstance(GameObject prefab, ref GameObject instance, ref Vector3 baseScale)
    {
        if (instance != null) return;
        if (!prefab) return;

        try
        {
            instance = Instantiate(prefab, transform.position, prefab.transform.rotation);
            instance.name = prefab.name;
            instance.SetActive(false);
            baseScale = instance.transform.lossyScale;
        }
        catch (MissingReferenceException)
        {
            // missing/broken prefab reference: leave indicator unset
            instance = null;
        }
    }

    void ApplyKnockbackPickup(KnockbackPowerupPickup pickup)
    {
        if (pickup != null)
        {
            powerUpDuration = pickup.durationSeconds;
            powerupStrength = pickup.boostedKnockbackStrength;
            knockbackStackMultiplier = pickup.stackMultiplier;
            EnsureIndicatorInstance(pickup.indicatorPrefab, ref powerupIndicator, ref knockbackIndicatorBaseScale);
        }

        knockbackStacks++;
        if (knockbackStacks == 1)
        {
            if (powerupIndicator != null) powerupIndicator.SetActive(true);
        }
        else
        {
            SpawnExtraIndicator(powerupIndicator, knockbackIndicatorBaseScale, knockbackIndicators, false, knockbackStackMultiplier);
        }

        if (knockbackCoroutine != null)
            StopCoroutine(knockbackCoroutine);
        knockbackCoroutine = StartCoroutine(PowerupCooldown());
    }

    void ApplySmashPickup(SmashPowerupPickup pickup)
    {
        if (pickup != null)
        {
            smashJumpForce = pickup.smashJumpForce;
            smashRadius = pickup.smashRadius;
            maxSmashForce = pickup.maxSmashForce;
            minSmashForce = pickup.minSmashForce;
            smashStackMultiplier = pickup.stackMultiplier;
            EnsureIndicatorInstance(pickup.indicatorPrefab, ref smashPowerupIndicator, ref smashIndicatorBaseScale);
        }

        smashPowerupStacks++;
        if (smashPowerupStacks == 1)
        {
            if (smashPowerupIndicator != null) smashPowerupIndicator.SetActive(true);
        }
        else
        {
            SpawnExtraIndicator(smashPowerupIndicator, smashIndicatorBaseScale, smashIndicators, false, smashStackMultiplier);
        }
    }

    void ApplyShieldPickup(ShieldPowerupPickup pickup)
    {
        if (pickup != null)
        {
            shieldRadius = pickup.shieldRadius;
            shieldMaxHits = pickup.shieldMaxHits;
            shieldShrinkDuration = pickup.shieldShrinkDuration;
            shieldStackMultiplier = pickup.stackMultiplier;
            EnsureIndicatorInstance(pickup.indicatorPrefab, ref shieldIndicator, ref shieldIndicatorBaseScale);
        }

        shieldStacks++;
        if (shieldStacks == 1 || shieldHitsRemaining <= 0)
            shieldHitsRemaining = shieldMaxHits;

        if (shieldStacks == 1)
        {
            if (shieldIndicator != null) shieldIndicator.SetActive(true);
        }
        else
        {
            SpawnExtraIndicator(shieldIndicator, shieldIndicatorBaseScale, shieldIndicators, true, shieldStackMultiplier);
        }
    }

    void ApplyGiantPickup(GiantPowerupPickup pickup)
    {
        if (pickup != null)
        {
            giantScale = pickup.giantScale;
            giantDuration = pickup.giantDuration;
            giantShrinkBackDuration = pickup.giantShrinkBackDuration;
            squishDuration = pickup.squishDuration;
            squishGroundOffset = pickup.squishGroundOffset;
            giantCameraDistance = pickup.cameraDistance;
            giantStackMultiplier = pickup.stackMultiplier;
            EnsureIndicatorInstance(pickup.indicatorPrefab, ref giantPowerupIndicator, ref giantIndicatorBaseScale);
        }

        giantStacks++;
        if (giantStacks == 1)
        {
            if (giantPowerupIndicator != null) giantPowerupIndicator.SetActive(true);
        }
        else
        {
            SpawnExtraIndicator(giantPowerupIndicator, giantIndicatorBaseScale, giantIndicators, false, giantStackMultiplier);
        }

        transform.localScale = originalPlayerScale * giantScale;
        isGiant = true;
        isShrinkingBack = false;

        // push camera back so the bigger ball doesn't fill the screen
        if (shoulderShift != null)
            shoulderShift.ForceCameraDistance(giantCameraDistance);

        if (giantCoroutine != null)
            StopCoroutine(giantCoroutine);
        giantCoroutine = StartCoroutine(GiantCooldown());
    }

    void ApplyHauntPickup(HauntPowerupPickup pickup)
    {
        if (pickup != null)
        {
            hauntDuration = pickup.hauntDuration;
            hauntSpeed = pickup.hauntSpeed;
            hauntEffectDuration = pickup.hauntEffectDuration;
            hauntStackMultiplier = pickup.stackMultiplier;
            hauntEnemyVfxPrefab = pickup.hauntEnemyVfxPrefab;
            EnsureIndicatorInstance(pickup.indicatorPrefab, ref hauntIndicator, ref hauntIndicatorBaseScale);
        }

        hauntStacks++;
        if (hauntStacks == 1)
        {
            if (hauntIndicator != null) hauntIndicator.SetActive(true);
        }
        else
        {
            SpawnExtraIndicator(hauntIndicator, hauntIndicatorBaseScale, hauntIndicators, false, hauntStackMultiplier);
        }

        // reset timer on re-pickup (same as knockback)
        if (hauntCoroutine != null)
            StopCoroutine(hauntCoroutine);
        hauntCoroutine = StartCoroutine(HauntCooldown());
    }

    // haunt buff wears off one stack at a time
    IEnumerator HauntCooldown()
    {
        yield return new WaitForSeconds(hauntDuration);
        hauntStacks--;
        if (hauntStacks <= 0)
        {
            hauntStacks = 0;
            ClearExtraIndicators(hauntIndicators);
            if (hauntIndicator != null) hauntIndicator.SetActive(false);
        }
        else
        {
            RemoveExtraIndicator(hauntIndicators);
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

        // begin shrink-back lerp (handled in Update, camera distance follows the same curve)
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
        hasSmashImpactData = false;
        lastSmashImpactNormal = Vector3.up;
        lastSmashImpactNormalVariance = 0f;
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
        SpawnSmashImpactSmoke();
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

    void RecordDiveImpact(Collision collision)
    {
        int count = collision.contactCount;
        if (count <= 0)
        {
            lastSmashImpactPoint = transform.position;
            lastSmashImpactNormal = Vector3.up;
            lastSmashImpactNormalVariance = 0f;
            hasSmashImpactData = true;
            return;
        }

        Vector3 avgPoint = Vector3.zero;
        Vector3 avgNormal = Vector3.zero;
        float variance = 0f;

        for (int i = 0; i < count; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            avgPoint += contact.point;
            avgNormal += contact.normal;
        }

        avgPoint /= count;
        avgNormal = avgNormal.normalized;
        if (avgNormal.sqrMagnitude < 0.001f) avgNormal = Vector3.up;

        for (int i = 0; i < count; i++)
        {
            float angle = Vector3.Angle(avgNormal, collision.GetContact(i).normal);
            if (angle > variance) variance = angle;
        }

        lastSmashImpactPoint = avgPoint;
        lastSmashImpactNormal = avgNormal;
        lastSmashImpactNormalVariance = variance;
        hasSmashImpactData = true;
    }

    void SpawnSmashImpactSmoke()
    {
        if (!enableSmashImpactSmoke) return;
        if (smashImpactSmokePrefab == null) return;

        Vector3 impactPoint = hasSmashImpactData ? lastSmashImpactPoint : transform.position;
        Vector3 impactNormal = hasSmashImpactData ? lastSmashImpactNormal : Vector3.up;
        impactNormal.Normalize();

        float cornerFactor = Mathf.InverseLerp(0f, 45f, lastSmashImpactNormalVariance);
        float spread = Mathf.Clamp01(0.45f + cornerFactor * smashSmokeCornerSpreadBoost);
        int particleCount = Mathf.RoundToInt(Mathf.Lerp(smashSmokeMinParticles, smashSmokeMaxParticles, 0.5f + cornerFactor * 0.5f));
        particleCount = Mathf.Max(1, particleCount);
        // build a tangent basis from the impact normal so smoke expands across the surface plane
        Vector3 tangentA = Vector3.Cross(impactNormal, Mathf.Abs(impactNormal.y) > 0.98f ? Vector3.forward : Vector3.up).normalized;
        Vector3 tangentB = Vector3.Cross(impactNormal, tangentA).normalized;

        ParticleSystem smoke = Instantiate(
            smashImpactSmokePrefab,
            impactPoint + impactNormal * smashSmokeSurfaceOffset,
            Quaternion.LookRotation(impactNormal)
        );

        // force one-shot burst behavior so this never runs as a continuous loop
        var main = smoke.main;
        main.loop = false;
        var emission = smoke.emission;
        emission.enabled = false;

        smoke.Clear(true);
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
        emit.applyShapeToPosition = true;

        for (int i = 0; i < particleCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 planeDir = (Mathf.Cos(angle) * tangentA + Mathf.Sin(angle) * tangentB).normalized;

            // keep jitter on the impact plane so smoke doesn't bias upward
            Vector2 jitter2D = Random.insideUnitCircle * spread * 0.35f;
            Vector3 jitter = tangentA * jitter2D.x + tangentB * jitter2D.y;
            Vector3 emitDir = (planeDir + jitter).normalized;
            emit.velocity = emitDir * Random.Range(smashSmokeMinSpeed, smashSmokeMaxSpeed);
            smoke.Emit(emit, 1);
        }

        smoke.Play();
        Destroy(smoke.gameObject, 3f);
    }

    void StartTurboSmoke(Vector3 dashDirection)
    {
        if (!enableTurboVfx) return;

        if (turboSmokeCoroutine != null)
            StopCoroutine(turboSmokeCoroutine);
        turboSmokeCoroutine = StartCoroutine(TurboSmokeRoutine(dashDirection));
    }

    IEnumerator TurboSmokeRoutine(Vector3 initialDashDirection)
    {
        float duration = Mathf.Max(0f, turboSmokeDuration);
        float tick = Mathf.Max(0.01f, turboSmokeTickInterval);
        float elapsed = 0f;
        bool clearFirst = true;

        while (elapsed < duration)
        {
            Vector3 currentDirection = initialDashDirection;
            Vector3 flatVelocity = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
            if (flatVelocity.sqrMagnitude > 0.001f)
                currentDirection = flatVelocity.normalized;
            else if (lastMoveDirection.sqrMagnitude > 0.001f)
                currentDirection = lastMoveDirection;

            EmitTurboSmokeBurst(currentDirection, clearFirst);
            clearFirst = false;
            elapsed += tick;
            yield return new WaitForSeconds(tick);
        }

        turboSmokeCoroutine = null;
        if (runtimeTurboSmokeFx != null)
        {
            Destroy(runtimeTurboSmokeFx.gameObject, 2.5f);
            runtimeTurboSmokeFx = null;
        }
    }

    void EmitTurboSmokeBurst(Vector3 dashDirection, bool clearBeforeEmit)
    {
        if (!enableTurboVfx) return;

        Vector3 backDir = -dashDirection.normalized;
        if (backDir.sqrMagnitude < 0.001f) backDir = -transform.forward;
        Vector3 spawnPos = transform.position + backDir * turboSmokeBackOffset + Vector3.up * turboSmokeUpOffset;

        ParticleSystem turboFx = turboParticle;

        // if turbo is unset (or set to a prefab asset), spawn/reuse a runtime smoke instance
        if (turboFx == null || !turboFx.gameObject.scene.IsValid())
        {
            if (smashImpactSmokePrefab == null) return;
            if (runtimeTurboSmokeFx == null)
                runtimeTurboSmokeFx = Instantiate(smashImpactSmokePrefab, spawnPos, Quaternion.LookRotation(backDir));
            turboFx = runtimeTurboSmokeFx;
        }
        turboFx.transform.position = spawnPos;

        if (clearBeforeEmit)
            turboFx.Clear(true);

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
        int count = Mathf.Max(1, turboSmokeParticles);
        for (int i = 0; i < count; i++)
        {
            Vector3 randomDir = (backDir + Random.insideUnitSphere * turboSmokeConeSpread).normalized;
            emit.velocity = randomDir * Random.Range(turboSmokeMinSpeed, turboSmokeMaxSpeed);
            turboFx.Emit(emit, 1);
        }

        turboFx.Play();
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
        if (turboSmokeCoroutine != null)
        {
            StopCoroutine(turboSmokeCoroutine);
            turboSmokeCoroutine = null;
        }
        if (runtimeTurboSmokeFx != null)
        {
            Destroy(runtimeTurboSmokeFx.gameObject);
            runtimeTurboSmokeFx = null;
        }

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
            RecordDiveImpact(other);
        }

        if (other.gameObject.CompareTag("Enemy"))
        {
            // giant handles enemies via OverlapSphere, skip knockback
            if (isGiant) return;

            // haunt the enemy if buff is active
            if (hauntStacks > 0)
            {
                EnemyX enemy = other.gameObject.GetComponent<EnemyX>();
                if (enemy != null) enemy.Haunt(hauntSpeed, hauntEffectDuration, hauntEnemyVfxPrefab);
            }

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

    // read-only powerup state for external scripts (e.g. PlayerPowerupColor)
    public bool IsKnockbackActive => knockbackStacks > 0;
    public bool IsSmashActive => smashPowerupStacks > 0;
    public bool IsShieldActive => shieldStacks > 0;
    public bool IsGiantActive => isGiant;
    public bool IsHauntActive => hauntStacks > 0;
}
