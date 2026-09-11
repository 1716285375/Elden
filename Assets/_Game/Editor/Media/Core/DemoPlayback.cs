using System.Collections.Generic;
using UnityEngine;

namespace ZZ.Demo
{
    /// <summary>
    /// Advances a <see cref="DemoClip"/> one frame at a time and exposes the input intent for the
    /// current frame. This is the piece an in-game replay module reuses.
    /// </summary>
    /// <remarks>
    /// Owns no Unity objects, reads no time source and performs no I/O: callers own the tick and
    /// the frame rate. The Editor drives it from <c>EditorApplication.update</c>; a runtime module
    /// would drive it from <c>Update</c> or a <c>PlayerLoop</c> injection without changing it.
    /// </remarks>
    public sealed class DemoPlayback
    {
        private readonly List<DemoStep> m_steps;
        private DemoStep m_currentStep;
        private int m_stepIndex;
        private int m_framesRemainingInStep;

        public DemoPlayback(DemoClip clip)
        {
            Clip = clip;
            m_steps = new List<DemoStep>(clip.Steps);
            if (clip.SettleSeconds > 0f)
            {
                m_steps.Insert(0, new DemoStep("settle", clip.SettleSeconds, default));
            }

            int totalFrames = 0;
            foreach (DemoStep step in m_steps)
            {
                totalFrames += FrameCountOf(step);
            }

            TotalFrames = totalFrames;
            Reset();
        }

        public DemoClip Clip { get; }

        public int FrameRate => Clip.FrameRate;

        /// <summary>Total frames the clip will produce, available up front for progress reporting.</summary>
        public int TotalFrames { get; }

        public int FrameIndex { get; private set; }

        /// <summary>Seconds elapsed at the start of the frame just advanced.</summary>
        public float Elapsed { get; private set; }

        public DemoInputState CurrentInput { get; private set; }

        public bool IsFinished { get; private set; }

        public string CurrentStepLabel => m_currentStep?.Label ?? string.Empty;

        /// <summary>Rewinds to the first frame so the same clip can be played again.</summary>
        public void Reset()
        {
            m_stepIndex = 0;
            m_framesRemainingInStep = 0;
            m_currentStep = null;
            FrameIndex = 0;
            Elapsed = 0f;
            CurrentInput = default;
            IsFinished = m_steps.Count == 0;
        }

        /// <summary>
        /// Advances one frame, running the next segment's entry action when the previous one ends.
        /// Call this once per rendered frame, then read <see cref="CurrentInput"/>.
        /// </summary>
        public void AdvanceFrame()
        {
            if (IsFinished)
            {
                return;
            }

            if (m_framesRemainingInStep <= 0 && !BeginNextStep())
            {
                IsFinished = true;
                m_currentStep = null;
                CurrentInput = default;
                return;
            }

            Elapsed = FrameIndex / (float)FrameRate;
            CurrentInput = m_currentStep.Input;
            m_framesRemainingInStep--;
            FrameIndex++;
        }

        private bool BeginNextStep()
        {
            if (m_stepIndex >= m_steps.Count)
            {
                return false;
            }

            DemoStep step = m_steps[m_stepIndex++];
            step.OnEnter?.Invoke();
            m_currentStep = step;
            m_framesRemainingInStep = FrameCountOf(step);
            return true;
        }

        private int FrameCountOf(DemoStep step)
        {
            return Mathf.Max(1, Mathf.RoundToInt(step.Duration * FrameRate));
        }
    }
}
