using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Renders an inverted-hull outline shell over every object whose
    /// material exposes a pass tagged LightMode = "HiFiOutline" (see
    /// Game/Stylized/HiFiToon). The hull is drawn with front-face culling
    /// so only the silhouette rim is visible. Uses Render Graph only.
    /// </summary>
    public sealed class HiFiOutlineRendererFeature : ScriptableRendererFeature
    {
        /// <summary>Feature settings (serialized on the renderer asset).</summary>
        [System.Serializable]
        public sealed class Settings
        {
            [Tooltip("Profiler tag used for this pass.")]
            public string m_ProfilerTag = "HiFi Outline";

            [Tooltip("When the outline pass executes.")]
            public RenderPassEvent m_RenderEvent = RenderPassEvent.AfterRenderingOpaques;

            [Tooltip("Objects on these layers receive the outline.")]
            public LayerMask m_LayerMask = -1;

            [Tooltip("Global multiplier applied to all HiFi outline widths. " +
                "The per-object base width stays a material property (_OutlineWidthPx).")]
            [Range(0.0f, 4.0f)] public float m_GlobalWidthScale = 1.0f;
        }

        [SerializeField] private Settings m_Settings = new Settings();

        private HiFiOutlinePass m_Pass;
        private HiFiOutlineStencilMaskPass m_StencilMaskPass;

        /// <inheritdoc />
        public override void Create()
        {
            m_Pass = new HiFiOutlinePass(m_Settings);
            m_StencilMaskPass = new HiFiOutlineStencilMaskPass(m_Settings);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
            {
                return;
            }

            // Stencil mask must run before the screen-space outline (which skips
            // stencil=1 pixels), so it is enqueued before the hull.
            renderer.EnqueuePass(m_StencilMaskPass);
            renderer.EnqueuePass(m_Pass);
        }
    }
}
