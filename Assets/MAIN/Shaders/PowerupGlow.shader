Shader "Custom/PowerupGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 0, 0.3)
        _Intensity ("Glow Intensity", Float) = 3.0
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _AlphaMin ("Alpha Min", Range(0, 1)) = 0.05
        _AlphaMax ("Alpha Max", Range(0, 1)) = 0.4
        _FresnelMin ("Center Radius (Fresnel Min)", Range(0, 1)) = 0.0
        _FresnelPower ("Edge Sharpness", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "GlowPass"
            Blend One One // additive
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _PulseSpeed;
                float _AlphaMin;
                float _AlphaMax;
                float _FresnelMin;
                float _FresnelPower;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // no vertex displacement — shape stays clean
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(worldPos);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // fresnel: edges glow more, center is subtler
                float fresnel = 1.0 - saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                fresnel = pow(fresnel, _FresnelPower);

                // remap so center still has some glow based on FresnelMin
                fresnel = lerp(_FresnelMin, 1.0, fresnel);

                // pulsing alpha
                float pulse01 = (sin(_Time.y * _PulseSpeed) + 1.0) * 0.5;
                float alpha = lerp(_AlphaMin, _AlphaMax, pulse01);

                // HDR output so bloom picks it up
                float3 col = _Color.rgb * _Intensity * fresnel;
                float a = alpha * fresnel;

                return half4(col * a, a);
            }
            ENDHLSL
        }
    }
}
