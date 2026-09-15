using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Authored guard rules for one shield or weapon. Replaces the single hard-coded
    /// blocking dot so small, medium and great shields can differ.
    /// </summary>
    [GameAsset(FileName = "Guard Profile", MenuName = "ZZ/Combat/Guard Profile")]
    public sealed class GuardProfile : ScriptableObject
    {
        [Tooltip("Total guard arc in degrees, measured across the whole cone. 120 means +/-60 from forward.")]
        [SerializeField, Range(0f, 360f)] private float m_totalGuardArcDegrees = 120f;

        [Tooltip("Percentage of incoming guard stamina damage prevented.")]
        [SerializeField, Range(0f, 100f)] private float m_stability = 50f;

        [Header("Absorption (0-100)")]
        [SerializeField, Range(0f, 100f)] private float m_physicalAbsorption = 85f;
        [SerializeField, Range(0f, 100f)] private float m_magicAbsorption = 40f;
        [SerializeField, Range(0f, 100f)] private float m_fireAbsorption = 35f;
        [SerializeField, Range(0f, 100f)] private float m_lightningAbsorption = 25f;
        [SerializeField, Range(0f, 100f)] private float m_holyAbsorption = 35f;

        /// <summary>
        /// Forward dot an incoming hit must exceed for this guard to apply. A 120 degree total arc
        /// accepts anything within 60 degrees of forward, giving a threshold of cos(60) = 0.5.
        /// </summary>
        public float MinimumBlockingDot
        {
            get
            {
                float halfArc = Mathf.Clamp(m_totalGuardArcDegrees, 0f, 360f) * 0.5f * Mathf.Deg2Rad;
                return Mathf.Cos(Mathf.Clamp(halfArc, 0f, Mathf.PI));
            }
        }

        /// <summary>Total cone width in degrees, not the half-angle on each side.</summary>
        public float TotalGuardArcDegrees => Mathf.Clamp(m_totalGuardArcDegrees, 0f, 360f);
        public float Stability => Mathf.Clamp(m_stability, 0f, 100f);
        public float PhysicalAbsorption => Mathf.Clamp(m_physicalAbsorption, 0f, 100f);
        public float MagicAbsorption => Mathf.Clamp(m_magicAbsorption, 0f, 100f);
        public float FireAbsorption => Mathf.Clamp(m_fireAbsorption, 0f, 100f);
        public float LightningAbsorption => Mathf.Clamp(m_lightningAbsorption, 0f, 100f);
        public float HolyAbsorption => Mathf.Clamp(m_holyAbsorption, 0f, 100f);

        public float GetAbsorption(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Physical => PhysicalAbsorption,
                DamageType.Magic => MagicAbsorption,
                DamageType.Fire => FireAbsorption,
                DamageType.Lightning => LightningAbsorption,
                DamageType.Holy => HolyAbsorption,
                _ => PhysicalAbsorption
            };
        }
    }
}
