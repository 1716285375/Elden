#ifndef HIFI_COMMON_INCLUDED
#define HIFI_COMMON_INCLUDED

// ------------------------------------------------------------------
//  HiFiCommon.hlsl
//  Shared math + stylized pattern functions for the Hi-Fi comic
//  rendering suite (HiFiToon, HiFiBloom, HiFiBenDay, HiFiComposite,
//  HiFiAOLines).
//
//  Toon material input (textures + UnityPerMaterial CBUFFER + sample
//  helpers) is opt-in via HIFI_TOON_INPUT, which HiFiToon.shader
//  defines before including this file. Full-screen shaders can include
//  this file without that define and only get the pattern helpers.
// ------------------------------------------------------------------

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#ifdef HIFI_TOON_INPUT

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);

// NOTE: All materials using HiFiToon share this exact layout (SRP Batcher contract).
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _ShadowColor;
    half _ShadowThreshold;
    half _ShadowSoftness;
    half4 _SpecularColor;
    half _SpecularThreshold;
    half _SurfacePrintStrength;
    half _SurfacePrintDensity;
    half4 _RimColor;
    half _RimIntensity;
    half4 _OutlineColor;
    half _OutlineWidthPixels;
    half _Cutoff;
    half _BumpScale;
    half _EmissionEnabled;
    half4 _EmissionColor;
    half _Surface;
    half _AlphaClip;
    half4 _EmissionMap_ST;
CBUFFER_END

// Small material sample helpers (mirrors URP SurfaceInput.hlsl, kept
// local so HiFiToon does not need to pull in the full Lit input chain).
half HiFiAlpha(half albedoAlpha, half4 color, half cutoff)
{
    half alpha = albedoAlpha * color.a;
    alpha = AlphaDiscard(alpha, cutoff);
    return alpha;
}

half4 HiFiSampleAlbedoAlpha(float2 uv)
{
    return half4(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv));
}

half3 HiFiSampleNormal(float2 uv, half scale)
{
#ifdef _NORMALMAP
    half4 n = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv);
    #if BUMP_SCALE_NOT_SUPPORTED
        return UnpackNormal(n);
    #else
        return UnpackNormalScale(n, scale);
    #endif
#else
    return half3(0.0h, 0.0h, 1.0h);
#endif
}

half3 HiFiSampleEmission(float2 uv, half3 emissionColor)
{
    return SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * emissionColor;
}

#endif // HIFI_TOON_INPUT

// ------------------------------------------------------------------
//  Ben-Day (halftone) dot mask in screen space.
//
//  Parameter semantics:
//    density   -> number of dot cells across the screen (e.g. 48). It only
//                 controls how finely the screen UV is divided into a grid;
//                 it does NOT change the dot fill ratio inside a cell.
//    radius    -> dot radius expressed as a fraction of HALF a cell
//                 (0 = no dot, ~1 = dot fills the whole cell). Because it is
//                 relative to the cell, the absolute dot size on screen
//                 shrinks when density grows.
//    rotation  -> rotates the pattern coordinate system BEFORE tiling. A
//                 uniform grid is rotation-invariant in coverage, so rotation
//                 only re-orients the pattern, it does not change intensity.
//    softness  -> extra smoothstep falloff width on top of the pixel-width
//                 fwidth band (0 = pure fwidth anti-aliasing).
//
//  Anti-aliasing: the dot mask is treated as a signed-distance field and
//  rasterized with a smoothstep whose width is derived from fwidth(), so the
//  edge stays exactly one pixel regardless of density/radius/zoom.
// ------------------------------------------------------------------
half HiFiBenDayDots(float2 screenUV, half density, half radius, half rotation, half softness)
{
    // Square cells in pixels: scale the x-UV by the screen aspect ratio so
    // dots stay circular on any resolution instead of becoming ellipses.
    float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
    float2 aUV = float2(screenUV.x * aspect, screenUV.y);

    half c = cos(rotation);
    half s = sin(rotation);

    // Rotate the grid coordinate system, then tile it into cells.
    float2 rotated = float2(dot(aUV, float2(c, -s)), dot(aUV, float2(s, c)));
    float2 cell = frac(rotated * density);

    // Dot SDF: distance from the cell center, 0 at center, 1 at cell edge.
    float distFromCenter = length(cell - 0.5) * 2.0;

    // Anti-aliased edge: fwidth of the SDF gives a one-pixel transition band
    // on any density; softness can add a wider, softer falloff on top.
    float aa = fwidth(distFromCenter);
    half edge = max(aa, softness);
    half inner = max(0.0, radius - edge * 0.5);
    half outer = max(inner + 0.001, radius + edge * 0.5);
    half dotMask = 1.0 - smoothstep(inner, outer, distFromCenter);
    return dotMask;
}

// ------------------------------------------------------------------
//  Ink line intensity from a screen-space normal discontinuity.
//  normalA / normalB are world-space normals; returns 1 on a hard
//  crease, 0 on a flat surface.
// ------------------------------------------------------------------
half HiFiNormalCrease(half3 normalA, half3 normalB)
{
    half ndn = saturate(dot(normalA, normalB));
    return 1.0 - ndn;
}

#endif // HIFI_COMMON_INCLUDED
