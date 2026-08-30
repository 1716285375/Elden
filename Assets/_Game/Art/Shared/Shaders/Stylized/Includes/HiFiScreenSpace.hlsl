#ifndef HIFI_SCREEN_SPACE_INCLUDED
#define HIFI_SCREEN_SPACE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

float HiFiLinearEyeDepth(float2 uv)
{
    return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
}

float HiFiRelativeDepthSobel(float2 uv, float2 texelSize)
{
    float depthCenter = HiFiLinearEyeDepth(uv);
    float topLeft = HiFiLinearEyeDepth(uv - texelSize);
    float top = HiFiLinearEyeDepth(uv - float2(0.0, texelSize.y));
    float topRight = HiFiLinearEyeDepth(uv + float2(texelSize.x, -texelSize.y));
    float left = HiFiLinearEyeDepth(uv - float2(texelSize.x, 0.0));
    float right = HiFiLinearEyeDepth(uv + float2(texelSize.x, 0.0));
    float bottomLeft = HiFiLinearEyeDepth(uv + float2(-texelSize.x, texelSize.y));
    float bottom = HiFiLinearEyeDepth(uv + float2(0.0, texelSize.y));
    float bottomRight = HiFiLinearEyeDepth(uv + texelSize);

    float gradientX = (topRight + 2.0 * right + bottomRight) -
        (topLeft + 2.0 * left + bottomLeft);
    float gradientY = (bottomLeft + 2.0 * bottom + bottomRight) -
        (topLeft + 2.0 * top + topRight);
    return length(float2(gradientX, gradientY)) / max(depthCenter, 0.001);
}

float HiFiNormalEdge4(float2 uv, float2 texelSize)
{
    half3 center = normalize(SampleSceneNormals(uv));
    half3 left = normalize(SampleSceneNormals(uv - float2(texelSize.x, 0.0)));
    half3 right = normalize(SampleSceneNormals(uv + float2(texelSize.x, 0.0)));
    half3 up = normalize(SampleSceneNormals(uv - float2(0.0, texelSize.y)));
    half3 down = normalize(SampleSceneNormals(uv + float2(0.0, texelSize.y)));

    float edgeLeft = 1.0 - saturate(dot(center, left));
    float edgeRight = 1.0 - saturate(dot(center, right));
    float edgeUp = 1.0 - saturate(dot(center, up));
    float edgeDown = 1.0 - saturate(dot(center, down));
    return max(max(edgeLeft, edgeRight), max(edgeUp, edgeDown));
}

#endif
