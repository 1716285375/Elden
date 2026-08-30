using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZZ.Rendering.Stylized
{
    /// <summary>
    /// Global screen-space parameters for the Hi-Fi stylized rendering suite.
    /// This volume drives the screen-space passes only (stylized bloom,
    /// Ben-Day dots, AO ink/hatching). Toon surface parameters such as
    /// outline width and shadow colors intentionally remain per-material
    /// properties and are not exposed here.
    /// </summary>
    [Serializable]
    [VolumeComponentMenu("Stylized/HiFi Style")]
    public sealed class HiFiStyleVolume : VolumeComponent, IPostProcessComponent
    {
        /// <summary>Master toggle for the stylized screen-space passes.</summary>
        public BoolParameter Enable = new BoolParameter(true);

        [Header("Stylized Bloom")]
        public ClampedFloatParameter BloomThreshold = new ClampedFloatParameter(1.2f, 0.0f, 4.0f);
        public ClampedFloatParameter BloomIntensity = new ClampedFloatParameter(0.5f, 0.0f, 8.0f);

        [Header("Ben-Day Dots")]
        [Tooltip("Number of dot cells across the screen (screen-UV grid density). " +
            "Too high values make dots sub-pixel; values above 128 are clamped in the pass.")]
        public ClampedFloatParameter DotDensity = new ClampedFloatParameter(48.0f, 4.0f, 256.0f);

        [Tooltip("Dot radius as a fraction of half a cell (the composite applies an extra 0.5 scale and the bloom gate). " +
            "0 = no dot, ~1 = dot fills the cell. On-screen dot area scales with radius^2.")]
        public ClampedFloatParameter DotRadius = new ClampedFloatParameter(0.2f, 0.05f, 1.0f);

        [Tooltip("Rotation of the dot pattern coordinate system. A uniform grid is " +
            "rotation-invariant in coverage, so this only re-orients the pattern.")]
        public ClampedFloatParameter DotRotation = new ClampedFloatParameter(0.7853982f, -3.14159265f, 3.14159265f);

        [Tooltip("Smoothstep edge width of the dot mask (0 = hard step, larger = softer edge).")]
        public ClampedFloatParameter DotSoftness = new ClampedFloatParameter(0.06f, 0.0f, 0.5f);

        [Header("Ben-Day Bloom Gate")]
        [Tooltip("Bloom luminance below which the Ben-Day dots are fully hidden.")]
        public ClampedFloatParameter DotBloomMin = new ClampedFloatParameter(0.8f, 0.0f, 4.0f);

        [Tooltip("Bloom luminance above which the Ben-Day gate is fully open.")]
        public ClampedFloatParameter DotBloomMax = new ClampedFloatParameter(2.0f, 0.0f, 8.0f);

        [Header("Comic Print Surface")]
        [Tooltip("Strength of the screen-space print dots applied to shaded surfaces.")]
        public ClampedFloatParameter PrintIntensity = new ClampedFloatParameter(0.14f, 0.0f, 1.0f);

        [Tooltip("Number of print cells across screen height. Values around 80-120 read as print texture at 1080p.")]
        public ClampedFloatParameter PrintDensity = new ClampedFloatParameter(96.0f, 16.0f, 192.0f);

        [Tooltip("How strongly darker colors grow the printed ink dots.")]
        public ClampedFloatParameter PrintShadowStrength = new ClampedFloatParameter(0.9f, 0.0f, 2.0f);

        [Header("Stylized AO")]
        public ClampedFloatParameter AOLineStrength = new ClampedFloatParameter(0.25f, 0.0f, 2.0f);
        public ClampedFloatParameter HatchingScale = new ClampedFloatParameter(8.0f, 1.0f, 64.0f);

        /// <inheritdoc />
        public bool IsActive() => Enable.value;

        /// <inheritdoc />
        public bool IsTileCompatible() => false;
    }
}
