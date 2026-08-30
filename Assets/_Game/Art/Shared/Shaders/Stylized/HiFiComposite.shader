Shader "Game/Stylized/HiFiComposite"
{
    // Final stylized composite.
    //
    // Confirmed formula (the dot pattern MULTIPLIES the bloom, it is never
    // added to the scene color):
    //
    //   DotRadiusEff   = DotRadius * 0.5 * BloomGate
    //   DotPattern     = HiFiBenDayDots(uv, density, DotRadiusEff, rotation, softness)
    //   BloomGate      = smoothstep(DotBloomMin, DotBloomMax, Luminance(bloom))
    //   StylizedBloom  = BloomTexture * DotPattern * BloomGate * BloomIntensity
    //   FinalColor     = SceneColor + StylizedBloom
    //
    //   - DotRadius is halved (0.5) to compensate the SDF normalization.
    //   - The effective radius is additionally scaled by BloomGate, so dots
    //     shrink in dim bloom areas and only reach full size where the bloom
    //     is bright (halftone "gated by light").
    //   - Dot edges are anti-aliased inside HiFiBenDayDots via fwidth().
    //
    //   Pass 0: composite         (scene + stylized bloom)
    //   Pass 1: bloom only        (debug - raw bloom chain output)
    //   Pass 2: dots only         (debug - BenDayPattern view)
    //   Pass 3: scene only        (debug - effect disabled preview)
    //   Pass 4: bloom gate        (debug - BloomGate view)
    //   Pass 5: stylized bloom    (debug - final Ben-Day bloom, no scene)
    //
    // The bloom texture is provided through the _HiFiBloomTexture global
    // slot (declared with UseGlobalTexture in the render graph).
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

        Pass // 0 - Composite: scene + bloom * dots * gate * intensity
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Includes/HiFiCommon.hlsl"

            TEXTURE2D_X(_HiFiBloomTexture);

            float _HiFiBloomIntensity;
            float _HiFiDotDensity;
            float _HiFiDotRadius;
            float _HiFiDotRotation;
            float _HiFiDotSoftness;
            float _HiFiDotBloomMin;
            float _HiFiDotBloomMax;
            float _HiFiPrintIntensity;
            float _HiFiPrintDensity;
            float _HiFiPrintShadowStrength;

            half4 FragComposite(Varyings input) : SV_Target
            {
                half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                half3 bloom = SAMPLE_TEXTURE2D_X(_HiFiBloomTexture, sampler_LinearClamp, input.texcoord).rgb;

                // Bloom luminance gate: only bloom brighter than DotBloomMin
                // may display the halftone; fully open above DotBloomMax.
                half bloomLuma = Luminance(bloom);
                half gate = smoothstep(_HiFiDotBloomMin, _HiFiDotBloomMax, bloomLuma);

                // Effective dot radius: halved to compensate the SDF
                // normalization, then scaled by the gate so dots shrink in
                // dim bloom regions.
                half effectiveRadius = _HiFiDotRadius * 0.5 * gate;

                // Ben-Day halftone mask in screen space (soft-edged, fwidth AA).
                half dots = HiFiBenDayDots(input.texcoord, _HiFiDotDensity, effectiveRadius, _HiFiDotRotation, _HiFiDotSoftness);

                // Stylized bloom = bloom * dots * gate * intensity (multiplicative,
                // never additive on its own).
                half3 stylizedBloom = bloom * dots * gate * _HiFiBloomIntensity;

                // Subtle comic-print ink over all shaded surfaces. Darker
                // colors grow larger dots, while highlights remain clean.
                half sceneLuma = Luminance(scene);
                half shadowWeight = saturate((1.0 - sceneLuma) * _HiFiPrintShadowStrength);
                half printRadius = lerp(0.08h, 0.52h, shadowWeight);
                half printDots = HiFiBenDayDots(
                    input.texcoord,
                    _HiFiPrintDensity,
                    printRadius,
                    _HiFiDotRotation + 0.3926991,
                    _HiFiDotSoftness);
                half printInk = printDots * shadowWeight * _HiFiPrintIntensity;

                half3 col = scene * (1.0h - printInk) + stylizedBloom;
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass // 1 - Raw bloom chain output (debug)
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBloomOnly
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_HiFiBloomTexture);

            float _HiFiBloomIntensity;

            half4 FragBloomOnly(Varyings input) : SV_Target
            {
                half3 bloom = SAMPLE_TEXTURE2D_X(_HiFiBloomTexture, sampler_LinearClamp, input.texcoord).rgb;
                return half4(bloom * _HiFiBloomIntensity, 1.0);
            }
            ENDHLSL
        }

        Pass // 2 - Ben-Day dot pattern only (debug)
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDotsOnly
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Includes/HiFiCommon.hlsl"

            float _HiFiDotDensity;
            float _HiFiDotRadius;
            float _HiFiDotRotation;
            float _HiFiDotSoftness;

            half4 FragDotsOnly(Varyings input) : SV_Target
            {
                // Pattern preview uses the same radius normalization as the
                // composite (DotRadius * 0.5) but no bloom gate.
                half dots = HiFiBenDayDots(input.texcoord, _HiFiDotDensity, _HiFiDotRadius * 0.5, _HiFiDotRotation, _HiFiDotSoftness);
                return half4(dots.xxx, 1.0);
            }
            ENDHLSL
        }

        Pass // 3 - Scene color only (debug)
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragSceneOnly
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragSceneOnly(Varyings input) : SV_Target
            {
                half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                return half4(scene, 1.0);
            }
            ENDHLSL
        }

        Pass // 4 - Bloom luminance gate only (debug)
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBloomGate
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_HiFiBloomTexture);

            float _HiFiDotBloomMin;
            float _HiFiDotBloomMax;

            half4 FragBloomGate(Varyings input) : SV_Target
            {
                half3 bloom = SAMPLE_TEXTURE2D_X(_HiFiBloomTexture, sampler_LinearClamp, input.texcoord).rgb;
                half bloomLuma = Luminance(bloom);
                half gate = smoothstep(_HiFiDotBloomMin, _HiFiDotBloomMax, bloomLuma);
                return half4(gate.xxx, 1.0);
            }
            ENDHLSL
        }

        Pass // 5 - Final stylized bloom, no scene (debug)
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragStylizedBloom
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Includes/HiFiCommon.hlsl"

            TEXTURE2D_X(_HiFiBloomTexture);

            float _HiFiBloomIntensity;
            float _HiFiDotDensity;
            float _HiFiDotRadius;
            float _HiFiDotRotation;
            float _HiFiDotSoftness;
            float _HiFiDotBloomMin;
            float _HiFiDotBloomMax;

            half4 FragStylizedBloom(Varyings input) : SV_Target
            {
                half3 bloom = SAMPLE_TEXTURE2D_X(_HiFiBloomTexture, sampler_LinearClamp, input.texcoord).rgb;
                half bloomLuma = Luminance(bloom);
                half gate = smoothstep(_HiFiDotBloomMin, _HiFiDotBloomMax, bloomLuma);
                half effectiveRadius = _HiFiDotRadius * 0.5 * gate;
                half dots = HiFiBenDayDots(input.texcoord, _HiFiDotDensity, effectiveRadius, _HiFiDotRotation, _HiFiDotSoftness);
                half3 stylizedBloom = bloom * dots * gate * _HiFiBloomIntensity;
                return half4(stylizedBloom, 1.0);
            }
            ENDHLSL
        }
    }
}
