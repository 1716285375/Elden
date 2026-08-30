Shader "Game/Stylized/HiFiToon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Header(Toon Lighting)]
        _ShadowColor("Shadow Color", Color) = (0.25, 0.28, 0.42, 1)
        _ShadowThreshold("Shadow Threshold", Range(0.0, 1.0)) = 0.45
        _ShadowSoftness("Shadow Softness", Range(0.001, 0.5)) = 0.08

        _SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularThreshold("Specular Threshold", Range(0.0, 1.0)) = 0.85

        [Header(Comic Print)]
        _SurfacePrintStrength("Surface Print Strength", Range(0.0, 1.0)) = 0.12
        _SurfacePrintDensity("Surface Print Density", Range(16.0, 192.0)) = 96.0

        [Header(Rim Light)]
        [HDR] _RimColor("Rim Color", Color) = (0.9, 0.55, 0.25, 1)
        _RimIntensity("Rim Intensity", Range(0.0, 4.0)) = 0.5

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0.03, 0.03, 0.04, 1)
        _OutlineWidthPixels("Outline Width (Pixels)", Range(0.0, 8.0)) = 2.0

        [Header(Surface)]
        [ToggleUI] _AlphaClip("Alpha Clip", Float) = 0.0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _BumpScale("Normal Scale", Float) = 1.0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        [Toggle(_EMISSION)] _EmissionEnabled("Emission", Float) = 0.0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap("Emission Map", 2D) = "white" {}

        // Blending state (opaque by default; transparents set _Surface = 1).
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
        }

        // ------------------------------------------------------------------
        //  Forward (toon) pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex HiFiToonVertex
            #pragma fragment HiFiToonFragment

            // Material keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT

            // Universal pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            // Unity defined keywords
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #define HIFI_TOON_INPUT
            #include "Includes/HiFiCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Includes/HiFiLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                #if defined(_NORMALMAP)
                half4 tangentWS : TEXCOORD3;
                #endif
                float fogCoord : TEXCOORD4;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings HiFiToonVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                #if defined(_NORMALMAP)
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
                #endif
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            half4 HiFiToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedoAlpha = HiFiSampleAlbedoAlpha(input.uv);
                half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;

                #if defined(_ALPHATEST_ON)
                    clip(albedoAlpha.a - _Cutoff);
                #endif

                half3 normalWS = normalize(input.normalWS);
                #if defined(_NORMALMAP)
                    half3 normalTS = HiFiSampleNormal(input.uv, _BumpScale);
                    float sign = input.tangentWS.w;
                    float3 bitangentWS = cross(normalWS, input.tangentWS.xyz) * sign;
                    normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, normalWS)));
                #endif

                float3 positionWS = input.positionWS;
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);

                // --- Main light: hard toon ramp with shadow support ---
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half ramp = HiFiToonRamp(saturate(dot(normalWS, mainLight.direction)), mainLight.shadowAttenuation);

                // Stylized diffuse: lerp albedo toward the shadow color.
                half3 diffuse = albedo * lerp(_ShadowColor.rgb, half3(1.0, 1.0, 1.0), ramp);

                half3 color = diffuse * mainLight.color;
                color += HiFiToonAmbient(normalWS, albedo);

                // --- Additional lights (fragment loop handles Forward and Forward+) ---
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
                        half aRamp = HiFiToonRamp(saturate(dot(normalWS, light.direction)), light.shadowAttenuation);
                        color += albedo * lerp(_ShadowColor.rgb, half3(1.0, 1.0, 1.0), aRamp)
                            * light.color * light.distanceAttenuation;
                        color += HiFiToonSpecular(normalWS, viewDirWS, light);
                    LIGHT_LOOP_END
                #endif

                // --- Specular + rim on the main light ---
                color += HiFiToonSpecular(normalWS, viewDirWS, mainLight) * ramp;
                color += HiFiToonRim(normalWS, viewDirWS) * ramp;

                // Material-space art control with screen-stable printed dots.
                // Shadow bands receive denser ink while lit areas stay clean.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half shadowWeight = 1.0h - ramp;
                half printRadius = lerp(0.08h, 0.48h, shadowWeight);
                half printDots = HiFiBenDayDots(
                    screenUV,
                    _SurfacePrintDensity,
                    printRadius,
                    0.7853982h,
                    0.04h);
                color *= 1.0h - printDots * shadowWeight * _SurfacePrintStrength;

                // --- Emission (gated by the _EmissionEnabled property float, so it
                // works without relying on keyword-variant state) ---
                color += HiFiSampleEmission(input.uv, _EmissionColor.rgb) * _EmissionEnabled;

                color = MixFog(color, input.fogCoord);
                return half4(color, albedoAlpha.a);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Shadow caster
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #define HIFI_TOON_INPUT
            #include "Includes/HiFiCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Depth only
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #define HIFI_TOON_INPUT
            #include "Includes/HiFiCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Depth + normals (used by SSAO / custom AO / camera normals)
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #define HIFI_TOON_INPUT
            #include "Includes/HiFiCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  HiFiOutlineStencilMask - marks the character region in stencil so
        //  the screen-space environment outline can skip characters.
        //  Renders the ORIGINAL geometry (no expansion), writes stencil only
        //  (ColorMask 0) - it can never paint the surface.
        // ------------------------------------------------------------------
        Pass
        {
            Name "HiFiOutlineStencilMask"
            Tags { "LightMode" = "HiFiOutlineStencilMask" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask 0

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex HiFiOutlineStencilMaskVert
            #pragma fragment HiFiOutlineStencilMaskFrag

            #pragma multi_compile_instancing

            #define HIFI_TOON_INPUT
            #include "Includes/HiFiOutline.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  HiFiOutline - inverted hull shell, drawn by HiFiOutlineRendererFeature.
        //  Constant-pixel width (see Includes/HiFiOutline.hlsl).
        // ------------------------------------------------------------------
        Pass
        {
            Name "HiFiOutline"
            Tags { "LightMode" = "HiFiOutline" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex HiFiOutlineVert
            #pragma fragment HiFiOutlineFrag

            #pragma multi_compile_instancing

            #define HIFI_TOON_INPUT
            #include "Includes/HiFiOutline.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
