Shader "Game/Stylized/HiFiBloom"
{
    // Hi-Fi stylized bloom chain.
    //   Pass 0: extract (threshold) bright regions, downsample to half res
    //   Pass 1: downsample + blur (4 bilinear taps)
    //   Pass 2: upsample with additive blending (Blend One One) for the
    //           reconstruct step; the source is the lower-resolution level.
    //
    // Driven by HiFiStylizedPostPass through Render Graph blit passes.
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

        Pass // 0 - Extract
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragExtract
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _HiFiBloomThreshold;

            half4 FragExtract(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                half luma = Luminance(col.rgb);
                // Soft knee around the threshold so the extraction is not a hard clip.
                half bright = smoothstep(_HiFiBloomThreshold, _HiFiBloomThreshold * 1.3, luma);
                return half4(col.rgb * bright, 1.0);
            }
            ENDHLSL
        }

        Pass // 1 - Downsample + blur
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsample
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragDownsample(Varyings input) : SV_Target
            {
                float2 texel = _BlitTexture_TexelSize.xy * 0.5;
                half4 sum = 0.0;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-texel.x, -texel.y)) * 0.25;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2( texel.x, -texel.y)) * 0.25;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-texel.x,  texel.y)) * 0.25;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2( texel.x,  texel.y)) * 0.25;
                return sum;
            }
            ENDHLSL
        }

        Pass // 2 - Upsample (additive reconstruct)
        {
            Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragUpsample(Varyings input) : SV_Target
            {
                float2 texel = _BlitTexture_TexelSize.xy * 0.5;
                half4 low = 0.0;
                low += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-texel.x, -texel.y)) * 0.25;
                low += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2( texel.x, -texel.y)) * 0.25;
                low += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-texel.x,  texel.y)) * 0.25;
                low += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2( texel.x,  texel.y)) * 0.25;
                return low;
            }
            ENDHLSL
        }
    }
}
