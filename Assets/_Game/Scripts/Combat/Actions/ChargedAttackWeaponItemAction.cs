using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Resolves a fully charged heavy attack into its replicated release animation.
    /// </summary>
    [GameAsset(FileName = "Charged Attack", MenuName = "ZZ/Actions/Charged Attack")]
    public class ChargedAttackWeaponItemAction : WeaponItemBasedAction
    {
        [SerializeField] private AttackType m_attackType = AttackType.ChargedAttack01;

        /// <inheritdoc />
        public override void AttemptToPerformAction(PlayerManager player, WeaponItem weapon)
        {
            if (!CanPerformAttack(player))
            {
                return;
            }

            player.PlayerCombatManager?.TryStartAttack(m_attackType, weapon);
        }
    }
}
