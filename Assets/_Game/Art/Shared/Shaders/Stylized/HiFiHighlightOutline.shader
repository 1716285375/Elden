Shader "Game/Stylized/HiFiHighlightOutline"
{
    // Double-layer highlight silhouette (video-reference style).
    //
    // Completely independent from the Depth/Normal Screen Outline: this effect
    // works purely on an OBJECT MASK (rendered geometry of the highlight layer),
    // then produces two rings:
    //
    //   M = original mask (1 inside target, 0 outside)
    //   S = small dilation (InnerWidthPixels)
    //   L = large dilation (OuterWidthPixels)
    //
    //   InnerRing = saturate(S - M)   -> dark, close to the object
    //   OuterRing = saturate(L - S)   -> bright, far from the object
    //
    // Both rings keep fractional coverage (anti-aliased, distance-weighted
    // dilation), so edges stay smooth; the object interior is never tinted.
    //
    //   Pass 0: object mask  (renders highlight-layer geometry as white, R8)
    //   Pass 1: dual dilation (R = small, G = large)
    //   Pass 2: composite (rings + color over the scene)
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        // --------------------------------------------------------------
        // Pass 0 - Object Mask (used via renderer-list override shader)
        // --------------------------------------------------------------
        Pass
        {
            Name "HighlightMask"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask RG

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex HighlightMaskVert
            #pragma fragment HighlightMaskFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct MaskAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct MaskVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            MaskVaryings HighlightMaskVert(MaskAttributes input)
            {
                MaskVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 HighlightMaskFrag(MaskVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return half4(1.0, 1.0, 1.0, 1.0);
            }
            ENDHLSL
        }

        // --------------------------------------------------------------
        // Pass 1 - Dual anti-aliased dilation
        // --------------------------------------------------------------
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDilate
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_HiFiHighlightMask);
            SAMPLER(sampler_HiFiHighlightMask);

            float _InnerWidthPixels;
            float _OuterWidthPixels;

            half4 FragDilate(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texel = 1.0 / _ScreenParams.xy;

                float rInner = max(_InnerWidthPixels, 0.5);
                float rOuter = max(_OuterWidthPixels, 0.5);
                int rMax = (int)ceil(max(rInner, rOuter)) + 1;

                float small = 0.0;
                float large = 0.0;
                for (int y = -rMax; y <= rMax; ++y)
                {
                    for (int x = -rMax; x <= rMax; ++x)
                    {
                        float dist = length(float2((float)x, (float)y));
                        float m = SAMPLE_TEXTURE2D(_HiFiHighlightMask, sampler_HiFiHighlightMask,
                            uv + float2(x, y) * texel).r;

                        float covSmall = 1.0 - smoothstep(rInner - 0.75, rInner + 0.75, dist);
                        float covLarge = 1.0 - smoothstep(rOuter - 0.75, rOuter + 0.75, dist);

                        small = max(small, m * covSmall);
                        large = max(large, m * covLarge);
                    }
                }
                return half4(small, large, 0.0, 1.0);
            }
            ENDHLSL
        }

        // --------------------------------------------------------------
        // Pass 2 - Composite (rings + color)
        // --------------------------------------------------------------
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_HiFiHighlightMask);
            SAMPLER(sampler_HiFiHighlightMask);
            TEXTURE2D(_HiFiHighlightDilated);
            SAMPLER(sampler_HiFiHighlightDilated);

            half4 _InnerColor;
            half4 _OuterColor;
            int _HiFiHighlightDebug;    // 0 off, 1 mask, 2 small dilate, 3 outer ring

            half4 FragComposite(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                float m = SAMPLE_TEXTURE2D(_HiFiHighlightMask, sampler_HiFiHighlightMask, uv).r;
                float2 dl = SAMPLE_TEXTURE2D(_HiFiHighlightDilated, sampler_HiFiHighlightDilated, uv).rg;
                float s = dl.r; // small dilation
                float l = dl.g; // large dilation

                float innerRing = saturate(s - m);
                float outerRing = saturate(l - s);

                if (_HiFiHighlightDebug == 1) return half4(m.xxx, 1.0);
                if (_HiFiHighlightDebug == 2) return half4(s.xxx, 1.0);
                if (_HiFiHighlightDebug == 3) return half4(outerRing.xxx, 1.0);
                if (_HiFiHighlightDebug == 4) return half4(scene, 1.0);

                // Outer bright ring first, then inner dark ring.
                half3 col = lerp(scene, _OuterColor.rgb, outerRing);
                col = lerp(col, _InnerColor.rgb, innerRing);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
