using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Describes one data-driven AI attack and its optional animation-window combo follow-up.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Character/AI/Attack Action",
        fileName = "AI Attack Action")]
    public class AICharacterAttackAction : ScriptableObject
    {
        [Header("Presentation")]
        [SerializeField] private AttackType m_attackType = AttackType.LightAttack01;
        [SerializeField] private bool m_isParryable = true;
        [SerializeField] private bool m_useCharacterActionAnimation;
        [SerializeField] private CharacterActionAnimation m_characterActionAnimation =
            CharacterActionAnimation.BowDraw;

        [Header("Selection")]
        [SerializeField, Min(0f)] private float m_minimumRange;
        [SerializeField, Min(0.01f)] private float m_maximumRange = 2.5f;
        [SerializeField, Min(0f)] private float m_selectionWeight = 1f;
        [SerializeField, Min(0f)] private float m_recoveryTime = 2f;

        [Header("Combo")]
        [SerializeField] private AICharacterAttackAction m_comboAction;

        [Header("Damage")]
        [SerializeField, Min(0f)] private float m_physicalDamage = 25f;
        [SerializeField, Min(0f)] private float m_magicDamage;
        [SerializeField, Min(0f)] private float m_fireDamage;
        [SerializeField, Min(0f)] private float m_lightningDamage;
        [SerializeField, Min(0f)] private float m_holyDamage;
        [SerializeField, Min(0f)] private float m_poiseDamage = 15f;

        public AttackType AttackType => m_attackType;
        public bool IsParryable => m_isParryable;
        public bool UseCharacterActionAnimation => m_useCharacterActionAnimation;
        public CharacterActionAnimation CharacterActionAnimation =>
            m_characterActionAnimation;
        public float MinimumRange => m_minimumRange;
        public float MaximumRange => m_maximumRange;
        public float SelectionWeight => m_selectionWeight;
        public float RecoveryTime => m_recoveryTime;
        public AICharacterAttackAction ComboAction => m_comboAction;
        public float PhysicalDamage => m_physicalDamage;
        public float MagicDamage => m_magicDamage;
        public float FireDamage => m_fireDamage;
        public float LightningDamage => m_lightningDamage;
        public float HolyDamage => m_holyDamage;
        public float PoiseDamage => m_poiseDamage;

        /// <summary>Gets whether the target is inside this attack's authored range.</summary>
        public bool IsInRange(float targetDistance)
        {
            float distance = Mathf.Max(0f, targetDistance);
            return distance >= m_minimumRange && distance <= m_maximumRange;
        }

        protected virtual void OnValidate()
        {
            m_minimumRange = Mathf.Max(0f, m_minimumRange);
            m_maximumRange = Mathf.Max(m_minimumRange, m_maximumRange);
        }
    }
}
