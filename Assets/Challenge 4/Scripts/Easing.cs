using UnityEngine;

public enum EasingType
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    EaseInCubic,
    EaseOutCubic,
    EaseInOutCubic
}

public static class Easing
{
    public static float Evaluate(EasingType type, float t)
    {
        t = Mathf.Clamp01(t);

        switch (type)
        {
            case EasingType.Linear:
                return t;
            case EasingType.EaseIn:
                return t * t;
            case EasingType.EaseOut:
                return 1f - (1f - t) * (1f - t);
            case EasingType.EaseInOut:
                return t * t * (3f - 2f * t);
            case EasingType.EaseInCubic:
                return t * t * t;
            case EasingType.EaseOutCubic:
                float inv = 1f - t;
                return 1f - inv * inv * inv;
            case EasingType.EaseInOutCubic:
                return t < 0.5f
                    ? 4f * t * t * t
                    : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
            default:
                return t;
        }
    }
}
