using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Inverted-hull outline pass.
    ///
    /// Draws a <see cref="RendererList"/> filtered by the "HiFiOutline"
    /// shader tag into the active camera color/depth targets. The hull
    /// does not write depth (render state block), so the shell is only
    /// visible at silhouette rims where front faces were culled.
    /// </summary>
    public sealed class HiFiOutlinePass : ScriptableRenderPass
    {
        private static readonly ShaderTagId s_OutlineTag = new ShaderTagId("HiFiOutline");

        private static readonly int s_OutlineGlobalScaleId = Shader.PropertyToID("_HiFiOutlineGlobalScale");

        // Reusable buffer for the RendererListDesc tag/state overrides.

        private readonly HiFiOutlineRendererFeature.Settings m_Settings;
        private readonly FilteringSettings m_FilteringSettings;
        private readonly RenderStateBlock m_RenderStateBlock;

        /// <summary>
        /// Creates a new outline pass.
        /// </summary>
        /// <param name="settings">The feature settings that drive this pass.</param>
        public HiFiOutlinePass(HiFiOutlineRendererFeature.Settings settings)
        {
            m_Settings = settings;
            profilingSampler = new ProfilingSampler(settings.m_ProfilerTag);
            renderPassEvent = settings.m_RenderEvent;

            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.m_LayerMask);

            // The shell must not write depth so it never occludes later
            // transparents; it still depth-tests against the opaque scene.
            // NOTE: stencil marking is a separate pass
            // (HiFiOutlineStencilMaskPass) - this pass only draws the hull.
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            m_RenderStateBlock.mask |= RenderStateMask.Depth;
            m_RenderStateBlock.depthState = new DepthState(false, CompareFunction.LessEqual);
        }

        private class PassData
        {
            internal RendererListHandle rendererListHdl;
            internal float outlineGlobalScale;
        }

        /// <inheritdoc />
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Scale 0 disables the outline entirely (an inverted hull expanded by
            // zero pixels would otherwise fill the surface with the outline color).
            if (m_Settings.m_GlobalWidthScale <= 0f)
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                passData.outlineGlobalScale = m_Settings.m_GlobalWidthScale;

                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);

                var desc = new UnityEngine.Rendering.RendererUtils.RendererListDesc(
                    s_OutlineTag, renderingData.cullResults, cameraData.camera)
                {
                    sortingCriteria = cameraData.defaultOpaqueSortFlags,
                    renderQueueRange = m_FilteringSettings.renderQueueRange,
                    layerMask = m_FilteringSettings.layerMask,
                    stateBlock = m_RenderStateBlock,
                };
                passData.rendererListHdl = renderGraph.CreateRendererList(desc);
                builder.UseRendererList(passData.rendererListHdl);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext rgContext) =>
                {
                    // Per-frame global multiplier for all HiFi outline widths.
                    rgContext.cmd.SetGlobalFloat(s_OutlineGlobalScaleId, data.outlineGlobalScale);
                    rgContext.cmd.DrawRendererList(data.rendererListHdl);
                });
            }
        }
    }
}
