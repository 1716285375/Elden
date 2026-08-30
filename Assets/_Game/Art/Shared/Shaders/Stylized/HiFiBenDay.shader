Shader "Game/Stylized/HiFiBenDay"
{
    // Ben-Day halftone dot mask renderer (screen space).
    //
    // Pass 0 outputs ONLY the dot pattern (1 inside a dot, 0 outside) and is
    // used both for validation (Frame Debugger) and as the debug views
    // (DotsOnly / BenDayPattern). The final composite lives in
    // HiFiComposite.shader: the dot pattern MULTIPLIES the bloom, it is never
    // added to the scene color directly.
    //
    // Parameter semantics (see Includes/HiFiCommon.hlsl -> HiFiBenDayDots):
    //   _HiFiDotDensity  -> screen-UV grid division count (cells across screen)
    //   _HiFiDotRadius   -> dot radius as a fraction of half a cell (coverage)
    //   _HiFiDotRotation -> rotation of the pattern coordinate system only
    //   _HiFiDotSoftness -> smoothstep edge width (0 = hard step)
    //
    // The shared dot function lives in Includes/HiFiCommon.hlsl.
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

        Pass // 0 - Dot mask
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBenDay
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Includes/HiFiCommon.hlsl"

            float _HiFiDotDensity;
            float _HiFiDotRadius;
            float _HiFiDotRotation;
            float _HiFiDotSoftness;

            half4 FragBenDay(Varyings input) : SV_Target
            {
                // Pattern preview matches the composite radius normalization
                // (DotRadius * 0.5); no bloom gate here (pattern is scene-independent).
                half dots = HiFiBenDayDots(input.texcoord, _HiFiDotDensity, _HiFiDotRadius * 0.5, _HiFiDotRotation, _HiFiDotSoftness);
                return half4(dots.xxx, 1.0);
            }
            ENDHLSL
        }
    }
}
