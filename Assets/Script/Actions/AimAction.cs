using UnityEngine;

namespace ZZ
{
    /// <summary>Enters first-person free aim for an equipped ranged weapon.</summary>
    [CreateAssetMenu(fileName = "Aim", menuName = "ZZ/Actions/Aim")]
    public class AimAction : WeaponItemAction
    {
        /// <inheritdoc />
        public override void AttemptToPerformAction(
            PlayerManager player,
            WeaponItem weapon)
        {
            if (player == null ||
                weapon is not RangedWeaponItem ||
                !player.IsOwner ||
                !player.IsGrounded ||
                player.IsJumping ||
                player.IsDead ||
                player.LockOnManager?.IsLockedOn == true)
            {
                return;
            }

            bool isRightHandWeapon =
                player.InventoryManager?.CurrentRightHandWeapon == weapon;
            if (player.PlayerNetworkManager?.EnsureTwoHandWeapon(
                    isRightHandWeapon) != true)
            {
                return;
            }

            player.PlayerNetworkManager.SetAimingState(true);
        }
    }
}
