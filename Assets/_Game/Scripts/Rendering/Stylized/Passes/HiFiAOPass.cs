using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Produces a reusable AO/crease mask, then composites comic ink and
    /// hatching into a new camera-color texture.
    /// </summary>
    public sealed class HiFiAOPass : ScriptableRenderPass
    {
        private static readonly int s_BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int s_BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int s_CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int s_CameraDepthTexelSizeId = Shader.PropertyToID("_CameraDepthTexture_TexelSize");
        private static readonly int s_CameraNormalsTextureId = Shader.PropertyToID("_CameraNormalsTexture");
        private static readonly int s_AOMaskId = Shader.PropertyToID("_HiFiAOMask");
        private static readonly int s_AOStrengthId = Shader.PropertyToID("_HiFiAOStrength");
        private static readonly int s_AOLineStrengthId = Shader.PropertyToID("_HiFiAOLineStrength");
        private static readonly int s_HatchingScaleId = Shader.PropertyToID("_HiFiHatchingScale");
        private static readonly int s_HatchingStrengthId = Shader.PropertyToID("_HiFiHatchingStrength");
        private static readonly int s_HatchingTextureId = Shader.PropertyToID("_HiFiHatchingTexture");

        private const string k_MaskName = "_HiFiAOMask";
        private const string k_OutputName = "_HiFiAOCameraColor";
        private const int k_CompositePass = 0;
        private const int k_MaskPass = 1;

        private readonly HiFiAORendererFeature.Settings m_Settings;
        private readonly Material m_Material;

        /// <summary>Creates the AO pass.</summary>
        public HiFiAOPass(HiFiAORendererFeature.Settings settings, Material material)
        {
            m_Settings = settings;
            m_Material = material;
            renderPassEvent = settings.m_RenderEvent;
            profilingSampler = new ProfilingSampler(settings.m_ProfilerTag);
            requiresIntermediateTexture = true;
        }

        private sealed class MaskData
        {
            internal Material material;
            internal MaterialPropertyBlock properties;
            internal TextureHandle depth;
            internal TextureHandle normals;
        }

        private sealed class CompositeData
        {
            internal Material material;
            internal MaterialPropertyBlock properties;
            internal TextureHandle source;
            internal TextureHandle mask;
        }

        /// <inheritdoc />
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_Material == null)
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
            if (resourceData.isActiveTargetBackBuffer ||
                !resourceData.cameraDepthTexture.IsValid() ||
                !resourceData.cameraNormalsTexture.IsValid())
            {
                return;
            }

            RenderTextureDescriptor maskDesc = cameraData.cameraTargetDescriptor;
            maskDesc.msaaSamples = 1;
            maskDesc.depthStencilFormat = GraphicsFormat.None;
            maskDesc.graphicsFormat = GraphicsFormat.R16G16_SFloat;
            TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, maskDesc, k_MaskName, false, FilterMode.Bilinear);
            AddMaskPass(renderGraph, cameraData, resourceData, mask);

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc outputDesc = renderGraph.GetTextureDesc(source);
            outputDesc.name = k_OutputName;
            outputDesc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(outputDesc);
            AddCompositePass(renderGraph, source, destination, mask, volume);

            HiFiFrameResources frameResources = HiFiFrameResources.GetOrCreate(frameData, resourceData);
            frameResources.AOMask = mask;
            frameResources.SceneColor = destination;
            resourceData.cameraColor = destination;
        }

        private void AddMaskPass(
            RenderGraph renderGraph,
            UniversalCameraData cameraData,
            UniversalResourceData resourceData,
            TextureHandle mask)
        {
            using (var builder = renderGraph.AddRasterRenderPass<MaskData>(
                passName + " Mask", out var passData, profilingSampler))
            {
                int width = cameraData.cameraTargetDescriptor.width;
                int height = cameraData.cameraTargetDescriptor.height;
                passData.material = m_Material;
                passData.depth = resourceData.cameraDepthTexture;
                passData.normals = resourceData.cameraNormalsTexture;
                passData.properties = new MaterialPropertyBlock();
                passData.properties.SetVector(s_BlitScaleBiasId, new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
                passData.properties.SetVector(
                    s_CameraDepthTexelSizeId,
                    new Vector4(1.0f / width, 1.0f / height, width, height));

                builder.UseTexture(passData.depth, AccessFlags.Read);
                builder.UseTexture(passData.normals, AccessFlags.Read);
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) =>
                {
                    data.properties.SetTexture(s_CameraDepthTextureId, data.depth);
                    data.properties.SetTexture(s_CameraNormalsTextureId, data.normals);
                    context.cmd.DrawProcedural(
                        Matrix4x4.identity,
                        data.material,
                        k_MaskPass,
                        MeshTopology.Triangles,
                        3,
                        1,
                        data.properties);
                });
            }
        }

        private void AddCompositePass(
            RenderGraph renderGraph,
            TextureHandle source,
            TextureHandle destination,
            TextureHandle mask,
            HiFiStyleVolume volume)
        {
            using (var builder = renderGraph.AddRasterRenderPass<CompositeData>(
                passName + " Composite", out var passData, profilingSampler))
            {
                passData.material = m_Material;
                passData.source = source;
                passData.mask = mask;
                passData.properties = new MaterialPropertyBlock();
                passData.properties.SetVector(s_BlitScaleBiasId, new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
                passData.properties.SetFloat(s_AOStrengthId, m_Settings.m_AOStrength);
                passData.properties.SetFloat(s_AOLineStrengthId, volume.AOLineStrength.value);
                passData.properties.SetFloat(s_HatchingScaleId, volume.HatchingScale.value);
                passData.properties.SetFloat(s_HatchingStrengthId, m_Settings.m_HatchingStrength);
                if (m_Settings.m_HatchingTexture != null)
                {
                    passData.properties.SetTexture(s_HatchingTextureId, m_Settings.m_HatchingTexture);
                }

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(mask, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (CompositeData data, RasterGraphContext context) =>
                {
                    data.properties.SetTexture(s_BlitTextureId, data.source);
                    data.properties.SetTexture(s_AOMaskId, data.mask);
                    context.cmd.DrawProcedural(
                        Matrix4x4.identity,
                        data.material,
                        k_CompositePass,
                        MeshTopology.Triangles,
                        3,
                        1,
                        data.properties);
                });
            }
        }
    }
}
