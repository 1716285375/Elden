using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Shares one hit registry across every collider used by a single AI attack.
    /// </summary>
    public class AIDamageCollider : DamageCollider
    {
        protected override bool UsesWeaponTrace => true;

        private AICharacterCombatManager m_combatManager;

        /// <inheritdoc />
        public override void NotifyParry(CharacterManager parryingCharacter)
        {
            if (parryingCharacter == null)
            {
                return;
            }

            m_combatManager ??= GetComponentInParent<AICharacterCombatManager>();
            bool wasRegisteredByServer = m_combatManager == null ||
                m_combatManager.TryRegisterDamageTarget(parryingCharacter);
            if (!wasRegisteredByServer && !parryingCharacter.IsOwner)
            {
                return;
            }

            base.NotifyParry(parryingCharacter);
        }

        /// <inheritdoc />
        protected override void DispatchDamage(
            CharacterManager target,
            in DamagePayload payload,
            Vector3 contactPoint,
            bool wasBlocked)
        {
            m_combatManager ??= GetComponentInParent<AICharacterCombatManager>();
            if (m_combatManager == null)
            {
                return;
            }

            bool wasRegisteredByServer =
                m_combatManager.TryRegisterDamageTarget(target);
            if (!wasRegisteredByServer && !target.IsOwner)
            {
                return;
            }

            base.DispatchDamage(target, payload, contactPoint, wasBlocked);
            if (wasRegisteredByServer)
            {
                m_combatManager.RecordSuccessfulHit(target);
            }
        }
    }
}
