using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerControllerX : MonoBehaviour
{
    private Rigidbody playerRb;
    public float speed = 15;
    public float maxSpeed = 20f; // top speed the player can reach
    [SerializeField] private GameObject focalPoint; // wire in Inspector instead of GameObject.Find
    public float brakingFactor = 5f; // how fast you stop when changing direction
    public float gravityMultiplier = 2.5f; // extra gravity so the ball doesn't feel floaty
    public float jumpForce = 8f; // upward impulse on jump
    public float groundCheckDistance = 1.5f; // raycast length for grounded check
    [SerializeField] private LayerMask groundCheckLayers = ~0; // what counts as ground for movement
    [SerializeField] private float groundContactNormalMin = 0.5f; // min upward normal to count as ground contact
    [SerializeField] private float landingMinImpactSpeed = 6f; // fallback trigger when drop height is small but impact is hard

    private float knockbackTimer;
    private GameObject powerupIndicator;
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
    private float knockbackDuration = 5f; // duration per pickup (read from prefab)

    // smash powerup
    private int smashPowerupStacks;
    private bool isSmashing;
    private bool isGrounded;
    private GameObject smashPowerupIndicator;
    private float smashJumpForce = 15f;
    private float smashRadius = 15f;
    private float maxSmashForce = 50f;
    private float minSmashForce = 10f;

    // shield powerup
    private int shieldStacks;
    private GameObject shieldIndicator;
    private float shieldRadius = 4f;       // how far the shield reaches
    private int shieldMaxHits = 3;         // enemies destroyed before shield breaks
    private float shieldShrinkDuration = 0.4f; // how long enemies shrink before vanishing
    private int shieldHitsRemaining;
    private Vector3 shieldStackMultiplier = new Vector3(0.3f, 0.3f, 0.3f); // scales stackSpacing for this powerup

    // giant powerup
    private float giantTimer;
    private GameObject giantPowerupIndicator;
    private float giantScale = 2f;
    private float giantCameraDistance = 8f;      // camera distance while giant
    private float giantDuration = 10f;          // how long the player stays giant
    private float giantShrinkBackDuration = 3f; // how long shrink-back takes
    private float squishDuration = 1f;          // how long enemies flatten
    private float squishGroundOffset = 0.03f;   // vertical offset above detected ground for squished enemies
    private Vector3 originalPlayerScale;
    private bool isGiant;
    private bool isShrinkingBack;
    private Vector3 shrinkBackStartScale;
    private float shrinkBackElapsed;

    // haunt powerup — touched enemies aggressively home toward their own goal
    private float hauntTimer;
    private float hauntDuration = 8f;
    private float hauntSpeed = 15f;
    private float hauntEffectDuration = 5f;
    private GameObject hauntIndicator;
    private GameObject hauntEnemyVfxPrefab; // particle effect to spawn on haunted enemies

    // rush ability — passively charges, press Q to activate
    private float rushTimer;
    private float rushCharge;
    [SerializeField] private KeyCode rushActivationKey = KeyCode.Q;
    [SerializeField] private float rushChargeTimeToReady = 20f; // seconds to fully charge from 0 to ready
    [SerializeField] private float rushDuration = 8f;           // active duration once triggered
    [SerializeField] private float rushSpeedMultiplier = 1.6f;
    [SerializeField] private float rushMaxSpeedMultiplier = 1.35f;
    [SerializeField] private float rushEnemySpeedMultiplier = 0.5f;
    private bool rushReadySfxPlayed;

    // stacking visuals — charge/hit-based powerups still clone indicators per stack
    // X/Z = scale growth per stack, Y = vertical position offset per stack
    public Vector3 stackSpacing = new Vector3(0.3f, 0.15f, 0.3f);
    [SerializeField] private float indicatorYOffset = -0.6f; // vertical offset below player for indicators
    private List<GameObject> shieldIndicators = new List<GameObject>();
    private Vector3 smashIndicatorBaseScale;
    private Vector3 shieldIndicatorBaseScale;

    // smash aiming system
    public GameObject landingIndicator;
    public float slowMotionScale = 0.15f;
    public float diveSpeed = 40f;
    public float diveAcceleration = 60f; // how much faster the dive gets per second
    public float maxDiveSpeed = 200f; // cap on time-compensated dive velocity to prevent tunneling
    public float launchDuration = 0.5f;
    public Vector3 smashCameraOffset = new Vector3(1f, 2f, -2f);
    private RotateCameraX cameraRotator;
    private CameraShoulderShift shoulderShift;
    private CinemachineBrain cinemachineBrain;
    private Vector3 landingTarget;
    private bool isAiming;
    private bool isDiving;
    private float airbornePeakY; // highest Y reached during current airborne window
    private bool landingSfxArmed; // arms when leaving ground, disarms on first ground contact
    private float lastVerticalVelocity;
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
    private Coroutine smashCoroutine;
    private static Shader cachedSpriteShader; // avoid Shader.Find every Start

    void Start()
    {
        playerRb = GetComponent<Rigidbody>();
        // fallback if not wired in Inspector (backwards-compat with existing scenes)
        if (focalPoint == null)
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

        // save base scales for cloning extra stacks later (charge/hit-based only)
        if (smashPowerupIndicator != null)
            smashIndicatorBaseScale = smashPowerupIndicator.transform.lossyScale;
        if (shieldIndicator != null)
            shieldIndicatorBaseScale = new Vector3(shieldRadius * 2f, shieldRadius * 2f, shieldRadius * 2f);

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

        // create aim line renderer (shader cached statically so Find only runs once across all loads)
        if (cachedSpriteShader == null)
            cachedSpriteShader = Shader.Find("Sprites/Default");
        aimLine = gameObject.AddComponent<LineRenderer>();
        aimLine.startWidth = 0.1f;
        aimLine.endWidth = 0.1f;
        aimLine.material = new Material(cachedSpriteShader);
        aimLine.startColor = Color.red;
        aimLine.endColor = Color.red;
        aimLine.positionCount = 2;
        aimLine.enabled = false;

        // safety on scene start
        EnemyX.SetGlobalSpeedMultiplier(1f);
    }

    void FixedUpdate()
    {
        lastVerticalVelocity = playerRb.linearVelocity.y;

        // apply extra gravity so the ball falls snappier
        if (playerRb.useGravity && !isGrounded)
        {
            playerRb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    void Update()
    {
        // always check ground state
        isGrounded = CheckGrounded();

        // arm landing SFX once per airborne cycle, and track peak height while in the air
        if (!isGrounded)
        {
            if (!landingSfxArmed)
            {
                landingSfxArmed = true;
                airbornePeakY = transform.position.y;
            }
            else if (transform.position.y > airbornePeakY)
            {
                airbornePeakY = transform.position.y;
            }
        }
        else if (!landingSfxArmed)
        {
            airbornePeakY = transform.position.y;
        }

        // skip gameplay input/logic while paused
        if (Time.timeScale == 0f) return;

        // tick duration-based powerup timers
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.deltaTime;
            if (knockbackTimer <= 0f)
            {
                knockbackTimer = 0f;
                if (powerupIndicator != null) powerupIndicator.SetActive(false);
            }
        }

        if (hauntTimer > 0f)
        {
            hauntTimer -= Time.deltaTime;
            if (hauntTimer <= 0f)
            {
                hauntTimer = 0f;
                if (hauntIndicator != null) hauntIndicator.SetActive(false);
            }
        }

        if (rushTimer > 0f)
        {
            rushTimer -= Time.deltaTime;
            if (rushTimer <= 0f)
            {
                rushTimer = 0f;
                EnemyX.SetGlobalSpeedMultiplier(1f);
                if (SFXManager.Instance != null) SFXManager.Instance.StopRushLoop();
                if (MusicManager.Instance != null) MusicManager.Instance.FadeFromRush();
            }
        }
        else
        {
            // passive charge while rush is inactive
            float chargePerSecond = rushChargeTimeToReady > 0.001f ? 1f / rushChargeTimeToReady : 1f;
            rushCharge = Mathf.Clamp01(rushCharge + chargePerSecond * Time.deltaTime);

            // one-shot ready SFX when charge first reaches full
            if (rushCharge >= 1f && !rushReadySfxPlayed)
            {
                rushReadySfxPlayed = true;
                if (SFXManager.Instance != null) SFXManager.Instance.PlayRushReady();
            }
        }

        if (giantTimer > 0f)
        {
            giantTimer -= Time.deltaTime;
            if (giantTimer <= 0f)
            {
                giantTimer = 0f;
                if (giantPowerupIndicator != null) giantPowerupIndicator.SetActive(false);
                // begin shrink-back lerp (handled below, camera distance follows the same curve)
                shrinkBackStartScale = transform.localScale;
                shrinkBackElapsed = 0f;
                isShrinkingBack = true;
            }
        }

        // F key: during aiming phase → dive request; otherwise → start smash
        if (Input.GetKeyDown(KeyCode.F) && isAiming)
        {
            diveRequested = true;
        }
        else if (Input.GetKeyDown(KeyCode.F) && smashPowerupStacks > 0 && !isSmashing)
        {
            smashCoroutine = StartCoroutine(PerformSmashAttack());
        }

        // Q (default): activate rush when fully charged
        if (Input.GetKeyDown(rushActivationKey) && CanActivateRush())
        {
            ActivateRush();
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
            float currentMaxSpeed = maxSpeed * GetCurrentMaxSpeedMultiplier();
            float speedFactor = Mathf.Clamp01(1f - currentSpeed / currentMaxSpeed);
            playerRb.AddForce(moveDirection * speed * GetCurrentSpeedMultiplier() * speedFactor, ForceMode.Force);

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

        // position indicators under the player
        Vector3 indicatorPos = transform.position + new Vector3(0, indicatorYOffset, 0);

        if (powerupIndicator != null) powerupIndicator.transform.position = indicatorPos;
        if (smashPowerupIndicator != null) smashPowerupIndicator.transform.position = indicatorPos;
        if (giantPowerupIndicator != null) giantPowerupIndicator.transform.position = indicatorPos;
        if (hauntIndicator != null) hauntIndicator.transform.position = indicatorPos;

        // shield is special: centered on player, scales to radius
        if (shieldIndicator != null)
        {
            shieldIndicator.transform.position = transform.position;
            float diameter = shieldRadius * 2f;
            shieldIndicator.transform.localScale = new Vector3(diameter, diameter, diameter);
        }
        UpdateIndicatorPosition(null, shieldIndicators, shieldStackMultiplier, transform.position);

        // giant: squish enemies on contact (overlap, not collision, so no physics push)
        if (isGiant)
        {
            float giantRadius = transform.localScale.x * 0.5f + 1f; // catch enemies just before they physically collide
            Collider[] giantHits = Physics.OverlapSphere(transform.position, giantRadius);
            foreach (Collider c in giantHits)
            {
                if (c.CompareTag("Enemy"))
                {
                    EnemyX enemyAI = c.GetComponent<EnemyX>();
                    if (GameManagerX.Instance != null)
                        GameManagerX.Instance.EnemyScored(enemyAI);

                    c.gameObject.tag = "Untagged";
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
                    if (SFXManager.Instance != null) SFXManager.Instance.PlayGiantSquish();
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
                    EnemyX enemyAI = col.GetComponent<EnemyX>();
                    if (GameManagerX.Instance != null)
                    {
                        GameManagerX.Instance.EnemyScored(enemyAI);
                    }

                    // untag so it won't be detected again, disable AI, then shrink away
                    col.gameObject.tag = "Untagged";
                    if (enemyAI != null) enemyAI.enabled = false;
                    Rigidbody enemyRb = col.GetComponent<Rigidbody>();
                    if (enemyRb != null) enemyRb.isKinematic = true;
                    StartCoroutine(ShrinkAndDestroy(col.gameObject, shieldShrinkDuration));
                    if (SFXManager.Instance != null) SFXManager.Instance.PlayShieldBreak();

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
            if (SFXManager.Instance != null) SFXManager.Instance.PlayPowerupPickup();
            ApplyKnockbackPickup(knockbackPickup);
            Destroy(other.gameObject);
            return;
        }

        if (other.TryGetComponent(out SmashPowerupPickup smashPickup))
        {
            if (SFXManager.Instance != null) SFXManager.Instance.PlayPowerupPickup();
            ApplySmashPickup(smashPickup);
            Destroy(other.gameObject);
            return;
        }

        if (other.TryGetComponent(out ShieldPowerupPickup shieldPickup))
        {
            if (SFXManager.Instance != null) SFXManager.Instance.PlayPowerupPickup();
            ApplyShieldPickup(shieldPickup);
            Destroy(other.gameObject);
            return;
        }

        if (other.TryGetComponent(out GiantPowerupPickup giantPickup))
        {
            if (SFXManager.Instance != null) SFXManager.Instance.PlayPowerupPickup();
            ApplyGiantPickup(giantPickup);
            Destroy(other.gameObject);
            return;
        }

        if (other.TryGetComponent(out HauntPowerupPickup hauntPickup))
        {
            if (SFXManager.Instance != null) SFXManager.Instance.PlayPowerupPickup();
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
        Vector3 unusedScale = Vector3.zero;
        if (pickup != null)
        {
            knockbackDuration = pickup.durationSeconds;
            powerupStrength = pickup.boostedKnockbackStrength;
            EnsureIndicatorInstance(pickup.indicatorPrefab, ref powerupIndicator, ref unusedScale);
        }

        // extend remaining time by full duration
        knockbackTimer += knockbackDuration;
        if (powerupIndicator != null) powerupIndicator.SetActive(true);
    }

    void ApplySmashPickup(SmashPowerupPickup pickup)
    {
        if (pickup != null)
        {
            smashJumpForce = pickup.smashJumpForce;
            smashRadius = pickup.smashRadius;
            maxSmashForce = pickup.maxSmashForce;
            minSmashForce = pickup.minSmashForce;
            EnsureIndicatorInstance(pickup.indicatorPrefab, ref smashPowerupIndicator, ref smashIndicatorBaseScale);
        }


        smashPowerupStacks++;
        // single indicator regardless of stack count (like haunt)
        if (smashPowerupIndicator != null) smashPowerupIndicator.SetActive(true);
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
        Vector3 unusedScale = Vector3.zero;
        if (pickup != null)
        {
            giantScale = pickup.giantScale;
            giantDuration = pickup.giantDuration;
            giantShrinkBackDuration = pickup.giantShrinkBackDuration;
            squishDuration = pickup.squishDuration;
            squishGroundOffset = pickup.squishGroundOffset;
            giantCameraDistance = pickup.cameraDistance;
            EnsureIndicatorInstance(pickup.indicatorPrefab, ref giantPowerupIndicator, ref unusedScale);
        }

        // extend remaining time by full duration
        giantTimer += giantDuration;
        if (giantPowerupIndicator != null) giantPowerupIndicator.SetActive(true);

        transform.localScale = originalPlayerScale * giantScale;
        isGiant = true;
        isShrinkingBack = false;

        // push camera back so the bigger ball doesn't fill the screen
        if (shoulderShift != null)
            shoulderShift.ForceCameraDistance(giantCameraDistance);
    }

    void ApplyHauntPickup(HauntPowerupPickup pickup)
    {
        Vector3 unusedScale = Vector3.zero;
        if (pickup != null)
        {
            hauntDuration = pickup.hauntDuration;
            hauntSpeed = pickup.hauntSpeed;
            hauntEffectDuration = pickup.hauntEffectDuration;
            hauntEnemyVfxPrefab = pickup.hauntEnemyVfxPrefab;
            EnsureIndicatorInstance(pickup.indicatorPrefab, ref hauntIndicator, ref unusedScale);
        }

        // extend remaining time by full duration
        hauntTimer += hauntDuration;
        if (hauntIndicator != null) hauntIndicator.SetActive(true);
    }

    bool CanActivateRush()
    {
        if (isSmashing) return false;
        if (rushTimer > 0f) return false;
        return rushCharge >= 1f;
    }

    void ActivateRush()
    {
        rushCharge = 0f;
        rushTimer = rushDuration;
        rushReadySfxPlayed = false;
        EnemyX.SetGlobalSpeedMultiplier(rushEnemySpeedMultiplier);
        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayRushActivate();
            SFXManager.Instance.StartRushLoop();
        }
        if (MusicManager.Instance != null) MusicManager.Instance.FadeToRush();
    }

    float GetCurrentSpeedMultiplier()
    {
        return rushTimer > 0f ? rushSpeedMultiplier : 1f;
    }

    float GetCurrentMaxSpeedMultiplier()
    {
        return rushTimer > 0f ? rushMaxSpeedMultiplier : 1f;
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

    // shared positioning logic for all indicator types (replaces 5x copy-pasted blocks)
    void UpdateIndicatorPosition(GameObject indicator, List<GameObject> extras, Vector3 multiplier, Vector3 basePos)
    {
        if (indicator != null)
            indicator.transform.position = basePos;
        for (int i = 0; i < extras.Count; i++)
            if (extras[i] != null)
                extras[i].transform.position = basePos + new Vector3(0, (i + 1) * stackSpacing.y * multiplier.y, 0);
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
        if (SFXManager.Instance != null) SFXManager.Instance.PlaySmashLaunch();

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
        if (SFXManager.Instance != null) SFXManager.Instance.PlaySmashDive();

        // dive until we hit something (ground, wall, enemy, anything)
        // switch to continuous so the ball can't tunnel through geometry at high speed
        playerRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        isDiving = true;
        diveCollided = false;
        float currentDiveSpeed = diveSpeed;
        // maxDiveSpeed configured in Inspector
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
            float compensatedSpeed = Mathf.Min(currentDiveSpeed * timeCompensation, maxDiveSpeed);
            playerRb.linearVelocity = diveDirection * compensatedSpeed;
            yield return new WaitForFixedUpdate();
        }
        isDiving = false;

        // --- Phase 5: Impact — kill momentum, restore time, then smash ---
        playerRb.linearVelocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;
        // restore discrete now that dive is over
        playerRb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        if (cinemachineBrain != null) cinemachineBrain.IgnoreTimeScale = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = originalFixedDeltaTime;
        SpawnSmashImpactSmoke();
        if (SFXManager.Instance != null) SFXManager.Instance.PlaySmashImpact();
        ApplySmashImpact();

        // consume one stack
        smashPowerupStacks--;
        if (smashPowerupStacks <= 0)
        {
            smashPowerupStacks = 0;
            if (smashPowerupIndicator != null) smashPowerupIndicator.SetActive(false);
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

    // cancel any active smash and restore all state to normal
    // called by SpawnManagerX between waves so the player doesn't stay stuck mid-smash
    public void ForceResetState()
    {
        // only stop the smash coroutine — leave powerup timers alone
        if (smashCoroutine != null)
        {
            StopCoroutine(smashCoroutine);
            smashCoroutine = null;
        }

        isSmashing = false;
        isAiming = false;
        isDiving = false;
        diveCollided = false;
        diveRequested = false;

        playerRb.useGravity = true;
        playerRb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        // restore time in case we were mid slow-mo
        Time.timeScale = 1f;
        Time.fixedDeltaTime = originalFixedDeltaTime;

        if (cinemachineBrain != null) cinemachineBrain.IgnoreTimeScale = false;

        if (cameraRotator != null)
            cameraRotator.RestorePitchClamp();
        if (shoulderShift != null)
        {
            shoulderShift.ReleaseCameraSide();
            shoulderShift.ReleaseShoulderOffset();
        }

        if (landingIndicator != null) landingIndicator.SetActive(false);
        if (aimLine != null) aimLine.enabled = false;
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

        // reset global slowdown on disable (scene unload / respawn safety)
        if (rushTimer > 0f)
        {
            EnemyX.SetGlobalSpeedMultiplier(1f);
            if (SFXManager.Instance != null) SFXManager.Instance.StopRushLoop();
            if (MusicManager.Instance != null) MusicManager.Instance.FadeFromRush();
        }
    }

    // bump enemies away on contact
    private void OnCollisionEnter(Collision other)
    {
        // any collision during dive = landed, skip knockback so physics doesn't fight the dive
        if (isDiving)
        {
            diveCollided = true;
            RecordDiveImpact(other);
            return;
        }

        TryPlayLandingSfx(other);

        if (other.gameObject.CompareTag("Enemy"))
        {
            if (SFXManager.Instance != null) SFXManager.Instance.PlayEnemyHit();

            // giant handles enemies via OverlapSphere, skip knockback
            if (isGiant) return;

            // haunt the enemy if buff is active
            if (hauntTimer > 0f)
            {
                EnemyX enemy = other.gameObject.GetComponent<EnemyX>();
                if (enemy != null) enemy.Haunt(hauntSpeed, hauntEffectDuration, hauntEnemyVfxPrefab);
            }

            Rigidbody enemyRigidbody = other.gameObject.GetComponent<Rigidbody>();
            Vector3 awayFromPlayer = (other.gameObject.transform.position - transform.position).normalized;

            if (knockbackTimer > 0f)
            {
                enemyRigidbody.AddForce(awayFromPlayer * powerupStrength, ForceMode.Impulse);
            }
            else
            {
                enemyRigidbody.AddForce(awayFromPlayer * normalStrength, ForceMode.Impulse);
            }
        }
    }

    bool CheckGrounded()
    {
        if (Physics.Raycast(
            transform.position,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            groundCheckLayers,
            QueryTriggerInteraction.Ignore))
        {
            // don't treat enemies as ground
            if (hit.collider != null && hit.collider.CompareTag("Enemy"))
                return false;
            return true;
        }

        return false;
    }

    void TryPlayLandingSfx(Collision other)
    {
        if (!landingSfxArmed) return;
        if (SFXManager.Instance == null) return;
        if (other == null || other.collider == null) return;
        if (other.collider.isTrigger) return;
        if (other.gameObject.CompareTag("Enemy")) return;

        bool groundLikeContact = false;
        int contacts = other.contactCount;
        for (int i = 0; i < contacts; i++)
        {
            ContactPoint cp = other.GetContact(i);
            if (cp.normal.y >= groundContactNormalMin)
            {
                groundLikeContact = true;
                break;
            }
        }
        if (!groundLikeContact) return;

        float fallDistance = airbornePeakY - transform.position.y;
        float impactSpeed = Mathf.Max(0f, -lastVerticalVelocity);
        if (fallDistance >= SFXManager.Instance.MinFallHeight || impactSpeed >= landingMinImpactSpeed)
            SFXManager.Instance.PlayLanding();

        landingSfxArmed = false;
        airbornePeakY = transform.position.y;
    }

    // read-only powerup state for external UI/visual scripts
    public bool IsKnockbackActive => knockbackTimer > 0f;
    public bool IsSmashActive => smashPowerupStacks > 0;
    public bool IsShieldActive => shieldStacks > 0;
    public bool IsGiantActive => isGiant;
    public bool IsHauntActive => hauntTimer > 0f;
    public bool IsRushActive => rushTimer > 0f;
    public bool IsRushReady => rushTimer <= 0f && rushCharge >= 1f;

    // remaining seconds on each duration-based powerup (for timer UI)
    public float KnockbackTimer => knockbackTimer;
    public float GiantTimer => giantTimer + (isShrinkingBack ? (giantShrinkBackDuration - shrinkBackElapsed) : 0f);
    public float HauntTimer => hauntTimer;
    public float RushTimer => rushTimer;
    public float RushDuration => rushDuration;
    public float RushChargeTimeToReady => rushChargeTimeToReady;
    public float RushChargeNormalized => IsRushActive
        ? (rushDuration > 0.001f ? Mathf.Clamp01(rushTimer / rushDuration) : 0f)
        : rushCharge;
    public int SmashStacks => smashPowerupStacks;
    public int ShieldStacks => shieldStacks;
    public int ShieldHitsRemaining => shieldHitsRemaining;
    public int ShieldMaxHits => shieldMaxHits;

    // raw velocity magnitude (used by SpeedLinesEffect)
    public float CurrentSpeed => playerRb != null ? playerRb.linearVelocity.magnitude : 0f;

    // true during smash aiming or diving (slow-mo phases)
    public bool IsSmashing => isAiming || isDiving;

    // 0–1 ratio of current speed to max speed
    public float SpeedRatio => playerRb != null ? Mathf.Clamp01(playerRb.linearVelocity.magnitude / (maxSpeed * GetCurrentMaxSpeedMultiplier())) : 0f;
}
