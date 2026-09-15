using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// A half-open normalized range [Start, End) inside one action clip. Values outside 0..1 are
    /// clamped on read so authored windows cannot silently invert.
    /// </summary>
    [Serializable]
    public struct ActionWindow
    {
        [SerializeField, Range(0f, 1f)] private float m_start;
        [SerializeField, Range(0f, 1f)] private float m_end;

        public ActionWindow(float start, float end)
        {
            m_start = start;
            m_end = end;
        }

        public float Start => m_start;
        public float End => m_end;

        public bool IsOpen => m_end > m_start;

        public bool Contains(float normalizedTime)
        {
            if (!IsOpen || float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime))
            {
                return false;
            }

            return normalizedTime >= m_start && normalizedTime < m_end;
        }

        public static ActionWindow None => new ActionWindow(0f, 0f);
    }
}
