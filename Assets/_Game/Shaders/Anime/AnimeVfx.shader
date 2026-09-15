Shader "ZZ/Anime/VFX"
{
    Properties
    {
        _BaseMap("Shape (Alpha)", 2D) = "white" {}
        [HDR] _BaseColor("Body Color", Color) = (1,0.65,0.2,1)
        [HDR] _CoreColor("Core Color", Color) = (1.5,1.4,1,1)
        _Threshold("Shape Threshold", Range(0,0.9)) = 0.12
        _Softness("Edge Softness", Range(0.001,0.4)) = 0.07
        _CoreThreshold("Core Threshold", Range(0,1)) = 0.65
        _Dissolve("Dissolve", Range(0,1)) = 0
        _Flow("UV Flow XY", Vector) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "AnimeVFX"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _CoreColor;
                float4 _Flow;
                half _Threshold;
                half _Softness;
                half _CoreThreshold;
                half _Dissolve;
                half _DstBlend;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half fog : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap) + _Flow.xy * _Time.y;
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                half edge = max(_Softness, fwidth(mask));
                half threshold = lerp(_Threshold, 1.0h + edge, _Dissolve);
                half alpha = smoothstep(threshold, threshold + edge, mask);
                half core = smoothstep(_CoreThreshold, _CoreThreshold + edge, mask);
                half3 color = lerp(_BaseColor.rgb, _CoreColor.rgb, core) * input.color.rgb;
                // Additive particles must fade to black in fog, not add the fog color to the scene.
                color = _DstBlend < 1.5h ? MixFogColor(color, half3(0,0,0), input.fog) : MixFog(color, input.fog);
                return half4(color, alpha * input.color.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
