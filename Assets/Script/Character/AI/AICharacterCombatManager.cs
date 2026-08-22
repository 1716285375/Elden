using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Owns the server-authoritative AI attack and its animation-driven hit windows.
    /// </summary>
    [RequireComponent(typeof(AICharacterManager))]
    public class AICharacterCombatManager : CharacterCombatManager
    {
        [Header("Damage Colliders")]
        [SerializeField] private AIDamageCollider m_leftHandDamageCollider;
        [SerializeField] private AIDamageCollider m_rightHandDamageCollider;

        [Header("Attack Damage")]
        [SerializeField, Min(0f)] private float m_physicalDamage = 25f;
        [SerializeField, Min(0f)] private float m_poiseDamage = 15f;

        private readonly HashSet<CharacterManager> m_charactersDamaged = new();

        private AICharacterManager m_aiCharacter;
        private BossAttackData m_currentBossAttack;

        protected override void Awake()
        {
            base.Awake();
            m_aiCharacter = GetComponent<AICharacterManager>();
            ConfigureDamageColliders();
            CloseDamageColliders();
        }

        /// <summary>Starts the predicted server attack and replicates it to clients.</summary>
        public bool PerformAttack()
        {
            return PerformAttack(null);
        }

        /// <summary>Starts one server-selected data-driven attack.</summary>
        public bool PerformAttack(BossAttackData bossAttack)
        {
            if (m_aiCharacter == null ||
                !m_aiCharacter.IsServer ||
                m_aiCharacter.IsDead ||
                m_aiCharacter.IsPerformingAction)
            {
                return false;
            }

            m_currentBossAttack = bossAttack;
            PrepareAttackDamage();
            AttackType attackType = bossAttack != null
                ? bossAttack.AttackType
                : AttackType.LightAttack01;
            ReplicateAttack(attackType);
            m_aiCharacter.CharacterNetworkManager
                ?.NotifyServerOfAttackActionServerRpc(attackType);
            return m_aiCharacter.IsPerformingAction;
        }

        /// <summary>Refreshes the damage payload and clears the per-attack hit registry.</summary>
        public void PrepareAttackDamage()
        {
            m_charactersDamaged.Clear();
            ConfigureDamageColliders();
        }

        /// <summary>Opens the left-hand active frames on the server.</summary>
        public void OpenLeftHandDamageCollider()
        {
            if (m_aiCharacter != null && m_aiCharacter.IsServer)
            {
                m_leftHandDamageCollider?.OpenDamageCollider();
            }
        }

        /// <summary>Closes the left-hand active frames.</summary>
        public void CloseLeftHandDamageCollider()
        {
            m_leftHandDamageCollider?.CloseDamageCollider();
        }

        /// <summary>Opens the right-hand active frames on the server.</summary>
        public void OpenRightHandDamageCollider()
        {
            if (m_aiCharacter != null && m_aiCharacter.IsServer)
            {
                m_rightHandDamageCollider?.OpenDamageCollider();
            }
        }

        /// <summary>Closes every active AI damage collider.</summary>
        public void CloseDamageColliders()
        {
            m_leftHandDamageCollider?.CloseDamageCollider();
            m_rightHandDamageCollider?.CloseDamageCollider();
        }

        internal bool TryRegisterDamageTarget(CharacterManager target)
        {
            return m_aiCharacter != null &&
                m_aiCharacter.IsServer &&
                target != null &&
                target != m_aiCharacter &&
                !target.IsDead &&
                m_charactersDamaged.Add(target);
        }

        private void ConfigureDamageColliders()
        {
            ConfigureDamageCollider(m_leftHandDamageCollider);
            ConfigureDamageCollider(m_rightHandDamageCollider);
        }

        private void ConfigureDamageCollider(AIDamageCollider damageCollider)
        {
            if (damageCollider == null)
            {
                return;
            }

            damageCollider.SetDamageSource(m_aiCharacter);
            if (m_currentBossAttack != null)
            {
                damageCollider.SetDamageValues(
                    m_currentBossAttack.PhysicalDamage,
                    m_currentBossAttack.MagicDamage,
                    m_currentBossAttack.FireDamage,
                    m_currentBossAttack.LightningDamage,
                    m_currentBossAttack.HolyDamage,
                    m_currentBossAttack.PoiseDamage);
                return;
            }

            damageCollider.SetDamageValues(
                m_physicalDamage,
                0f,
                0f,
                0f,
                0f,
                m_poiseDamage);
        }
    }
}
