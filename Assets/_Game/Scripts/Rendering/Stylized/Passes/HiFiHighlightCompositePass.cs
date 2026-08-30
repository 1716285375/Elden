using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Highlight composite: reads the scene copy + mask + dilated rings and
    /// writes inner (dark) / outer (bright) rings over the scene color.
    /// Object interior (mask == 1) is never tinted.
    /// </summary>
    public sealed class HiFiHighlightCompositePass : ScriptableRenderPass
    {
        private static readonly int s_BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int s_BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int s_MaskId = Shader.PropertyToID("_HiFiHighlightMask");
        private static readonly int s_DilatedId = Shader.PropertyToID("_HiFiHighlightDilated");
        private static readonly int s_DebugId = Shader.PropertyToID("_HiFiHighlightDebug");
        private static readonly int s_InnerColorId = Shader.PropertyToID("_InnerColor");
        private static readonly int s_OuterColorId = Shader.PropertyToID("_OuterColor");

        private const string k_OutputName = "_HiFiHighlightCameraColor";
        private const int k_CompositePass = 2;

        private readonly HiFiHighlightOutlineRendererFeature.Settings m_Settings;
        private readonly HiFiHighlightOutlineRendererFeature.SharedResources m_Shared;

        private readonly Material m_Material;

        /// <summary>
        /// Creates a new highlight composite pass.
        /// </summary>
        /// <param name="settings">The feature settings.</param>
        /// <param name="shared">Shared Render Graph resources.</param>
        public HiFiHighlightCompositePass(
            HiFiHighlightOutlineRendererFeature.Settings settings,
            HiFiHighlightOutlineRendererFeature.SharedResources shared,
            Material material)
        {
            m_Settings = settings;
            m_Shared = shared;
            m_Material = material;
            renderPassEvent = settings.m_RenderEvent;
            profilingSampler = new ProfilingSampler(settings.m_ProfilerTag + " Composite");
            requiresIntermediateTexture = true;
        }

        private class PassData
        {
            internal Material material;
            internal TextureHandle sceneColor;
            internal TextureHandle mask;
            internal TextureHandle dilated;
            internal Color innerColor;
            internal Color outerColor;
            internal int debugMode;
            internal MaterialPropertyBlock properties;
        }

        /// <inheritdoc />
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {

            if (m_Material == null || !m_Shared.mask.IsValid() || !m_Shared.dilated.IsValid())
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
            {
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc outputDesc = renderGraph.GetTextureDesc(source);
            outputDesc.name = k_OutputName;
            outputDesc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(outputDesc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                passData.material = m_Material;
                passData.sceneColor = source;
                passData.mask = m_Shared.mask;
                passData.dilated = m_Shared.dilated;
                passData.innerColor = m_Settings.m_InnerColor;
                passData.outerColor = m_Settings.m_OuterColor;
                passData.debugMode = (int)m_Settings.m_DebugMode;
                passData.properties = new MaterialPropertyBlock();
                passData.properties.SetVector(s_BlitScaleBiasId, new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
                passData.properties.SetColor(s_InnerColorId, passData.innerColor);
                passData.properties.SetColor(s_OuterColorId, passData.outerColor);
                passData.properties.SetInt(s_DebugId, passData.debugMode);

                builder.UseTexture(passData.sceneColor, AccessFlags.Read);
                builder.UseTexture(passData.mask, AccessFlags.Read);
                builder.UseTexture(passData.dilated, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext rgContext) =>
                {
                    data.properties.SetTexture(s_BlitTextureId, data.sceneColor);
                    data.properties.SetTexture(s_MaskId, data.mask);
                    data.properties.SetTexture(s_DilatedId, data.dilated);
                    rgContext.cmd.DrawProcedural(
                        Matrix4x4.identity,
                        data.material,
                        k_CompositePass,
                        MeshTopology.Triangles,
                        3,
                        1,
                        data.properties);
                });
            }

            HiFiFrameResources frameResources = HiFiFrameResources.GetOrCreate(frameData, resourceData);
            frameResources.SceneColor = destination;
            resourceData.cameraColor = destination;
        }
    }
}
