#ifndef HIFI_OUTLINE_INCLUDED
#define HIFI_OUTLINE_INCLUDED

// ------------------------------------------------------------------
//  HiFiOutline.hlsl
//  Inverted-hull outline algorithm for HiFiToon.shader (CHARACTER outline).
//
//  Kept out of the main shader so HiFiToon.shader stays readable:
//    HiFiCommon   -> shared CBUFFER / utilities
//    HiFiLighting -> toon lighting
//    HiFiOutline  -> inverted hull (this file)
//
//  Width is expressed in SCREEN PIXELS (constant visual thickness):
//    clip position          = TransformObjectToHClip(...)
//    view-space normal .xy  = screen-space expand direction
//    pixelToNDC = 2 / _ScaledScreenParams.xy
//    positionCS.xy += direction * pixelToNDC * outlinePx * positionCS.w
//
//  Smooth outline normals: hard-edged meshes (cube, armor, low-poly) split
//  the hull when extruded with the raw per-vertex normal. A precomputed
//  smooth normal (averaged per shared position, stored in UV3 by the editor
//  tool) is used instead when available; otherwise we fall back to the
//  vertex normal.
// ------------------------------------------------------------------

#include "HiFiCommon.hlsl"

// Global multiplier driven by HiFiOutlineRendererFeature.m_GlobalWidthScale
// (set via SetGlobalFloat before the renderer list is drawn). NOT a material
// property, so it intentionally lives outside UnityPerMaterial.
float _HiFiOutlineGlobalScale;

struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float3 smoothNormalOS : TEXCOORD3; // UV3: precomputed smooth normal (0 if absent)
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

OutlineVaryings HiFiOutlineVert(OutlineAttributes input)
{
    OutlineVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    // Prefer the precomputed smooth normal (UV3) so hard edges extrude as one
    // continuous shell; fall back to the vertex normal when the mesh has no
    // smooth normal channel (its components are then 0).
    float3 outlineNormalOS = any(input.smoothNormalOS) ? input.smoothNormalOS : input.normalOS;

    // Object position -> clip position (positions are CPU-skinned for
    // SkinnedMeshRenderer, so this works for static and skinned meshes).
    float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);

    // Object normal -> world -> view, then take the screen-space xy as the
    // expand direction. This keeps the hull expanding roughly perpendicular
    // to the camera, which is what a constant-pixel silhouette needs.
    float3 normalWS = TransformObjectToWorldNormal(outlineNormalOS);
    float3 normalVS = TransformWorldToViewDir(normalWS);

    // Guard against zero-length screen directions (e.g. normals pointing
    // straight at / away from the camera) so rsqrt(0) is never evaluated.
    float2 direction = normalVS.xy;
    float lenSq = dot(direction, direction);
    lenSq = max(lenSq, 1e-6);
    direction *= rsqrt(lenSq);

    // Pixels -> NDC conversion (2 NDC units span the whole screen).
    float2 pixelToNDC = 2.0 / _ScaledScreenParams.xy;
    float outlinePx = _OutlineWidthPixels * _HiFiOutlineGlobalScale;

    // Expand in clip space: adding an NDC offset d requires clip.xy += d * w.
    positionCS.xy += direction * pixelToNDC * outlinePx * positionCS.w;

    // Depth correction (critical): the hull must sit ON the far side of the
    // surface along the view normal. Without this, the expanded shell shares
    // the exact same depth as the original surface and ZTest LEqual passes
    // everywhere, painting the whole object with the outline color. Pushing
    // clip.z along the view-space normal keeps the hull behind the surface on
    // front-facing regions (so the interior stays hidden) while the silhouette
    // rim (view normal ~perpendicular) keeps the surface depth and stays
    // visible as a clean edge.
    positionCS.z += (normalVS.z * rsqrt(lenSq)) * pixelToNDC.y * outlinePx * positionCS.w * 0.5;

    output.positionCS = positionCS;
    return output;
}

half4 HiFiOutlineFrag(OutlineVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    return _OutlineColor;
}

// ------------------------------------------------------------------
//  Stencil-mask variant: renders the ORIGINAL geometry (Cull Back,
//  ColorMask 0) so the whole character region is tagged with stencil 1.
//  It never expands and never writes color.
// ------------------------------------------------------------------
OutlineVaryings HiFiOutlineStencilMaskVert(OutlineAttributes input)
{
    OutlineVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}

half4 HiFiOutlineStencilMaskFrag(OutlineVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    return half4(0, 0, 0, 0);
}

#endif // HIFI_OUTLINE_INCLUDED
