using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Shows authoritative values immediately and a delayed damage trail without changing combat time.</summary>
    public sealed class HudResourceMeter
    {
        private readonly VisualElement m_container;
        private readonly VisualElement m_fill;
        private readonly VisualElement m_trail;
        private readonly Label m_value;
        private IVisualElementScheduledItem m_trailTask;
        private float m_ratio;
        private bool m_initialized;

        public bool IsLow { get; private set; }

        public HudResourceMeter(VisualElement root, string name)
        {
            m_container = root.Q(name);
            m_fill = root.Q(name + "-fill");
            m_trail = root.Q(name + "-trail");
            m_value = root.Q<Label>(name + "-value");
        }

        public void SetValue(float current, float maximum)
        {
            float ratio = Normalize(current, maximum);
            bool damaged = m_initialized && ratio < m_ratio;
            bool changed = !m_initialized || !Mathf.Approximately(ratio, m_ratio);
            IsLow = maximum > 0f && ratio <= 0.2f;
            m_container.EnableInClassList("resource--low", IsLow);
            m_value.text = $"{Mathf.CeilToInt(Sanitize(current))} / {Mathf.CeilToInt(Sanitize(maximum))}";
            m_fill.style.width = Length.Percent(ratio * 100f);
            if (changed)
            {
                CancelTrail();
                if (damaged)
                {
                    m_trailTask = m_trail.schedule.Execute(() =>
                        m_trail.style.width = Length.Percent(ratio * 100f)).StartingIn(250);
                }
                else
                {
                    m_trail.style.width = Length.Percent(ratio * 100f);
                }
            }
            m_ratio = ratio;
            m_initialized = true;
        }

        public void CancelTrail()
        {
            m_trailTask?.Pause();
            m_trailTask = null;
        }

        /// <summary>Maps potentially corrupt or uninitialized stats to a finite display range.</summary>
        public static float Normalize(float current, float maximum)
        {
            maximum = Sanitize(maximum);
            return maximum > 0f ? Mathf.Clamp01(Sanitize(current) / maximum) : 0f;
        }

        private static float Sanitize(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }
}
