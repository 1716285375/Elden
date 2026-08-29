using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Shares one hit registry across every collider used by a single AI attack.
    /// </summary>
    public class AIDamageCollider : DamageCollider
    {
        private AICharacterCombatManager m_combatManager;

        /// <inheritdoc />
        protected override bool CheckForParry(CharacterManager damageTarget)
        {
            if (!CanParryDamageTarget(damageTarget))
            {
                return false;
            }

            m_combatManager ??= GetComponentInParent<AICharacterCombatManager>();
            if (m_combatManager != null &&
                !m_combatManager.TryRegisterDamageTarget(damageTarget))
            {
                return true;
            }

            return ProcessSuccessfulParry(damageTarget);
        }

        protected override void Damage(
            CharacterManager target,
            Vector3 contactPoint,
            bool wasBlocked)
        {
            m_combatManager ??= GetComponentInParent<AICharacterCombatManager>();
            if (m_combatManager == null ||
                !m_combatManager.TryRegisterDamageTarget(target))
            {
                return;
            }

            base.Damage(target, contactPoint, wasBlocked);
            m_combatManager.RecordSuccessfulHit(target);
        }
    }
}
