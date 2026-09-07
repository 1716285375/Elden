using UnityEngine;

namespace ZZ
{
    /// <summary>Blocks with shields/two-handed weapons, or attacks with a separately held melee weapon.</summary>
    [GameAsset(
        FileName = "Off Hand Melee Action",
        MenuName = "ZZ/Weapon Actions/Off Hand Melee")]
    public class OffHandMeleeAction : WeaponItemBasedAction
    {
        /// <inheritdoc />
        public override void AttemptToPerformAction(
            PlayerManager player,
            WeaponItem weapon)
        {
            if (player == null || weapon == null)
            {
                return;
            }
            if (weapon.WeaponClass == WeaponClass.Shield || weapon.IsUnarmed ||
                player.PlayerNetworkManager.IsTwoHandingWeapon.Value)
            {
                player.PlayerCombatManager.SetBlocking(true, weapon);
                return;
            }
            if (player.IsPerformingAction)
            {
                player.PlayerCombatManager.TryPerformMainHandCombo(AttackType.LightAttack01);
                return;
            }
            if (CanPerformAttack(player))
            {
                player.PlayerCombatManager.SetBlocking(false);
                PerformAttack(player, weapon, AttackType.LightAttack01);
            }
        }
    }
}
