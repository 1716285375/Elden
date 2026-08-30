using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Renders the highlight-layer geometry into an R8 object mask
    /// (1 inside the target, 0 outside) using the scene depth (ZTest LEqual).
    ///
    /// Uses the URP-official RendererList creation path
    /// (RenderingUtils.CreateDrawingSettings + CreateRendererListWithRenderStateBlock).
    /// A bare RendererListDesc was observed to feed the mask shader a stale
    /// unity_ObjectToWorld on URP17 Render Graph, projecting objects to the
    /// wrong screen position (appearing ~3x wider and shifted right).
    /// </summary>
    public sealed class HiFiHighlightMaskPass : ScriptableRenderPass
    {
        private static readonly List<ShaderTagId> s_ShaderTagIds = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        private const string k_MaskName = "_HiFiHighlightMask";

        private readonly HiFiHighlightOutlineRendererFeature.Settings m_Settings;
        private readonly HiFiHighlightOutlineRendererFeature.SharedResources m_Shared;

        private readonly Material m_MaskMaterial;

        /// <summary>
        /// Creates a new highlight mask pass.
        /// </summary>
        public HiFiHighlightMaskPass(
            HiFiHighlightOutlineRendererFeature.Settings settings,
            HiFiHighlightOutlineRendererFeature.SharedResources shared,
            Material maskMaterial)
        {
            m_Settings = settings;
            m_Shared = shared;
            m_MaskMaterial = maskMaterial;
            renderPassEvent = settings.m_RenderEvent;
            profilingSampler = new ProfilingSampler(settings.m_ProfilerTag + " Mask");
        }

        private class PassData
        {
            internal RendererListHandle rendererListHdl;
        }

        /// <inheritdoc />
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_MaskMaterial == null)
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            // Use the ACTIVE COLOR texture's actual size rather than
            // cameraTargetDescriptor: on some Render Graph configurations
            // cameraTargetDescriptor can differ from the real render-target size.
            TextureDesc colorDesc = resourceData.activeColorTexture.GetDescriptor(renderGraph);
            RenderTextureDescriptor maskDesc = new RenderTextureDescriptor(
                colorDesc.width,
                colorDesc.height,
                RenderTextureFormat.RGHalf, 0);
            maskDesc.depthStencilFormat = GraphicsFormat.None;
            maskDesc.msaaSamples = 1;
            // Clear the mask: only object pixels are written, the background must
            // be exactly 0 (uncleared pool memory could contain 1s and flood the
            // dilation rings over the whole screen).
            m_Shared.mask = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, maskDesc, k_MaskName, true, FilterMode.Bilinear);
            HiFiFrameResources.GetOrCreate(frameData, resourceData).HighlightMask = m_Shared.mask;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                builder.SetRenderAttachment(m_Shared.mask, 0, AccessFlags.Write);
                // Bind scene depth for ZTest LEqual so the mask respects occluders
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);

                // URP-official RendererList path: DrawingSettings carries
                // renderingData.perObjectData so UnityPerDraw (unity_ObjectToWorld)
                // is correctly uploaded per object. A bare RendererListDesc missed
                // this and rendered with a stale matrix on URP17 RG.
                var drawSettings = RenderingUtils.CreateDrawingSettings(
                    s_ShaderTagIds, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);
                drawSettings.overrideMaterial = m_MaskMaterial;
                drawSettings.overrideMaterialPassIndex = 0;
                var filterSettings = new FilteringSettings(RenderQueueRange.opaque, m_Settings.m_LayerMask.value);
                var rlParams = new UnityEngine.Rendering.RendererListParams(
                    renderingData.cullResults, drawSettings, filterSettings)
                {
                    isPassTagName = false,
                };
                passData.rendererListHdl = renderGraph.CreateRendererList(rlParams);

                builder.UseRendererList(passData.rendererListHdl);
                // Allow global state modification: without this the Render Graph may
                // assume the pass cannot modify global shader state and skip
                // uploading the per-camera / per-object matrices.
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext rgContext) =>
                {
                    rgContext.cmd.DrawRendererList(data.rendererListHdl);
                });
            }
        }
    }
}
