using UnityEngine;
using UnityEngine.UI;

// fullscreen anime speed lines driven by player speed
// attach to a UI RawImage that covers the whole screen
// keep the RawImage DISABLED in editor to avoid the white screen — this script enables it at runtime
public class SpeedLinesEffect : MonoBehaviour
{
    [Header("References")]
    [Tooltip("If empty, auto-finds PlayerControllerX in scene")]
    public PlayerControllerX player;

    [Tooltip("Pre-made material using Custom/SpeedLines shader. Edit this in the Inspector without play mode. If empty, auto-creates one from the shader reference.")]
    public Material speedLinesMaterial;

    [Tooltip("Fallback: drag the Custom/SpeedLines shader here in case no material is assigned")]
    public Shader speedLinesShader;

    [Header("Speed Mapping")]
    [Tooltip("What counts as 'max speed' for the effect. Set higher than gameplay maxSpeed since turbo/gravity push you way past it. 0 = use player's maxSpeed field")]
    public float effectMaxSpeed = 40f;

    [Tooltip("Speed ratio (0–1) to intensity curve. X = speed/effectMaxSpeed, Y = line intensity")]
    public AnimationCurve intensityCurve = new AnimationCurve(
        new Keyframe(0f, 0f),       // no lines at rest
        new Keyframe(0.3f, 0f),     // still nothing below 30% speed
        new Keyframe(0.6f, 0.3f),   // lines start fading in
        new Keyframe(1f, 1f)        // full intensity at max speed
    );

    [Tooltip("Speed ratio to inner radius curve. X = speed/maxSpeed, Y = inner radius (1=edge, 0=center)")]
    public AnimationCurve radiusCurve = new AnimationCurve(
        new Keyframe(0f, 0.9f),     // lines only at screen edge when slow
        new Keyframe(0.5f, 0.6f),   // creep toward center
        new Keyframe(1f, 0.3f)      // reach close to center at max speed
    );

    [Header("Smoothing")]
    [Tooltip("How smooth/delayed the effect is. Higher = more gradual tween, lower = snappier. 0 = instant.")]
    public float smoothing = 0.15f;

    private RawImage rawImage;
    private Material mat;
    private bool ownsMatInstance; // true if we created the material, so we clean it up
    private float currentIntensity;
    private float currentRadius;

    private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
    private static readonly int InnerRadiusID = Shader.PropertyToID("_InnerRadius");
    private static readonly int AspectRatioID = Shader.PropertyToID("_AspectRatio");
    private static readonly int UnscaledTimeID = Shader.PropertyToID("_UnscaledTime");

    void Start()
    {
        rawImage = GetComponent<RawImage>();
        if (rawImage == null)
        {
            Debug.LogError("SpeedLinesEffect: needs a RawImage component");
            enabled = false;
            return;
        }

        // use assigned material directly — all visual tuning lives on the material asset
        if (speedLinesMaterial != null)
        {
            mat = speedLinesMaterial;
            ownsMatInstance = false;
        }
        else
        {
            // fallback: create from shader reference
            Shader shader = speedLinesShader;
            if (shader == null)
                shader = Shader.Find("Custom/SpeedLines");

            if (shader == null)
            {
                Debug.LogError("SpeedLinesEffect: assign a material or shader!");
                enabled = false;
                return;
            }

            mat = new Material(shader);
            ownsMatInstance = true;
        }

        rawImage.material = mat;
        rawImage.texture = null;
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;

        // enable at runtime (keep disabled in editor so you don't get a white screen)
        rawImage.enabled = true;

        if (player == null)
            player = FindAnyObjectByType<PlayerControllerX>();

        currentRadius = radiusCurve.Evaluate(0f);
    }

    void Update()
    {
        if (player == null || mat == null) return;

        // use our own max speed so turbo/gravity don't instantly cap the effect
        float maxSpd = effectMaxSpeed > 0f ? effectMaxSpeed : player.maxSpeed;
        float speedRatio = Mathf.Clamp01(player.CurrentSpeed / maxSpd);

        // force max during smash (aiming/diving in slow-mo)
        bool smashing = player.IsSmashing;
        float targetIntensity = smashing ? 1f : intensityCurve.Evaluate(speedRatio);
        float targetRadius = smashing ? radiusCurve.Evaluate(1f) : radiusCurve.Evaluate(speedRatio);

        // exponential ease — smoothing controls how sluggish the tween is
        // smoothing 0 = instant snap, higher = more gradual
        float lerpFactor = smoothing > 0.001f
            ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothing)
            : 1f;
        currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, lerpFactor);
        currentRadius = Mathf.Lerp(currentRadius, targetRadius, lerpFactor);

        // only write intensity, radius, and aspect — everything else stays as you set it on the material
        mat.SetFloat(IntensityID, currentIntensity);
        mat.SetFloat(InnerRadiusID, currentRadius);
        mat.SetFloat(AspectRatioID, (float)Screen.width / Screen.height);
        mat.SetFloat(UnscaledTimeID, Time.unscaledTime);
    }

    void OnDestroy()
    {
        if (ownsMatInstance && mat != null)
            Destroy(mat);
    }
}
