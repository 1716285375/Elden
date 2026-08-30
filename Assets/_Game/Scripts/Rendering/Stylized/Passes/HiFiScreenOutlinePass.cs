using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Render Graph screen-space outline with independent detection, separable
    /// dilation, and stencil-aware composition.
    /// </summary>
    public sealed class HiFiScreenOutlinePass : ScriptableRenderPass
    {
        private static readonly int s_BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int s_BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int s_CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int s_CameraDepthTexelSizeId = Shader.PropertyToID("_CameraDepthTexture_TexelSize");
        private static readonly int s_CameraNormalsTextureId = Shader.PropertyToID("_CameraNormalsTexture");
        private static readonly int s_RawMaskId = Shader.PropertyToID("_HiFiScreenRawMask");
        private static readonly int s_DilatedMaskId = Shader.PropertyToID("_HiFiScreenDilatedMask");
        private static readonly int s_ComputeInputMaskId = Shader.PropertyToID("_InputMask");
        private static readonly int s_ComputeOutputMaskId = Shader.PropertyToID("_OutputMask");
        private static readonly int s_ComputeTextureSizeId = Shader.PropertyToID("_TextureSize");
        private static readonly int s_ComputeDilationAxisId = Shader.PropertyToID("_DilationAxis");
        private static readonly int s_ComputeDilationRadiusId = Shader.PropertyToID("_DilationRadius");
        private static readonly int s_DepthThresholdId = Shader.PropertyToID("_DepthThreshold");
        private static readonly int s_DepthSoftnessId = Shader.PropertyToID("_DepthSoftness");
        private static readonly int s_NormalThresholdId = Shader.PropertyToID("_NormalThreshold");
        private static readonly int s_NormalSoftnessId = Shader.PropertyToID("_NormalSoftness");
        private static readonly int s_SilhouetteStrengthId = Shader.PropertyToID("_SilhouetteStrength");
        private static readonly int s_CreaseStrengthId = Shader.PropertyToID("_CreaseStrength");
        private static readonly int s_DebugModeId = Shader.PropertyToID("_HiFiScreenDebugMode");
        private static readonly int s_OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int s_OutlineColorModeId = Shader.PropertyToID("_OutlineColorMode");
        private static readonly int s_DarkOutlineColorId = Shader.PropertyToID("_DarkOutlineColor");
        private static readonly int s_LightOutlineColorId = Shader.PropertyToID("_LightOutlineColor");
        private static readonly int s_AutoContrastThresholdId = Shader.PropertyToID("_AutoContrastThreshold");

        private const string k_RawMaskName = "_HiFiScreenRawMask";
        private const string k_HorizontalMaskName = "_HiFiScreenHorizontalMask";
        private const string k_DilatedMaskName = "_HiFiScreenDilatedMask";
        private const string k_OutputName = "_HiFiScreenOutlineCameraColor";

        private const int k_DetectPass = 0;
        private const int k_CompositePass = 2;
        private const int k_ThreadGroupSize = 8;

        private readonly HiFiScreenOutlineRendererFeature.Settings m_Settings;
        private readonly Material m_Material;
        private readonly ComputeShader m_MorphologyCompute;
        private readonly int m_DilateKernel;

        /// <summary>Creates the screen outline pass.</summary>
        public HiFiScreenOutlinePass(
            HiFiScreenOutlineRendererFeature.Settings settings,
            Material material,
            ComputeShader morphologyCompute)
        {
            m_Settings = settings;
            m_Material = material;
            m_MorphologyCompute = morphologyCompute;
            m_DilateKernel = morphologyCompute != null ? morphologyCompute.FindKernel("DilateDual") : -1;
            renderPassEvent = settings.m_RenderEvent;
            profilingSampler = new ProfilingSampler(settings.m_ProfilerTag);
            requiresIntermediateTexture = true;
        }

        private sealed class DetectData
        {
            internal Material material;
            internal MaterialPropertyBlock properties;
            internal TextureHandle depthTexture;
            internal TextureHandle normalsTexture;
        }

        private sealed class DilateData
        {
            internal ComputeShader computeShader;
            internal int kernel;
            internal TextureHandle inputMask;
            internal TextureHandle outputMask;
            internal Vector2Int textureSize;
            internal Vector2Int axis;
            internal Vector2 radius;
        }

        private sealed class CompositeData
        {
            internal Material material;
            internal MaterialPropertyBlock properties;
            internal TextureHandle sceneColor;
            internal TextureHandle rawMask;
            internal TextureHandle dilatedMask;
        }

        /// <inheritdoc />
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_Material == null || m_MorphologyCompute == null || m_DilateKernel < 0)
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
            maskDesc.enableRandomWrite = true;

            TextureHandle rawMask = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, maskDesc, k_RawMaskName, false, FilterMode.Bilinear);
            TextureHandle horizontalMask = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, maskDesc, k_HorizontalMaskName, false, FilterMode.Bilinear);
            TextureHandle dilatedMask = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, maskDesc, k_DilatedMaskName, false, FilterMode.Bilinear);

            AddDetectPass(renderGraph, cameraData, resourceData, rawMask);
            AddDilationPass(renderGraph, rawMask, horizontalMask, Vector2.right, "HiFi Screen Outline Dilate Horizontal");
            AddDilationPass(renderGraph, horizontalMask, dilatedMask, Vector2.up, "HiFi Screen Outline Dilate Vertical");

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc outputDesc = renderGraph.GetTextureDesc(source);
            outputDesc.name = k_OutputName;
            outputDesc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(outputDesc);

            AddCompositePass(renderGraph, resourceData, source, destination, rawMask, dilatedMask);
            HiFiFrameResources frameResources = HiFiFrameResources.GetOrCreate(frameData, resourceData);
            frameResources.EdgeMask = dilatedMask;
            frameResources.SceneColor = destination;
            resourceData.cameraColor = destination;
        }

        private void AddDetectPass(
            RenderGraph renderGraph,
            UniversalCameraData cameraData,
            UniversalResourceData resourceData,
            TextureHandle rawMask)
        {
            using (var builder = renderGraph.AddRasterRenderPass<DetectData>(
                passName + " Detect", out var passData, profilingSampler))
            {
                int width = cameraData.cameraTargetDescriptor.width;
                int height = cameraData.cameraTargetDescriptor.height;

                passData.material = m_Material;
                passData.depthTexture = resourceData.cameraDepthTexture;
                passData.normalsTexture = resourceData.cameraNormalsTexture;
                passData.properties = new MaterialPropertyBlock();
                passData.properties.SetVector(s_BlitScaleBiasId, new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
                passData.properties.SetVector(
                    s_CameraDepthTexelSizeId,
                    new Vector4(1.0f / width, 1.0f / height, width, height));
                passData.properties.SetFloat(s_DepthThresholdId, m_Settings.m_DepthThreshold);
                passData.properties.SetFloat(s_DepthSoftnessId, m_Settings.m_DepthSoftness);
                passData.properties.SetFloat(s_NormalThresholdId, m_Settings.m_NormalThreshold);
                passData.properties.SetFloat(s_NormalSoftnessId, m_Settings.m_NormalSoftness);

                builder.UseTexture(passData.depthTexture, AccessFlags.Read);
                builder.UseTexture(passData.normalsTexture, AccessFlags.Read);
                builder.SetRenderAttachment(rawMask, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (DetectData data, RasterGraphContext context) =>
                {
                    data.properties.SetTexture(s_CameraDepthTextureId, data.depthTexture);
                    data.properties.SetTexture(s_CameraNormalsTextureId, data.normalsTexture);
                    context.cmd.DrawProcedural(
                        Matrix4x4.identity,
                        data.material,
                        k_DetectPass,
                        MeshTopology.Triangles,
                        3,
                        1,
                        data.properties);
                });
            }
        }

        private void AddDilationPass(
            RenderGraph renderGraph,
            TextureHandle source,
            TextureHandle destination,
            Vector2 axis,
            string renderPassName)
        {
            using (var builder = renderGraph.AddComputePass<DilateData>(
                renderPassName, out var passData, profilingSampler))
            {
                TextureDesc descriptor = source.GetDescriptor(renderGraph);
                passData.computeShader = m_MorphologyCompute;
                passData.kernel = m_DilateKernel;
                passData.inputMask = source;
                passData.outputMask = destination;
                passData.textureSize = new Vector2Int(descriptor.width, descriptor.height);
                passData.axis = new Vector2Int(Mathf.RoundToInt(axis.x), Mathf.RoundToInt(axis.y));
                passData.radius = new Vector2(
                    m_Settings.m_SilhouetteWidthPixels,
                    m_Settings.m_CreaseWidthPixels);

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(destination, AccessFlags.Write);
                builder.SetRenderFunc(static (DilateData data, ComputeGraphContext context) =>
                {
                    context.cmd.SetComputeTextureParam(
                        data.computeShader, data.kernel, s_ComputeInputMaskId, data.inputMask);
                    context.cmd.SetComputeTextureParam(
                        data.computeShader, data.kernel, s_ComputeOutputMaskId, data.outputMask);
                    context.cmd.SetComputeIntParams(
                        data.computeShader,
                        s_ComputeTextureSizeId,
                        data.textureSize.x,
                        data.textureSize.y);
                    context.cmd.SetComputeIntParams(
                        data.computeShader,
                        s_ComputeDilationAxisId,
                        data.axis.x,
                        data.axis.y);
                    context.cmd.SetComputeVectorParam(
                        data.computeShader,
                        s_ComputeDilationRadiusId,
                        new Vector4(data.radius.x, data.radius.y, 0.0f, 0.0f));
                    int groupsX = Mathf.CeilToInt(data.textureSize.x / (float)k_ThreadGroupSize);
                    int groupsY = Mathf.CeilToInt(data.textureSize.y / (float)k_ThreadGroupSize);
                    context.cmd.DispatchCompute(data.computeShader, data.kernel, groupsX, groupsY, 1);
                });
            }
        }

        private void AddCompositePass(
            RenderGraph renderGraph,
            UniversalResourceData resourceData,
            TextureHandle source,
            TextureHandle destination,
            TextureHandle rawMask,
            TextureHandle dilatedMask)
        {
            using (var builder = renderGraph.AddRasterRenderPass<CompositeData>(
                passName + " Composite", out var passData, profilingSampler))
            {
                passData.material = m_Material;
                passData.sceneColor = source;
                passData.rawMask = rawMask;
                passData.dilatedMask = dilatedMask;
                passData.properties = new MaterialPropertyBlock();
                passData.properties.SetVector(s_BlitScaleBiasId, new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
                passData.properties.SetInt(s_DebugModeId, (int)m_Settings.m_DebugMode);
                passData.properties.SetFloat(s_SilhouetteStrengthId, m_Settings.m_SilhouetteStrength);
                passData.properties.SetFloat(s_CreaseStrengthId, m_Settings.m_CreaseStrength);
                passData.properties.SetColor(s_OutlineColorId, m_Settings.m_OutlineColor);
                passData.properties.SetInt(s_OutlineColorModeId, (int)m_Settings.m_OutlineColorMode);
                passData.properties.SetColor(s_DarkOutlineColorId, m_Settings.m_DarkOutlineColor);
                passData.properties.SetColor(s_LightOutlineColorId, m_Settings.m_LightOutlineColor);
                passData.properties.SetFloat(s_AutoContrastThresholdId, m_Settings.m_AutoContrastThreshold);

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(rawMask, AccessFlags.Read);
                builder.UseTexture(dilatedMask, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);
                builder.SetRenderFunc(static (CompositeData data, RasterGraphContext context) =>
                {
                    data.properties.SetTexture(s_BlitTextureId, data.sceneColor);
                    data.properties.SetTexture(s_RawMaskId, data.rawMask);
                    data.properties.SetTexture(s_DilatedMaskId, data.dilatedMask);
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
