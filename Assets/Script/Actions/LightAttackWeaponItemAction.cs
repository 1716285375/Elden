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
            if (!CanPerformAction(player))
            {
                return;
            }

            player.PlayerNetworkManager?.SetCharacterActionHand(true);
            player.PlayerCombatManager?.ReplicateAttack(m_attackType);
            player.CharacterNetworkManager?.NotifyServerOfAttackActionServerRpc(m_attackType);
        }

        private static bool CanPerformAction(PlayerManager player)
        {
            if (player == null || !player.IsOwner || player.IsPerformingAction)
            {
                return false;
            }

            if (!player.IsGrounded)
            {
                return false;
            }

            return player.CharacterNetworkManager != null &&
                player.CharacterNetworkManager.CurrentStamina.Value > 0f;
        }
    }
}
