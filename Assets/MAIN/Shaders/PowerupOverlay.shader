Shader "Custom/PowerupOverlay"
{
    Properties
    {
        _Color1 ("Knockback Color", Color) = (1, 0.89, 0, 1)
        _Color2 ("Smash Color", Color) = (1, 0, 0, 1)
        _Color3 ("Shield Color", Color) = (0, 0.77, 1, 1)
        _Color4 ("Giant Color", Color) = (0.11, 0.53, 0.22, 1)
        _Color5 ("Haunt Color", Color) = (0.86, 0, 1, 1)

        _Weight1 ("Knockback Weight", Range(0,1)) = 0
        _Weight2 ("Smash Weight", Range(0,1)) = 0
        _Weight3 ("Shield Weight", Range(0,1)) = 0
        _Weight4 ("Giant Weight", Range(0,1)) = 0
        _Weight5 ("Haunt Weight", Range(0,1)) = 0

        _NoiseScale ("Noise Scale", Float) = 2.0
        _FlowSpeed ("Flow Speed", Float) = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "PowerupOverlay"
            // multiply blend: output color multiplies what's already on screen
            // white (1,1,1) = no change, colored = tints the surface
            Blend DstColor Zero
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color1, _Color2, _Color3, _Color4, _Color5;
                half _Weight1, _Weight2, _Weight3, _Weight4, _Weight5;
                float _NoiseScale;
                float _FlowSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            // 3d hash for gradient noise
            float3 Hash3(float3 p)
            {
                p = float3(
                    dot(p, float3(127.1, 311.7, 74.7)),
                    dot(p, float3(269.5, 183.3, 246.1)),
                    dot(p, float3(113.5, 271.9, 124.6))
                );
                return frac(sin(p) * 43758.5453123) * 2.0 - 1.0;
            }

            float GradientNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(
                        lerp(dot(Hash3(i + float3(0,0,0)), f - float3(0,0,0)),
                             dot(Hash3(i + float3(1,0,0)), f - float3(1,0,0)), u.x),
                        lerp(dot(Hash3(i + float3(0,1,0)), f - float3(0,1,0)),
                             dot(Hash3(i + float3(1,1,0)), f - float3(1,1,0)), u.x), u.y),
                    lerp(
                        lerp(dot(Hash3(i + float3(0,0,1)), f - float3(0,0,1)),
                             dot(Hash3(i + float3(1,0,1)), f - float3(1,0,1)), u.x),
                        lerp(dot(Hash3(i + float3(0,1,1)), f - float3(0,1,1)),
                             dot(Hash3(i + float3(1,1,1)), f - float3(1,1,1)), u.x), u.y),
                    u.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half totalWeight = _Weight1 + _Weight2 + _Weight3 + _Weight4 + _Weight5;

                // no active powerups = output white = multiply does nothing
                if (totalWeight < 0.001)
                    return half4(1, 1, 1, 1);

                float t = _Time.y * _FlowSpeed;
                float3 samplePos = input.positionWS * _NoiseScale;

                // each powerup samples noise at a different offset so they flow independently
                float n1 = GradientNoise3D(samplePos + float3(t * 1.0,  t * 0.3,  t * -0.5)) * 0.5 + 0.5;
                float n2 = GradientNoise3D(samplePos + float3(t * -0.7, t * 1.1,  t * 0.4))  * 0.5 + 0.5;
                float n3 = GradientNoise3D(samplePos + float3(t * 0.4,  t * -0.8, t * 1.2))  * 0.5 + 0.5;
                float n4 = GradientNoise3D(samplePos + float3(t * -1.0, t * 0.5,  t * 0.7))  * 0.5 + 0.5;
                float n5 = GradientNoise3D(samplePos + float3(t * 0.6,  t * -0.4, t * -1.1)) * 0.5 + 0.5;

                // color alpha controls intensity — lower alpha = weaker tint
                half w1 = n1 * _Weight1 * _Color1.a;
                half w2 = n2 * _Weight2 * _Color2.a;
                half w3 = n3 * _Weight3 * _Color3.a;
                half w4 = n4 * _Weight4 * _Color4.a;
                half w5 = n5 * _Weight5 * _Color5.a;

                half sumW = w1 + w2 + w3 + w4 + w5;

                half3 tintColor = half3(1, 1, 1);
                if (sumW > 0.001)
                {
                    half invSum = 1.0 / sumW;
                    tintColor = _Color1.rgb * (w1 * invSum)
                              + _Color2.rgb * (w2 * invSum)
                              + _Color3.rgb * (w3 * invSum)
                              + _Color4.rgb * (w4 * invSum)
                              + _Color5.rgb * (w5 * invSum);

                    // blend toward white using alpha-scaled weight so opacity actually controls intensity
                    half maxAlpha = max(max(max(_Color1.a * _Weight1, _Color2.a * _Weight2),
                                            max(_Color3.a * _Weight3, _Color4.a * _Weight4)),
                                        _Color5.a * _Weight5);
                    tintColor = lerp(half3(1,1,1), tintColor, saturate(maxAlpha));
                }

                return half4(tintColor, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
