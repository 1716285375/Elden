using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Resolves a light attack intent into a validated, replicated attack animation.
    /// </summary>
    [CreateAssetMenu(fileName = "Light Attack", menuName = "ZZ/Actions/Light Attack")]
    public class LightAttackWeaponItemAction : WeaponItemBasedAction
    {
        [SerializeField] private AttackType m_attackType = AttackType.LightAttack01;

        /// <inheritdoc />
        public override void AttemptToPerformAction(PlayerManager player, WeaponItem weapon)
        {
            if (player?.IsPerformingAction == true)
            {
                if (player.PlayerCombatManager?.TryPerformCommittedAttack(weapon) == true)
                {
                    return;
                }

                player.PlayerCombatManager?.TryPerformMainHandCombo(m_attackType);
                return;
            }

            if (player?.PlayerCombatManager?.TryPerformRunningAttack(weapon) == true)
            {
                return;
            }

            if (!CanPerformAttack(player))
            {
                return;
            }

            player.PlayerCombatManager?.DisableCanCombo();
            player.PlayerNetworkManager?.SetCharacterActionHand(
                player.PlayerNetworkManager.IsTwoHandingLeftWeapon.Value == false);
            player.PlayerCombatManager?.ReplicateAttack(m_attackType, weapon);
            player.CharacterNetworkManager?.NotifyServerOfAttackActionServerRpc(m_attackType);
        }
    }
}
