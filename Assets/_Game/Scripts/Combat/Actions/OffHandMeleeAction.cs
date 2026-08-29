using UnityEngine;

namespace ZZ
{
    /// <summary>Starts the equipped off-hand weapon's sustained blocking action.</summary>
    [CreateAssetMenu(
        fileName = "Off Hand Melee Action",
        menuName = "ZZ/Weapon Actions/Off Hand Melee")]
    public class OffHandMeleeAction : WeaponItemBasedAction
    {
        /// <inheritdoc />
        public override void AttemptToPerformAction(
            PlayerManager player,
            WeaponItem weapon)
        {
            player?.PlayerCombatManager?.SetBlocking(true, weapon);
        }
    }
}
