Shader "ZZ/Anime/Telegraph"
{
    Properties
    {
        [HDR] _Color("Warning Color", Color) = (1,0.35,0.08,0.8)
        _Progress("Charge Progress", Range(0,1)) = 0.5
        _Thickness("Ring Thickness", Range(0.005,0.2)) = 0.025
        _FillAlpha("Interior Opacity", Range(0,1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Telegraph"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Progress;
                float _Thickness;
                float _FillAlpha;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2 - 1);
                float aa = max(fwidth(radius), 0.001);
                float border = 1 - smoothstep(_Thickness, _Thickness + aa, abs(radius - 0.94));
                float charge = (1 - smoothstep(_Thickness, _Thickness + aa,
                    abs(radius - 0.94 * _Progress))) * step(0.001, _Progress);
                float fill = (1 - smoothstep(0.94 - aa, 0.94, radius)) * _FillAlpha;
                return half4(_Color.rgb, saturate(max(border, charge) + fill) * _Color.a);
            }
            ENDHLSL
        }
    }
}
