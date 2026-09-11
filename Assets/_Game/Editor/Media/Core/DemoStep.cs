using System;

namespace ZZ.Demo
{
    /// <summary>
    /// One timed segment of a demo recording. Input is held for the whole segment, so a button
    /// press is modelled as a short segment followed by a released one.
    /// </summary>
    public sealed class DemoStep
    {
        public DemoStep(string label, float duration, DemoInputState input, Action onEnter = null)
        {
            Label = label;
            Duration = duration;
            Input = input;
            OnEnter = onEnter;
        }

        public string Label { get; }

        /// <summary>Length in seconds. The director rounds this to whole frames.</summary>
        public float Duration { get; }

        public DemoInputState Input { get; }

        /// <summary>Runs once when the segment starts, for things input cannot express.</summary>
        public Action OnEnter { get; }
    }
}
