Shader "Game/Stylized/HiFiScreenOutline"
{
    // Screen-space outline, phase-4 quality pass.
    //
    // THREE independent passes:
    //   Pass 0 Edge Detect : fractional-coverage masks (R=silhouette, G=crease)
    //   Pass 1 Dilation    : separable anti-aliased, per-role dilation
    //   Pass 2 Composite   : lerp scene -> outline color by coverage
    //
    // Anti-aliasing strategy:
    //   - Detection keeps 0..1 coverage (fwidth-smoothed smoothstep), never 0/1.
    //   - Depth silhouette uses 8-tap Sobel on relative eye depth.
    //   - Normal crease uses the angle measure 1 - dot(normalized normals).
    //   - Dilation weights neighbours by distance (fractional edge falloff).
    //   - All mask sampling uses LinearClamp.
    //
    // Debug views are always grayscale.
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

        // --------------------------------------------------------------
        // Pass 0 - Edge Detect (1px neighbours, fractional coverage)
        // --------------------------------------------------------------
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDetect
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Includes/HiFiScreenSpace.hlsl"

            float _DepthThreshold;
            float _DepthSoftness;
            float _NormalThreshold;
            float _NormalSoftness;

            half4 FragDetect(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texel = _CameraDepthTexture_TexelSize.xy;

                float dC = HiFiLinearEyeDepth(uv);
                float farPlane = _ProjectionParams.z;
                if (dC > farPlane * 0.999)
                {
                    return half4(0.0, 0.0, 0.0, 1.0);
                }

                // ---- Depth silhouette: shared 8-tap Sobel on relative eye depth ----
                float relMag = HiFiRelativeDepthSobel(uv, texel);

                float depthAA = max(fwidth(relMag), 1e-5);
                float silhouette = smoothstep(
                    _DepthThreshold - depthAA,
                    _DepthThreshold + _DepthSoftness + depthAA,
                    relMag);

                // ---- Normal crease: angle measure (kept smooth on surfaces) ----
                float normalDelta = HiFiNormalEdge4(uv, texel);

                float normalAA = max(fwidth(normalDelta), 1e-5);
                float crease = smoothstep(
                    _NormalThreshold - normalAA,
                    _NormalThreshold + _NormalSoftness + normalAA,
                    normalDelta);

                // R = silhouette coverage, G = crease coverage (0..1 grayscale).
                return half4(silhouette, crease, 0.0, 1.0);
            }
            ENDHLSL
        }

        // --------------------------------------------------------------
        // Pass 1 - Anti-aliased Dilation with per-role width
        // --------------------------------------------------------------
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDilate
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_HiFiScreenMaskInput);
            SAMPLER(sampler_HiFiScreenMaskInput);

            float _SilhouetteWidthPixels;
            float _CreaseWidthPixels;
            float2 _DilationAxis;

            half4 FragDilate(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texel = 1.0 / _ScreenParams.xy;

                float rSil = max(_SilhouetteWidthPixels, 0.5);
                float rCr = max(_CreaseWidthPixels, 0.5);
                int rMax = min((int)ceil(max(rSil, rCr)) + 1, 9);

                float dilSil = 0.0;
                float dilCrease = 0.0;

                // This pass is executed horizontally and vertically. The two
                // 1D passes cap the maximum work at 38 taps instead of the
                // previous 19x19 nested search (361 taps per pixel).
                for (int i = -rMax; i <= rMax; ++i)
                {
                    float dist = abs((float)i);
                    float2 off = _DilationAxis * i * texel;
                    float2 m = SAMPLE_TEXTURE2D(
                        _HiFiScreenMaskInput,
                        sampler_HiFiScreenMaskInput,
                        uv + off).rg;

                    float covSil = 1.0 - smoothstep(rSil - 0.75, rSil + 0.75, dist);
                    float covCr = 1.0 - smoothstep(rCr - 0.75, rCr + 0.75, dist);
                    dilSil = max(dilSil, m.r * covSil);
                    dilCrease = max(dilCrease, m.g * covCr);
                }
                return half4(dilSil, dilCrease, 0.0, 1.0);
            }
            ENDHLSL
        }

        // --------------------------------------------------------------
        // Pass 2 - Composite + debug views
        // --------------------------------------------------------------
        Pass
        {
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_HiFiScreenRawMask);
            SAMPLER(sampler_HiFiScreenRawMask);
            TEXTURE2D(_HiFiScreenDilatedMask);
            SAMPLER(sampler_HiFiScreenDilatedMask);

            int _HiFiScreenDebugMode;   // 0 Combined, 1 DepthEdge, 2 NormalEdge, 3 RawEdge, 4 DilatedEdge
            float _SilhouetteStrength;
            float _CreaseStrength;
            half4 _OutlineColor;
            int _OutlineColorMode;
            half4 _DarkOutlineColor;
            half4 _LightOutlineColor;
            float _AutoContrastThreshold;

            half4 FragComposite(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                float2 rawMask = saturate(SAMPLE_TEXTURE2D(_HiFiScreenRawMask, sampler_HiFiScreenRawMask, uv).rg);
                float2 dilated = saturate(SAMPLE_TEXTURE2D(_HiFiScreenDilatedMask, sampler_HiFiScreenDilatedMask, uv).rg);

                float silhouette = dilated.r;
                float creaseRaw = dilated.g;
                float mask = saturate(
                    silhouette * _SilhouetteStrength +
                    creaseRaw * (1.0 - silhouette) * _CreaseStrength);

                // Debug views: grayscale mask output only.
                if (_HiFiScreenDebugMode == 1) return half4(silhouette.xxx, 1.0);
                if (_HiFiScreenDebugMode == 2) return half4(creaseRaw.xxx, 1.0);
                if (_HiFiScreenDebugMode == 3)
                {
                    float rawCombined = max(rawMask.r, rawMask.g);
                    return half4(rawCombined.xxx, 1.0);
                }
                if (_HiFiScreenDebugMode == 4) return half4(mask.xxx, 1.0);

                // Outline color by mode.
                half3 outlineColor;
                if (_OutlineColorMode == 1)
                {
                    // Stable luminance switch with a narrow anti-flicker band.
                    float sceneLum = dot(scene, float3(0.2126, 0.7152, 0.0722));
                    float useDark = smoothstep(
                        _AutoContrastThreshold - 0.05,
                        _AutoContrastThreshold + 0.05,
                        sceneLum);
                    outlineColor = lerp(_LightOutlineColor.rgb, _DarkOutlineColor.rgb, useDark);
                }
                else if (_OutlineColorMode == 2)
                {
                    outlineColor = 1.0 - scene;
                }
                else
                {
                    outlineColor = _OutlineColor.rgb;
                }

                // mask == 0 MUST yield scene exactly.
                half3 col = lerp(scene, outlineColor, saturate(mask));
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
