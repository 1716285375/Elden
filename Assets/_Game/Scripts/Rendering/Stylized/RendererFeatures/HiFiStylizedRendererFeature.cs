using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Stylized post-processing feature: custom Hi-Fi bloom chain
    /// (extract -> downsample -> blur -> upsample), Ben-Day halftone dots,
    /// and the final composite. All parameters are driven by the
    /// <see cref="HiFiStyleVolume"/> volume; the feature only configures
    /// the injection point and debug view.
    /// </summary>
    public sealed class HiFiStylizedRendererFeature : ScriptableRendererFeature
    {
        /// <summary>Debug views for validating individual pipeline stages.</summary>
        /// <remarks>
        /// Enum values 0-3 are stable serialized values; 4-6 were added later and
        /// must not be reordered.
        /// </remarks>
        public enum DebugMode
        {
            /// <summary>Full composite: scene + dotted, gated bloom.</summary>
            Composite = 0,
            /// <summary>Raw bloom chain output only (before Ben-Day).</summary>
            BloomOnly = 1,
            /// <summary>Ben-Day dot pattern only (legacy alias of <see cref="BenDayPattern"/>).</summary>
            DotsOnly = 2,
            /// <summary>Scene color only (effect disabled preview).</summary>
            SceneOnly = 3,
            /// <summary>Ben-Day dot pattern only (soft-edged mask).</summary>
            BenDayPattern = 4,
            /// <summary>Bloom luminance gate only (smoothstep min/max).</summary>
            BloomGate = 5,
            /// <summary>Final stylized bloom (bloom * dots * gate * intensity), no scene.</summary>
            StylizedBloom = 6,
        }

        /// <summary>Feature settings (serialized on the renderer asset).</summary>
        [System.Serializable]
        public sealed class Settings
        {
            [Tooltip("Profiler tag used for the stylized post passes.")]
            public string m_ProfilerTag = "HiFi Stylized Post";

            [Tooltip("When the composite executes (must be after opaques, before or after URP post).")]
            public RenderPassEvent m_RenderEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Bloom pyramid scale: 2 = half resolution, 4 = quarter resolution.")]
            [Range(1, 8)] public int m_BloomDownsample = 2;

            [Tooltip("Number of bloom pyramid levels (2..6).")]
            [Range(2, 6)] public int m_BloomIterations = 4;

            [Tooltip("Debug view to render instead of the composite.")]
            public DebugMode m_DebugMode = DebugMode.Composite;
        }

        [SerializeField] private Settings m_Settings = new Settings();

        [Header("Shaders")]
        [SerializeField] private Shader m_BloomShader;
        [SerializeField] private Shader m_BenDayShader;
        [SerializeField] private Shader m_CompositeShader;

        private HiFiStylizedPostPass m_Pass;
        private Material m_BloomMaterial;
        private Material m_BenDayMaterial;
        private Material m_CompositeMaterial;

        /// <inheritdoc />
        public override void Create()
        {
            DisposeMaterials();

            m_BloomMaterial = CreateMaterial(m_BloomShader, "Game/Stylized/HiFiBloom");
            m_BenDayMaterial = CreateMaterial(m_BenDayShader, "Game/Stylized/HiFiBenDay");
            m_CompositeMaterial = CreateMaterial(m_CompositeShader, "Game/Stylized/HiFiComposite");
            m_Pass = new HiFiStylizedPostPass(
                m_Settings,
                m_BloomMaterial,
                m_BenDayMaterial,
                m_CompositeMaterial);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
            {
                return;
            }

            renderer.EnqueuePass(m_Pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            DisposeMaterials();
        }

        private static Material CreateMaterial(Shader shader, string fallbackName)
        {
            Shader resolvedShader = shader != null ? shader : Shader.Find(fallbackName);
            return resolvedShader != null ? CoreUtils.CreateEngineMaterial(resolvedShader) : null;
        }

        private void DisposeMaterials()
        {
            CoreUtils.Destroy(m_BloomMaterial);
            CoreUtils.Destroy(m_BenDayMaterial);
            CoreUtils.Destroy(m_CompositeMaterial);
            m_BloomMaterial = null;
            m_BenDayMaterial = null;
            m_CompositeMaterial = null;
        }
    }
}
