using System.Collections.Generic;

namespace ZZ.Demo
{
    /// <summary>
    /// One scripted demo recording: a name, a capture frame rate, and an ordered step list.
    /// </summary>
    /// <remarks>
    /// Part of the reusable demo/replay core: nothing in this folder may reference
    /// <c>UnityEditor</c>, so the whole folder can be moved out of <c>Editor</c> and compiled into
    /// the game when an in-game replay or recording module is added.
    /// </remarks>
    public sealed class DemoClip
    {
        public DemoClip(
            string name,
            IReadOnlyList<DemoStep> steps,
            int frameRate = 60,
            float settleSeconds = 0.8f,
            bool bootIntoGameplay = true,
            bool forceEnableInput = true)
        {
            Name = name;
            Steps = steps;
            FrameRate = frameRate;
            SettleSeconds = settleSeconds;
            BootIntoGameplay = bootIntoGameplay;
            ForceEnableInput = forceEnableInput;
        }

        public string Name { get; }

        public IReadOnlyList<DemoStep> Steps { get; }

        public int FrameRate { get; }

        /// <summary>Length of the leading idle segment that lets the camera settle.</summary>
        public float SettleSeconds { get; }

        /// <summary>Whether to start a new game before recording.</summary>
        public bool BootIntoGameplay { get; }

        /// <summary>
        /// Whether to re-enable player input every captured frame. <see cref="PlayerInputManager"/>
        /// disables itself when the Editor loses focus, which would silently drop the scripted input.
        /// </summary>
        public bool ForceEnableInput { get; }
    }
}
