using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Stylized screen-space AO / ink / hatching feature.
    ///
    /// Uses the camera depth + normals textures to derive contact,
    /// crease, depth-discontinuity and normal-discontinuity masks and
    /// reveals hatching / ink lines with them. This is intentionally not
    /// a smoothed SSAO - it is a comic-ink treatment.
    /// </summary>
    public sealed class HiFiAORendererFeature : ScriptableRendererFeature
    {
        /// <summary>Feature settings (serialized on the renderer asset).</summary>
        [System.Serializable]
        public sealed class Settings
        {
            [Tooltip("Profiler tag used for the AO pass.")]
            public string m_ProfilerTag = "HiFi AO Lines";

            [Tooltip("When the AO pass executes.")]
            public RenderPassEvent m_RenderEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Contact/AO darkening strength (not exposed on the volume).")]
            [Range(0.0f, 1.0f)] public float m_AOStrength = 0.35f;

            [Tooltip("How strongly hatching is revealed by the AO mask.")]
            [Range(0.0f, 1.0f)] public float m_HatchingStrength = 0.5f;

            [Tooltip("Hatching pattern texture (screen-space tiled).")]
            public Texture2D m_HatchingTexture;
        }

        [SerializeField] private Settings m_Settings = new Settings();
        [SerializeField] private Shader m_Shader;

        private HiFiAOPass m_Pass;
        private Material m_Material;

        /// <inheritdoc />
        public override void Create()
        {
            CoreUtils.Destroy(m_Material);
            Shader resolvedShader = m_Shader != null ? m_Shader : Shader.Find("Game/Stylized/HiFiAOLines");
            m_Material = resolvedShader != null ? CoreUtils.CreateEngineMaterial(resolvedShader) : null;
            m_Pass = new HiFiAOPass(m_Settings, m_Material);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
            {
                return;
            }

            // Request the depth-normals prepass so the camera normals texture exists.
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
