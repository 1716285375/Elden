Shader "Game/Stylized/HiFiAOLines"
{
    // Stylized screen-space AO / ink / hatching.
    //
    // Reads the camera depth + normals textures and derives:
    //   - contact / depth-discontinuity mask
    //   - crease (normal-discontinuity) mask -> ink lines
    //   - AO shading used to reveal hatching and contact darkening
    //
    // Pass 0: composite AO/ink/hatching over the scene color (_BlitTexture).
    // Pass 1: debug - masks only.
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass // 0 - Stylized AO + ink + hatching
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragAOLines
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Includes/HiFiCommon.hlsl"

            TEXTURE2D(_HiFiHatchingTexture);
            SAMPLER(sampler_HiFiHatchingTexture);
            TEXTURE2D(_HiFiAOMask);
            SAMPLER(sampler_HiFiAOMask);

            float _HiFiAOStrength;        // contact / AO darkening
            float _HiFiAOLineStrength;    // crease ink lines
            float _HiFiHatchingScale;     // hatching tiling
            float _HiFiHatchingStrength;  // how strongly hatching shows

            half4 FragAOLines(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 mask = SAMPLE_TEXTURE2D(_HiFiAOMask, sampler_HiFiAOMask, uv).rg;
                float depthDisc = saturate(mask.r);
                float crease = saturate(mask.g);

                // --- Stylized AO mask (contact-heavy, intentionally not a smooth SSAO) ---
                float aoMask = saturate(depthDisc * 0.55 + crease * 0.45);

                // --- Hatching revealed by the AO mask ---
                float2 hatchUV = uv * _HiFiHatchingScale;
                half hatchPattern = SAMPLE_TEXTURE2D(_HiFiHatchingTexture, sampler_HiFiHatchingTexture, hatchUV).r;
                float hatchAmt = hatchPattern * aoMask * _HiFiHatchingStrength;

                // --- Ink lines on creases ---
                float ink = crease * _HiFiAOLineStrength;

                // --- Apply over the scene ---
                half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float darken = saturate(aoMask * _HiFiAOStrength + ink);
                half3 col = scene * (1.0 - darken) * (1.0 - hatchAmt);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass // 1 - Debug: combined masks
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMasks
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Includes/HiFiScreenSpace.hlsl"

            half4 FragMasks(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texel = _CameraDepthTexture_TexelSize.xy;
                float depthDisc = saturate(HiFiRelativeDepthSobel(uv, texel) * 3.0);
                float crease = saturate(HiFiNormalEdge4(uv, texel) * 2.5);

                return half4(depthDisc, crease, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}
