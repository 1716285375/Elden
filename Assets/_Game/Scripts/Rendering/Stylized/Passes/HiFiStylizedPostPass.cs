using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static UnityEngine.Rendering.RenderGraphModule.Util.RenderGraphUtils;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Stylized post-processing pass (Render Graph).
    ///
    /// Logical pipeline:
    ///   CameraColor -> Bloom Extract -> Downsample -> Blur -> Upsample
    ///     -> BloomTexture -> Ben-Day filter -> new CameraColor
    ///
    /// The pass follows Unity 6.3's Render Graph camera-color swap pattern:
    /// it never reads and writes the same texture and never copies back.
    /// </summary>
    public sealed class HiFiStylizedPostPass : ScriptableRenderPass
    {
        private static readonly int s_BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int s_BloomTextureId = Shader.PropertyToID("_HiFiBloomTexture");
        private static readonly int s_BloomThresholdId = Shader.PropertyToID("_HiFiBloomThreshold");
        private static readonly int s_BloomIntensityId = Shader.PropertyToID("_HiFiBloomIntensity");
        private static readonly int s_DotDensityId = Shader.PropertyToID("_HiFiDotDensity");
        private static readonly int s_DotRadiusId = Shader.PropertyToID("_HiFiDotRadius");
        private static readonly int s_DotRotationId = Shader.PropertyToID("_HiFiDotRotation");
        private static readonly int s_DotSoftnessId = Shader.PropertyToID("_HiFiDotSoftness");
        private static readonly int s_DotBloomMinId = Shader.PropertyToID("_HiFiDotBloomMin");
        private static readonly int s_DotBloomMaxId = Shader.PropertyToID("_HiFiDotBloomMax");
        private static readonly int s_PrintIntensityId = Shader.PropertyToID("_HiFiPrintIntensity");
        private static readonly int s_PrintDensityId = Shader.PropertyToID("_HiFiPrintDensity");
        private static readonly int s_PrintShadowStrengthId = Shader.PropertyToID("_HiFiPrintShadowStrength");

        private const string k_OutputName = "_HiFiStylizedCameraColor";
        private const string k_BloomMipPrefix = "_HiFiBloomMip";

        // Densities above this produce sub-pixel dots (visual aliasing noise);
        // the volume allows up to 256, so we clamp here and warn once.
        private const float k_MaxDotDensity = 128.0f;
        private static bool s_DensityClampWarned;

        private const int k_ExtractPass = 0;
        private const int k_DownsamplePass = 1;
        private const int k_UpsamplePass = 2;
        private const int k_CompositePass = 0;
        private const int k_BloomOnlyPass = 1;
        private const int k_DotsOnlyPass = 2;
        private const int k_SceneOnlyPass = 3;
        private const int k_BloomGatePass = 4;
        private const int k_StylizedBloomPass = 5;

        private readonly HiFiStylizedRendererFeature.Settings m_Settings;

        private readonly Material m_BloomMaterial;
        private readonly Material m_BenDayMaterial;
        private readonly Material m_CompositeMaterial;

        /// <summary>
        /// Creates a new stylized post pass.
        /// </summary>
        /// <param name="settings">The feature settings that drive this pass.</param>
        /// <param name="bloomMaterial">Bloom pyramid material.</param>
        /// <param name="benDayMaterial">Ben-Day debug material.</param>
        /// <param name="compositeMaterial">Final composite material.</param>
        public HiFiStylizedPostPass(
            HiFiStylizedRendererFeature.Settings settings,
            Material bloomMaterial,
            Material benDayMaterial,
            Material compositeMaterial)
        {
            m_Settings = settings;
            m_BloomMaterial = bloomMaterial;
            m_BenDayMaterial = benDayMaterial;
            m_CompositeMaterial = compositeMaterial;
            renderPassEvent = settings.m_RenderEvent;
            profilingSampler = new ProfilingSampler(settings.m_ProfilerTag);
            requiresIntermediateTexture = true;
        }

        /// <inheritdoc />
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_BloomMaterial == null || m_BenDayMaterial == null || m_CompositeMaterial == null)
            {
                return;
            }

            HiFiStyleVolume volume = VolumeManager.instance.stack.GetComponent<HiFiStyleVolume>();
            if (volume == null || !volume.Enable.value)
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (resourceData.isActiveTargetBackBuffer)
            {
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;

            // --- Per-frame material parameters (from the volume) ---
            // Clamp extreme densities so the pattern never degenerates into
            // per-pixel aliasing noise (sub-pixel dots).
            float dotDensity = Mathf.Min(volume.DotDensity.value, k_MaxDotDensity);
            if (volume.DotDensity.value > k_MaxDotDensity && !s_DensityClampWarned)
            {
                s_DensityClampWarned = true;
                Debug.LogWarning($"HiFiStyleVolume.DotDensity ({volume.DotDensity.value}) clamped to {k_MaxDotDensity} " +
                    "to avoid sub-pixel Ben-Day dots. Lower the value in the Volume profile.");
            }

            MaterialPropertyBlock bloomProperties = new MaterialPropertyBlock();
            bloomProperties.SetFloat(s_BloomThresholdId, volume.BloomThreshold.value);

            MaterialPropertyBlock dotProperties = CreateDotProperties(volume, dotDensity);
            MaterialPropertyBlock compositeProperties = CreateDotProperties(volume, dotDensity);
            compositeProperties.SetFloat(s_BloomIntensityId, volume.BloomIntensity.value);
            compositeProperties.SetFloat(s_DotBloomMinId, volume.DotBloomMin.value);
            compositeProperties.SetFloat(s_DotBloomMaxId, volume.DotBloomMax.value);
            compositeProperties.SetFloat(s_PrintIntensityId, volume.PrintIntensity.value);
            compositeProperties.SetFloat(s_PrintDensityId, volume.PrintDensity.value);
            compositeProperties.SetFloat(s_PrintShadowStrengthId, volume.PrintShadowStrength.value);

            // --- Bloom pyramid ---
            TextureHandle bloomTexture = BuildBloomPyramid(renderGraph, source, cameraData, bloomProperties);

            TextureDesc outputDesc = renderGraph.GetTextureDesc(source);
            outputDesc.name = k_OutputName;
            outputDesc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(outputDesc);

            // --- Final composite (or debug view) ---
            switch (m_Settings.m_DebugMode)
            {
                case HiFiStylizedRendererFeature.DebugMode.Composite:
                    CompositePass(renderGraph, source, destination, bloomTexture, k_CompositePass, compositeProperties);
                    break;
                case HiFiStylizedRendererFeature.DebugMode.BloomOnly:
                    CompositePass(renderGraph, source, destination, bloomTexture, k_BloomOnlyPass, compositeProperties);
                    break;
                case HiFiStylizedRendererFeature.DebugMode.DotsOnly:
                case HiFiStylizedRendererFeature.DebugMode.BenDayPattern:
                    DotsOnlyPass(renderGraph, source, destination, dotProperties);
                    break;
                case HiFiStylizedRendererFeature.DebugMode.BloomGate:
                    CompositePass(renderGraph, source, destination, bloomTexture, k_BloomGatePass, compositeProperties);
                    break;
                case HiFiStylizedRendererFeature.DebugMode.StylizedBloom:
                    CompositePass(renderGraph, source, destination, bloomTexture, k_StylizedBloomPass, compositeProperties);
                    break;
                case HiFiStylizedRendererFeature.DebugMode.SceneOnly:
                    CompositePass(renderGraph, source, destination, bloomTexture, k_SceneOnlyPass, compositeProperties);
                    break;
            }

            HiFiFrameResources frameResources = HiFiFrameResources.GetOrCreate(frameData, resourceData);
            frameResources.BloomTexture = bloomTexture;
            frameResources.StylizedCompositeTarget = destination;
            frameResources.SceneColor = destination;
            resourceData.cameraColor = destination;
        }

        private TextureHandle BuildBloomPyramid(
            RenderGraph renderGraph,
            TextureHandle source,
            UniversalCameraData cameraData,
            MaterialPropertyBlock bloomProperties)
        {
            int iterations = Mathf.Clamp(m_Settings.m_BloomIterations, 2, 6);
            int downsample = Mathf.Max(1, m_Settings.m_BloomDownsample);

            RenderTextureDescriptor mipDesc = cameraData.cameraTargetDescriptor;
            mipDesc.msaaSamples = 1;
            mipDesc.depthStencilFormat = GraphicsFormat.None;
            mipDesc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            mipDesc.width = Mathf.Max(1, mipDesc.width / downsample);
            mipDesc.height = Mathf.Max(1, mipDesc.height / downsample);

            TextureHandle[] mips = new TextureHandle[iterations];

            // Extract + first downsample into mip 0.
            mips[0] = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, mipDesc, k_BloomMipPrefix + "0", false, FilterMode.Bilinear);
            renderGraph.AddBlitPass(new BlitMaterialParameters(
                    source,
                    mips[0],
                    m_BloomMaterial,
                    k_ExtractPass,
                    bloomProperties,
                    FullScreenGeometryType.ProceduralTriangle),
                passName: "HiFi Bloom Extract");

            // Downsample + blur chain.
            for (int i = 1; i < iterations; ++i)
            {
                mipDesc.width = Mathf.Max(1, mipDesc.width / 2);
                mipDesc.height = Mathf.Max(1, mipDesc.height / 2);
                mips[i] = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, mipDesc, k_BloomMipPrefix + i, false, FilterMode.Bilinear);
                renderGraph.AddBlitPass(new BlitMaterialParameters(mips[i - 1], mips[i], m_BloomMaterial, k_DownsamplePass),
                    passName: "HiFi Bloom Downsample");
            }

            // Upsample (additive) back toward mip 0, which becomes the bloom texture.
            for (int i = iterations - 1; i > 0; --i)
            {
                bool last = i == 1;
                if (last)
                {
                    using (var builder = renderGraph.AddBlitPass(
                        new BlitMaterialParameters(mips[i], mips[i - 1], m_BloomMaterial, k_UpsamplePass),
                        passName: "HiFi Bloom Upsample", returnBuilder: true))
                    {
                        builder.SetGlobalTextureAfterPass(mips[0], s_BloomTextureId);
                    }
                }
                else
                {
                    renderGraph.AddBlitPass(new BlitMaterialParameters(mips[i], mips[i - 1], m_BloomMaterial, k_UpsamplePass),
                        passName: "HiFi Bloom Upsample");
                }
            }

            return mips[0];
        }

        private void CompositePass(
            RenderGraph renderGraph,
            TextureHandle source,
            TextureHandle destination,
            TextureHandle bloomTexture,
            int passIndex,
            MaterialPropertyBlock properties)
        {
            using (var builder = renderGraph.AddBlitPass(
                new BlitMaterialParameters(
                    source,
                    destination,
                    m_CompositeMaterial,
                    passIndex,
                    properties,
                    FullScreenGeometryType.ProceduralTriangle),
                passName: "HiFi Composite", returnBuilder: true))
            {
                builder.UseGlobalTexture(s_BloomTextureId, AccessFlags.Read);
            }
        }

        private void DotsOnlyPass(
            RenderGraph renderGraph,
            TextureHandle source,
            TextureHandle destination,
            MaterialPropertyBlock properties)
        {
            renderGraph.AddBlitPass(
                new BlitMaterialParameters(
                    source,
                    destination,
                    m_BenDayMaterial,
                    0,
                    properties,
                    FullScreenGeometryType.ProceduralTriangle),
                passName: "HiFi Ben-Day Debug");
        }

        private static MaterialPropertyBlock CreateDotProperties(HiFiStyleVolume volume, float dotDensity)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetFloat(s_DotDensityId, dotDensity);
            properties.SetFloat(s_DotRadiusId, volume.DotRadius.value);
            properties.SetFloat(s_DotRotationId, volume.DotRotation.value);
            properties.SetFloat(s_DotSoftnessId, volume.DotSoftness.value);
            return properties;
        }
    }
}
