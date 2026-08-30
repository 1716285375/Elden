using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Marks the character region in the depth-stencil buffer (stencil = 1)
    /// so the screen-space environment outline can skip characters.
    ///
    /// Renders the ORIGINAL geometry (Cull Back, no expansion) with ColorMask 0
    /// - this pass can never paint the surface, it only tags stencil.
    /// The actual outline drawing is done by <see cref="HiFiOutlinePass"/>.
    /// </summary>
    public sealed class HiFiOutlineStencilMaskPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId s_StencilMaskTag = new ShaderTagId("HiFiOutlineStencilMask");

        private readonly HiFiOutlineRendererFeature.Settings m_Settings;
        private readonly FilteringSettings m_FilteringSettings;

        /// <summary>
        /// Creates a new stencil-mask pass.
        /// </summary>
        /// <param name="settings">The feature settings that drive this pass.</param>
        public HiFiOutlineStencilMaskPass(HiFiOutlineRendererFeature.Settings settings)
        {
            m_Settings = settings;
            renderPassEvent = settings.m_RenderEvent;
            profilingSampler = new ProfilingSampler(settings.m_ProfilerTag + " Stencil Mask");

            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.m_LayerMask);
        }

        private class PassData
        {
            internal RendererListHandle rendererListHdl;
        }

        /// <inheritdoc />
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_Settings.m_GlobalWidthScale <= 0f)
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                // Read-write the depth-stencil: we read depth (ZTest) and write
                // the stencil bits. No color attachment at all.
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);

                var desc = new UnityEngine.Rendering.RendererUtils.RendererListDesc(
                    s_StencilMaskTag, renderingData.cullResults, cameraData.camera)
                {
                    sortingCriteria = cameraData.defaultOpaqueSortFlags,
                    renderQueueRange = m_FilteringSettings.renderQueueRange,
                    layerMask = m_FilteringSettings.layerMask,
                };
                passData.rendererListHdl = renderGraph.CreateRendererList(desc);

                builder.UseRendererList(passData.rendererListHdl);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext rgContext) =>
                {
                    rgContext.cmd.DrawRendererList(data.rendererListHdl);
                });
            }
        }
    }
}
