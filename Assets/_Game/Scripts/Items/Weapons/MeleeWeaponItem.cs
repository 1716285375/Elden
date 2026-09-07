using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Provides the creatable item type for melee weapon definitions.
    /// </summary>
    [GameAsset(
        FileName = "Melee Weapon",
        MenuName = "ZZ/Items/Weapons/Melee Weapon")]
    public class MeleeWeaponItem : WeaponItem
    {
        [Header("Critical Attacks")]
        [SerializeField, Min(0f)] private float m_riposteAttack01Modifier = 3.3f;
        [SerializeField, Min(0f)] private float m_backstabAttack01Modifier = 2.8f;

        /// <summary>Gets the damage multiplier for the first Riposte animation.</summary>
        public float RiposteAttack01Modifier => m_riposteAttack01Modifier;

        /// <summary>Gets the damage multiplier for the first Backstab animation.</summary>
        public float BackstabAttack01Modifier => m_backstabAttack01Modifier;
    }
}
