using UnityEngine;

namespace ZZ
{
    /// <summary>Defines a catalyst weapon and the spell class it can release.</summary>
    [GameAsset(
        FileName = "Caster Weapon",
        MenuName = "ZZ/Items/Weapons/Caster Weapon")]
    public class CasterWeaponItem : WeaponItem
    {
        [Header("Spell Casting")]
        [SerializeField] private SpellClass m_spellClass = SpellClass.Incantation;

        /// <summary>Gets the spell class accepted by this catalyst.</summary>
        public SpellClass SpellClass => m_spellClass;
    }
}
