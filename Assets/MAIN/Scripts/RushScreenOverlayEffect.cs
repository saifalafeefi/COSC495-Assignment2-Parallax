using UnityEngine;
using UnityEngine.UI;

// full-screen UI overlay that color-cycles while rush is active
// attach to a Screen Space canvas Image/RawImage stretched full screen
public class RushScreenOverlayEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerControllerX player;
    [SerializeField] private Graphic overlayGraphic;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float enterDuration = 0.2f;
    [SerializeField, Min(0f)] private float exitDuration = 0.3f;
    [SerializeField] private AnimationCurve enterCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve exitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Rainbow")]
    [SerializeField] private float baseHueCycleSpeed = 0.3f;
    [SerializeField] private float chaosFrequency = 2.9f;
    [SerializeField] private float chaosAmplitude = 0.22f;
    [SerializeField, Range(0f, 1f)] private float saturation = 0.9f;
    [SerializeField, Range(0f, 1f)] private float value = 1f;

    [Header("Opacity")]
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.2f;

    private float blend;
    private bool idleStateApplied;

    void Awake()
    {
        if (overlayGraphic == null)
            overlayGraphic = GetComponent<Graphic>();

        if (overlayGraphic != null)
            overlayGraphic.raycastTarget = false;
    }

    void Start()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerControllerX>();

        SetOverlayColor(0f);
    }

    void Update()
    {
        if (overlayGraphic == null || player == null) return;

        bool active = player.IsRushActive;
        if (!active && blend <= 0.0001f)
        {
            if (!idleStateApplied)
            {
                SetOverlayColor(0f);
                idleStateApplied = true;
            }
            return;
        }

        idleStateApplied = false;
        float duration = active ? enterDuration : exitDuration;
        float delta = duration > 0.0001f ? Time.deltaTime / duration : 1f;
        blend = Mathf.Clamp01(blend + (active ? delta : -delta));

        AnimationCurve curve = active ? enterCurve : exitCurve;
        float curveBlend = curve != null ? Mathf.Clamp01(curve.Evaluate(blend)) : blend;

        // scaled time so hue motion pauses with gameplay pause
        float t = Time.time;
        float baseHue = Mathf.Repeat(t * baseHueCycleSpeed, 1f);
        float chaosA = (Mathf.PerlinNoise(t * chaosFrequency, 0.618f) - 0.5f) * 2f;
        float chaosB = Mathf.Sin(t * chaosFrequency * 1.73f);
        float hue = Mathf.Repeat(baseHue + (chaosA * 0.7f + chaosB * 0.3f) * chaosAmplitude, 1f);

        Color c = Color.HSVToRGB(hue, saturation, value);
        c.a = maxAlpha * curveBlend;
        overlayGraphic.color = c;
    }

    void SetOverlayColor(float alpha)
    {
        if (overlayGraphic == null) return;
        Color c = overlayGraphic.color;
        c.r = 1f;
        c.g = 1f;
        c.b = 1f;
        c.a = alpha;
        overlayGraphic.color = c;
    }
}
