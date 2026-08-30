using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Double-layer highlight silhouette (video-reference style).
    ///
    /// Fully independent from the Depth/Normal Screen Outline: works purely on
    /// an object mask of the highlight layer, then produces an inner dark ring
    /// and an outer bright ring via dual anti-aliased dilation.
    ///
    /// Pipeline (three explicit Render Graph passes):
    ///   HiFi Highlight Mask  -> renders highlight-layer geometry as R8 mask
    ///   HiFi Highlight Dilate -> R = small dilation, G = large dilation
    ///   HiFi Highlight Composite -> rings + color over the scene
    /// </summary>
    public sealed class HiFiHighlightOutlineRendererFeature : ScriptableRendererFeature
    {
        /// <summary>Feature settings (serialized on the renderer asset).</summary>
        [System.Serializable]
        public sealed class Settings
        {
            [Tooltip("Profiler tag used for the highlight passes.")]
            public string m_ProfilerTag = "HiFi Highlight Outline";

            [Tooltip("When the passes execute (after opaques, before/after post).")]
            public RenderPassEvent m_RenderEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Objects on these layers get the highlight silhouette.")]
            public LayerMask m_LayerMask;

            [Header("Rings")]
            [Tooltip("Inner (dark) ring width in pixels.")]
            [Range(0.5f, 16.0f)] public float m_InnerWidthPixels = 2.0f;

            [Tooltip("Outer (bright) ring width in pixels.")]
            [Range(0.5f, 32.0f)] public float m_OuterWidthPixels = 7.0f;

            [Tooltip("Inner ring color (near black by default).")]
            public Color m_InnerColor = new Color(0.02f, 0.02f, 0.03f, 1.0f);

            [Tooltip("Outer ring color (bright by default).")]
            public Color m_OuterColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);

            [Header("Debug")]
            [Tooltip("0 off, 1 mask, 2 small dilation, 3 outer ring.")]
            public int m_DebugMode = 0;
        }

        /// <summary>Per-frame Render Graph resources shared between the passes.</summary>
        public sealed class SharedResources
        {
            internal TextureHandle mask;
            internal TextureHandle horizontal;
            internal TextureHandle dilated;
        }

        [SerializeField] private Settings m_Settings = new Settings();
        [SerializeField] private Shader m_Shader;
        [SerializeField] private ComputeShader m_MorphologyCompute;

        private SharedResources m_Shared = new SharedResources();

        private HiFiHighlightMaskPass m_MaskPass;
        private HiFiHighlightDilationPass m_DilationPass;
        private HiFiHighlightCompositePass m_CompositePass;
        private Material m_Material;

        /// <inheritdoc />
        public override void Create()
        {
            CoreUtils.Destroy(m_Material);
            Shader resolvedShader = m_Shader != null ? m_Shader : Shader.Find("Game/Stylized/HiFiHighlightOutline");
            m_Material = resolvedShader != null ? CoreUtils.CreateEngineMaterial(resolvedShader) : null;
            m_MaskPass = new HiFiHighlightMaskPass(m_Settings, m_Shared, m_Material);
            m_DilationPass = new HiFiHighlightDilationPass(
                m_Settings, m_Shared, m_MorphologyCompute);
            m_CompositePass = new HiFiHighlightCompositePass(m_Settings, m_Shared, m_Material);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection ||
                !SystemInfo.supportsComputeShaders)
            {
                return;
            }

            renderer.EnqueuePass(m_MaskPass);
            renderer.EnqueuePass(m_DilationPass);
            renderer.EnqueuePass(m_CompositePass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_Material);
            m_Material = null;
        }
    }
}
