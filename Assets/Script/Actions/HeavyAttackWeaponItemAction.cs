using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Resolves a heavy attack intent into a validated, replicated attack animation.
    /// </summary>
    [CreateAssetMenu(fileName = "Heavy Attack", menuName = "ZZ/Actions/Heavy Attack")]
    public class HeavyAttackWeaponItemAction : WeaponItemBasedAction
    {
        [SerializeField] private AttackType m_attackType = AttackType.HeavyAttack01;

        /// <inheritdoc />
        public override void AttemptToPerformAction(PlayerManager player, WeaponItem weapon)
        {
            if (player?.IsPerformingAction == true)
            {
                player.PlayerCombatManager?.TryPerformMainHandCombo(m_attackType);
                return;
            }

            if (!CanPerformAttack(player))
            {
                return;
            }

            player.PlayerCombatManager?.DisableCanCombo();
            player.PlayerNetworkManager?.SetCharacterActionHand(true);
            player.PlayerCombatManager?.ReplicateAttack(m_attackType, weapon);
            player.CharacterNetworkManager?.NotifyServerOfAttackActionServerRpc(m_attackType);
        }
    }
}
