using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Provides the creatable item type for melee weapon definitions.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Melee Weapon",
        menuName = "ZZ/Items/Weapons/Melee Weapon")]
    public class MeleeWeaponItem : WeaponItem
    {
        [Header("Critical Attacks")]
        [SerializeField, Min(0f)] private float m_riposteAttack01Modifier = 3.3f;

        /// <summary>Gets the damage multiplier for the first Riposte animation.</summary>
        public float RiposteAttack01Modifier => m_riposteAttack01Modifier;
    }
}
