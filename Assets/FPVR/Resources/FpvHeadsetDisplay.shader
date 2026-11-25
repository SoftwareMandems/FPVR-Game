Shader "FPVR/Headset Display"
{
    Properties
    {
        _MainTex ("Drone Feed", 2D) = "black" {}
        _BorderStrength ("Comfort Border Strength", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _BorderStrength;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 feed = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                float2 centerDistance = abs(input.uv * 2.0 - 1.0);
                float edge = max(centerDistance.x, centerDistance.y);
                float shade = smoothstep(0.78, 1.0, edge) * _BorderStrength;
                return half4(feed * (1.0 - shade), 1.0);
            }
            ENDHLSL
        }
    }
}
