#ifndef HIFI_LIGHTING_INCLUDED
#define HIFI_LIGHTING_INCLUDED

// ------------------------------------------------------------------
//  HiFiLighting.hlsl
//  Stylized toon lighting for HiFiToon.shader.
//
//  The model intentionally avoids PBR complexity:
//    - hard light/shadow separation via a threshold ramp
//    - thresholded Blinn-Phong specular
//    - thresholded fresnel rim light
//    - ambient probe contribution so shadowed regions stay readable
//
//  Material parameters come from the UnityPerMaterial CBUFFER declared
//  by HiFiCommon.hlsl (via HIFI_TOON_INPUT).
// ------------------------------------------------------------------

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "HiFiCommon.hlsl"

// Smooth light mask: 0 in shadow, 1 in light, stepped at _ShadowThreshold.
half HiFiToonRamp(half ndl, half shadowAttenuation)
{
    half lightMask = ndl * shadowAttenuation;
    return smoothstep(
        _ShadowThreshold - _ShadowSoftness,
        _ShadowThreshold + _ShadowSoftness,
        lightMask);
}

// Thresholded Blinn-Phong specular highlight (hard comic-style glint).
half3 HiFiToonSpecular(half3 normalWS, half3 viewDirWS, Light light)
{
    half3 halfDir = SafeNormalize(light.direction + viewDirWS);
    half ndh = saturate(dot(normalWS, halfDir));
    half spec = smoothstep(_SpecularThreshold - 0.06, _SpecularThreshold + 0.06, ndh);
    return _SpecularColor.rgb * light.color * spec
        * light.distanceAttenuation * light.shadowAttenuation;
}

// Thresholded fresnel rim light.
half3 HiFiToonRim(half3 normalWS, half3 viewDirWS)
{
    half ndv = saturate(dot(normalWS, viewDirWS));
    half rim = 1.0 - ndv;
    rim = smoothstep(0.35, 0.65, rim);
    return _RimColor.rgb * rim * _RimIntensity;
}

// Ambient (SH probe) contribution, kept dim so the stylized ramp stays dominant.
half3 HiFiToonAmbient(half3 normalWS, half3 albedo)
{
    half3 ambient = SampleSH(normalWS);
    return ambient * albedo * 0.35;
}

#endif // HIFI_LIGHTING_INCLUDED
