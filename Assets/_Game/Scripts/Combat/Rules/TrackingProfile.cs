using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Authored aim assistance applied while an attack's rotation window is open.</summary>
    [Serializable]
    public struct TrackingProfile
    {
        [Tooltip("Degrees per second the attacker turns towards its lock-on target. 0 disables tracking.")]
        [SerializeField, Min(0f)] private float m_turnSpeed;

        [Tooltip("Maximum angle from the attacker's forward that still receives tracking.")]
        [SerializeField, Range(0f, 180f)] private float m_maximumAngle;

        [Tooltip("Distance within which tracking is allowed. 0 means unlimited.")]
        [SerializeField, Min(0f)] private float m_maximumDistance;

        public float TurnSpeed => Mathf.Max(0f, m_turnSpeed);
        public float MaximumAngle => Mathf.Clamp(m_maximumAngle, 0f, 180f);
        public float MaximumDistance => Mathf.Max(0f, m_maximumDistance);

        public bool IsEnabled => m_turnSpeed > 0f;

        public static TrackingProfile None => new TrackingProfile();
    }
}
