using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Screen-space environment outline feature (Depth + Normal edge detection).
    ///
    /// Phase-2 behavior:
    ///   - Detection always uses 1-pixel neighbours (angle-based normals,
    ///     relative eye-space depth), so smooth curved surfaces are ignored.
    ///   - Outline width is applied by dilation of the raw edge mask, fully
    ///     decoupled from the detection taps.
    ///   - Silhouette (depth) and Crease (normal) strengths are separate.
    ///   - Color modes: Fixed / AutoContrast / InvertedColor.
    /// </summary>
    public sealed class HiFiScreenOutlineRendererFeature : ScriptableRendererFeature
    {
        /// <summary>How the outline color is chosen.</summary>
        public enum OutlineColorMode
        {
            /// <summary>Use <see cref="Settings.m_OutlineColor"/> directly.</summary>
            Fixed = 0,
            /// <summary>Dark on bright scene, light on dark scene.</summary>
            AutoContrast = 1,
            /// <summary>Complementary (inverted) color of the scene.</summary>
            InvertedColor = 2,
        }

        /// <summary>Debug views for the edge masks.</summary>
        public enum DebugMode
        {
            /// <summary>Final composite.</summary>
            Combined = 0,
            /// <summary>Dilated silhouette (depth edge) mask.</summary>
            DepthEdge = 1,
            /// <summary>Dilated crease (normal edge) mask.</summary>
            NormalEdge = 2,
            /// <summary>Raw (undilated) combined edge mask.</summary>
            RawEdge = 3,
            /// <summary>Final dilated edge mask (before color).</summary>
            DilatedEdge = 4,
        }

        /// <summary>Feature settings (serialized on the renderer asset).</summary>
        [System.Serializable]
        public sealed class Settings
        {
            [Tooltip("Profiler tag used for the screen-space outline pass.")]
            public string m_ProfilerTag = "HiFi Screen Outline";

            [Tooltip("When the pass executes (must be after opaques).")]
            public RenderPassEvent m_RenderEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            [Header("Depth (Silhouette)")]
            [Tooltip("Relative eye-depth difference that starts an outer silhouette line.")]
            [Range(0.001f, 0.5f)] public float m_DepthThreshold = 0.02f;

            [Tooltip("Smoothstep width of the depth threshold.")]
            [Range(0.001f, 0.2f)] public float m_DepthSoftness = 0.01f;

            [Header("Normal (Crease)")]
            [Tooltip("Angle-based normal difference (1 - dot) that starts a crease line.")]
            [Range(0.01f, 0.5f)] public float m_NormalThreshold = 0.12f;

            [Tooltip("Smoothstep width of the normal threshold.")]
            [Range(0.005f, 0.2f)] public float m_NormalSoftness = 0.04f;

            [Header("Width & Roles")]
            [Tooltip("Silhouette (depth edge) width in pixels. Applied by anti-aliased dilation.")]
            [Range(0.5f, 8.0f)] public float m_SilhouetteWidthPixels = 2.25f;

            [Tooltip("Crease (normal edge) width in pixels - interior structure lines stay thinner.")]
            [Range(0.25f, 4.0f)] public float m_CreaseWidthPixels = 0.85f;

            [Tooltip("Strength of the silhouette (depth) line.")]
            [Range(0.0f, 2.0f)] public float m_SilhouetteStrength = 1.0f;

            [Tooltip("Strength of the crease (normal) line - interior structure lines.")]
            [Range(0.0f, 1.0f)] public float m_CreaseStrength = 0.35f;

            [Header("Color")]
            [Tooltip("Line color used in Fixed mode.")]
            public Color m_OutlineColor = new Color(0.03f, 0.03f, 0.04f, 1.0f);

            [Tooltip("Color mode: Fixed / AutoContrast / InvertedColor.")]
            public OutlineColorMode m_OutlineColorMode = OutlineColorMode.Fixed;

            [Tooltip("AutoContrast: line color used on bright scene pixels.")]
            public Color m_DarkOutlineColor = new Color(0.02f, 0.02f, 0.03f, 1.0f);

            [Tooltip("AutoContrast: line color used on dark scene pixels.")]
            public Color m_LightOutlineColor = new Color(0.85f, 0.85f, 0.88f, 1.0f);

            [Tooltip("AutoContrast: scene luminance above this uses the dark line.")]
            [Range(0.0f, 1.0f)] public float m_AutoContrastThreshold = 0.45f;

            [Header("Debug")]
            [Tooltip("Debug view of the edge masks.")]
            public DebugMode m_DebugMode = DebugMode.Combined;
        }

        [SerializeField] private Settings m_Settings = new Settings();
        [SerializeField] private Shader m_Shader;
        [SerializeField] private ComputeShader m_MorphologyCompute;

        private HiFiScreenOutlinePass m_Pass;
        private Material m_Material;

        /// <inheritdoc />
        public override void Create()
        {
            CoreUtils.Destroy(m_Material);
            Shader resolvedShader = m_Shader != null ? m_Shader : Shader.Find("Game/Stylized/HiFiScreenOutline");
            m_Material = resolvedShader != null ? CoreUtils.CreateEngineMaterial(resolvedShader) : null;
            m_Pass = new HiFiScreenOutlinePass(m_Settings, m_Material, m_MorphologyCompute);
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

            // Depth + normals are required for edge detection.
            m_Pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            renderer.EnqueuePass(m_Pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_Material);
            m_Material = null;
        }
    }
}
