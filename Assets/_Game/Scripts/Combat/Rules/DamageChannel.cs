using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// One authored damage channel: a base value plus its own attribute scaling. This is what
    /// makes Fire (STR/FAI) differ from Lightning (DEX/FAI) on the same weapon.
    /// </summary>
    [Serializable]
    public struct DamageChannel
    {
        [SerializeField, Min(0f)] private float m_baseDamage;

        [Header("Attribute Scaling")]
        [SerializeField, Range(0f, 2f)] private float m_strengthScaling;
        [SerializeField, Range(0f, 2f)] private float m_dexterityScaling;
        [SerializeField, Range(0f, 2f)] private float m_intelligenceScaling;
        [SerializeField, Range(0f, 2f)] private float m_faithScaling;

        public DamageChannel(float baseDamage, float strengthScaling, float dexterityScaling,
            float intelligenceScaling, float faithScaling)
        {
            m_baseDamage = baseDamage;
            m_strengthScaling = strengthScaling;
            m_dexterityScaling = dexterityScaling;
            m_intelligenceScaling = intelligenceScaling;
            m_faithScaling = faithScaling;
        }

        public float BaseDamage => Mathf.Max(0f, m_baseDamage);
        public float StrengthScaling => Mathf.Clamp(m_strengthScaling, 0f, 2f);
        public float DexterityScaling => Mathf.Clamp(m_dexterityScaling, 0f, 2f);
        public float IntelligenceScaling => Mathf.Clamp(m_intelligenceScaling, 0f, 2f);
        public float FaithScaling => Mathf.Clamp(m_faithScaling, 0f, 2f);
    }
}
