using UnityEngine;

namespace ZZ
{
    /// <summary>Validates the equipped spell and starts a catalyst casting action.</summary>
    [GameAsset(
        FileName = "Cast Incantation",
        MenuName = "ZZ/Weapon Actions/Cast Incantation")]
    public class CastIncantationAction : WeaponItemAction
    {
        [SerializeField] private SpellClass m_requiredSpellClass = SpellClass.Incantation;

        /// <inheritdoc />
        public override void AttemptToPerformAction(
            PlayerManager player,
            WeaponItem weapon)
        {
            CasterWeaponItem casterWeapon = weapon as CasterWeaponItem;
            SpellItem spell = player?.InventoryManager?.CurrentSpell;
            if (casterWeapon == null ||
                spell == null ||
                casterWeapon.SpellClass != m_requiredSpellClass ||
                spell.SpellClass != m_requiredSpellClass)
            {
                return;
            }

            bool isRightHand = player.PlayerNetworkManager == null ||
                player.PlayerNetworkManager.IsUsingLeftHand.Value == false;
            spell.AttemptToCastSpell(player, casterWeapon, isRightHand);
        }
    }
}
