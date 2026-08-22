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
            if (m_aiCharacter == null ||
                !m_aiCharacter.IsServer ||
                m_aiCharacter.IsDead ||
                m_aiCharacter.IsPerformingAction)
            {
                return false;
            }

            PrepareAttackDamage();
            ReplicateAttack(AttackType.LightAttack01);
            m_aiCharacter.CharacterNetworkManager
                ?.NotifyServerOfAttackActionServerRpc(AttackType.LightAttack01);
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
