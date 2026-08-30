using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Frame-scoped handles shared by the HiFi Render Graph passes.
    /// Handles are valid only for the current camera frame.
    /// </summary>
    public sealed class HiFiFrameResources : ContextItem
    {
        public TextureHandle SceneColor = TextureHandle.nullHandle;
        public TextureHandle Depth = TextureHandle.nullHandle;
        public TextureHandle Normals = TextureHandle.nullHandle;
        public TextureHandle AOMask = TextureHandle.nullHandle;
        public TextureHandle EdgeMask = TextureHandle.nullHandle;
        public TextureHandle HighlightMask = TextureHandle.nullHandle;
        public TextureHandle BloomTexture = TextureHandle.nullHandle;
        public TextureHandle BenDayMask = TextureHandle.nullHandle;
        public TextureHandle StylizedCompositeTarget = TextureHandle.nullHandle;

        /// <inheritdoc />
        public override void Reset()
        {
            SceneColor = TextureHandle.nullHandle;
            Depth = TextureHandle.nullHandle;
            Normals = TextureHandle.nullHandle;
            AOMask = TextureHandle.nullHandle;
            EdgeMask = TextureHandle.nullHandle;
            HighlightMask = TextureHandle.nullHandle;
            BloomTexture = TextureHandle.nullHandle;
            BenDayMask = TextureHandle.nullHandle;
            StylizedCompositeTarget = TextureHandle.nullHandle;
        }

        /// <summary>Gets the frame context and seeds built-in URP resources.</summary>
        public static HiFiFrameResources GetOrCreate(
            ContextContainer frameData,
            UniversalResourceData resourceData)
        {
            HiFiFrameResources resources = frameData.GetOrCreate<HiFiFrameResources>();
            resources.SceneColor = resourceData.activeColorTexture;
            resources.Depth = resourceData.cameraDepthTexture;
            resources.Normals = resourceData.cameraNormalsTexture;
            return resources;
        }
    }
}
