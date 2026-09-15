using UnityEngine;

namespace ZZ
{
    /// <summary>Shared status authoring data; active timers and buildup remain on runtime characters.</summary>
    [CreateAssetMenu(menuName = "ZZ/Combat/Status Effect Definition")]
    public sealed class StatusEffectDefinition : ScriptableObject
    {
        [SerializeField] private Buildup m_status;
        [SerializeField, Range(0f, 100f)] private float m_maxHealthPercent = 12f;
        [SerializeField, Min(0f)] private float m_flatDamage = 20f;
        [SerializeField] private TimedCharacterEffect m_timedEffect;

        public float MaxHealthPercent => Mathf.Clamp(m_maxHealthPercent, 0f, 100f);
        public float FlatDamage => Mathf.Max(0f, m_flatDamage);
        public Buildup Status => m_status;
        public TimedCharacterEffect TimedEffect => m_timedEffect;

        public float CalculateProcDamage(float maxHealth)
        {
            return Mathf.Max(0f, maxHealth) * Mathf.Clamp01(m_maxHealthPercent * 0.01f) + Mathf.Max(0f, m_flatDamage);
        }
    }
}
