using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Separable compute dilation for the two highlight ring radii.
    /// </summary>
    public sealed class HiFiHighlightDilationPass : ScriptableRenderPass
    {
        private static readonly int s_InputMaskId = Shader.PropertyToID("_InputMask");
        private static readonly int s_OutputMaskId = Shader.PropertyToID("_OutputMask");
        private static readonly int s_TextureSizeId = Shader.PropertyToID("_TextureSize");
        private static readonly int s_DilationAxisId = Shader.PropertyToID("_DilationAxis");
        private static readonly int s_DilationRadiusId = Shader.PropertyToID("_DilationRadius");

        private const string k_HorizontalName = "_HiFiHighlightHorizontal";
        private const string k_DilatedName = "_HiFiHighlightDilated";
        private const int k_ThreadGroupSize = 8;

        private readonly HiFiHighlightOutlineRendererFeature.Settings m_Settings;
        private readonly HiFiHighlightOutlineRendererFeature.SharedResources m_Shared;
        private readonly ComputeShader m_ComputeShader;
        private readonly int m_DilateKernel;

        /// <summary>Creates the compute dilation pass.</summary>
        public HiFiHighlightDilationPass(
            HiFiHighlightOutlineRendererFeature.Settings settings,
            HiFiHighlightOutlineRendererFeature.SharedResources shared,
            ComputeShader computeShader)
        {
            m_Settings = settings;
            m_Shared = shared;
            m_ComputeShader = computeShader;
            m_DilateKernel = computeShader != null ? computeShader.FindKernel("DilateDual") : -1;
            renderPassEvent = settings.m_RenderEvent;
            profilingSampler = new ProfilingSampler(settings.m_ProfilerTag + " Dilate");
        }

        private sealed class PassData
        {
            internal ComputeShader computeShader;
            internal int kernel;
            internal TextureHandle input;
            internal TextureHandle output;
            internal Vector2Int textureSize;
            internal Vector2Int axis;
            internal Vector2 radius;
        }

        /// <inheritdoc />
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_ComputeShader == null || m_DilateKernel < 0 || !m_Shared.mask.IsValid())
            {
                return;
            }

            TextureDesc sourceDesc = m_Shared.mask.GetDescriptor(renderGraph);
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
                sourceDesc.width,
                sourceDesc.height,
                RenderTextureFormat.RGHalf,
                0)
            {
                depthStencilFormat = GraphicsFormat.None,
                msaaSamples = 1,
                enableRandomWrite = true,
            };

            m_Shared.horizontal = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, descriptor, k_HorizontalName, false, FilterMode.Bilinear);
            m_Shared.dilated = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, descriptor, k_DilatedName, false, FilterMode.Bilinear);

            AddComputePass(renderGraph, m_Shared.mask, m_Shared.horizontal, Vector2Int.right, passName + " Horizontal");
            AddComputePass(renderGraph, m_Shared.horizontal, m_Shared.dilated, Vector2Int.up, passName + " Vertical");
        }

        private void AddComputePass(
            RenderGraph renderGraph,
            TextureHandle source,
            TextureHandle destination,
            Vector2Int axis,
            string renderPassName)
        {
            using (var builder = renderGraph.AddComputePass<PassData>(
                renderPassName, out var passData, profilingSampler))
            {
                TextureDesc descriptor = source.GetDescriptor(renderGraph);
                passData.computeShader = m_ComputeShader;
                passData.kernel = m_DilateKernel;
                passData.input = source;
                passData.output = destination;
                passData.textureSize = new Vector2Int(descriptor.width, descriptor.height);
                passData.axis = axis;
                passData.radius = new Vector2(
                    m_Settings.m_InnerWidthPixels,
                    m_Settings.m_OuterWidthPixels);

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(destination, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
                {
                    context.cmd.SetComputeTextureParam(data.computeShader, data.kernel, s_InputMaskId, data.input);
                    context.cmd.SetComputeTextureParam(data.computeShader, data.kernel, s_OutputMaskId, data.output);
                    context.cmd.SetComputeIntParams(
                        data.computeShader, s_TextureSizeId, data.textureSize.x, data.textureSize.y);
                    context.cmd.SetComputeIntParams(
                        data.computeShader, s_DilationAxisId, data.axis.x, data.axis.y);
                    context.cmd.SetComputeVectorParam(
                        data.computeShader,
                        s_DilationRadiusId,
                        new Vector4(data.radius.x, data.radius.y, 0.0f, 0.0f));

                    int groupsX = Mathf.CeilToInt(data.textureSize.x / (float)k_ThreadGroupSize);
                    int groupsY = Mathf.CeilToInt(data.textureSize.y / (float)k_ThreadGroupSize);
                    context.cmd.DispatchCompute(data.computeShader, data.kernel, groupsX, groupsY, 1);
                });
            }
        }
    }
}
