Shader "Custom/SpeedLines"
{
    Properties
    {
        [HideInInspector] _MainTex ("Main Texture", 2D) = "white" {}
        _LineColor ("Line Color", Color) = (1, 1, 1, 0.6)
        _LineCount ("Line Count", Float) = 80
        _BaseThickness ("Base Thickness (at edge)", Range(0.01, 1)) = 0.5
        _TipThickness ("Tip Thickness (at center)", Range(0, 0.2)) = 0.02
        _InnerRadius ("Inner Radius (0=center, 1=edge)", Range(0, 1)) = 0.5
        _Intensity ("Intensity", Range(0, 1)) = 0
        _LengthVariation ("Length Variation", Range(0, 1)) = 0.4
        _ThicknessVariation ("Thickness Variation", Range(0, 1)) = 0.5
        _Jitter ("Jitter Amount", Range(0, 0.5)) = 0.2
        _JitterSpeed ("Jitter Speed", Range(0, 20)) = 8
        _UnscaledTime ("Unscaled Time", Float) = 0
        _AspectRatio ("Aspect Ratio", Float) = 1.777
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
        }

        Pass
        {
            Name "SpeedLines"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _LineColor;
            float _LineCount;
            float _BaseThickness;
            float _TipThickness;
            float _InnerRadius;
            float _Intensity;
            float _LengthVariation;
            float _ThicknessVariation;
            float _Jitter;
            float _JitterSpeed;
            float _UnscaledTime;
            float _AspectRatio;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float Hash(float n)
            {
                return frac(sin(n * 127.1) * 43758.5453);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                if (_Intensity < 0.001)
                    return half4(0, 0, 0, 0);

                float2 centeredUV = i.uv - 0.5;
                centeredUV.x *= _AspectRatio;

                float dist = length(centeredUV);
                float angle = atan2(centeredUV.y, centeredUV.x);

                float lineAngle = angle * _LineCount / 6.2831853;
                float lineIndex = floor(lineAngle);
                float lineFrac = frac(lineAngle);

                // per-spike random seeds
                float randLen = Hash(lineIndex);
                float randThick = Hash(lineIndex + 100);
                float randPhase = Hash(lineIndex + 200);  // random phase offset so spikes don't jitter in sync
                float randFreq = Hash(lineIndex + 300);   // random speed multiplier per spike

                // layered jitter — unscaled time so slow-mo doesn't freeze it
                // jitter is constant regardless of intensity so shape doesn't change with speed
                float phase = randPhase * 6.2831853;
                float baseFreq = _JitterSpeed * (0.6 + randFreq * 0.8);
                float uTime = _UnscaledTime;
                float jitterOffset = (
                    sin(uTime * baseFreq + phase) * 0.5 +
                    sin(uTime * baseFreq * 2.37 + phase * 1.7) * 0.3 +
                    sin(uTime * baseFreq * 4.13 + phase * 3.1) * 0.2
                ) * _Jitter;

                // base inner radius with static variation + animated jitter
                float spikeInner = _InnerRadius + randLen * _LengthVariation * (1.0 - _InnerRadius);
                spikeInner = saturate(spikeInner + jitterOffset);
                float effectiveInner = lerp(1.0, spikeInner, _Intensity);

                // per-angle distance to screen edge (ray-box intersection)
                float cosA = abs(cos(angle));
                float sinA = abs(sin(angle));
                float halfW = 0.5 * _AspectRatio;
                float halfH = 0.5;
                float maxDist = min(halfW / max(cosA, 0.0001), halfH / max(sinA, 0.0001));
                float normalizedDist = dist / maxDist;

                if (normalizedDist < effectiveInner || normalizedDist > 1.0)
                    return half4(0, 0, 0, 0);

                // t: 0 at tip, 1 at base
                float spikeRange = 1.0 - effectiveInner;
                float t = saturate((normalizedDist - effectiveInner) / max(spikeRange, 0.001));

                // thickness tapers: thick at base, thin point at center
                float thicknessMod = 1.0 - randThick * _ThicknessVariation;
                float allowedWidth = lerp(_TipThickness, _BaseThickness * thicknessMod, t);

                float distFromCenter = abs(lineFrac - 0.5) * 2.0;

                float spike = step(distFromCenter, allowedWidth);

                float alpha = spike * _Intensity * _LineColor.a * i.color.a;

                return half4(_LineColor.rgb, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
