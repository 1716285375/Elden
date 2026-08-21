using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Defines the authored model, damage, requirements, and costs shared by weapons.
    /// </summary>
    public abstract class WeaponItem : Item
    {
        [Header("Weapon Model")]
        [SerializeField] private GameObject m_weaponModel;
        [SerializeField] private bool m_isUnarmed;

        [Header("Base Damage")]
        [SerializeField, Min(0f)] private float m_physicalDamage;
        [SerializeField, Min(0f)] private float m_magicDamage;
        [SerializeField, Min(0f)] private float m_fireDamage;
        [SerializeField, Min(0f)] private float m_lightningDamage;
        [SerializeField, Min(0f)] private float m_holyDamage;

        [Header("Attribute Requirements")]
        [SerializeField, Min(0)] private int m_strengthRequirement;
        [SerializeField, Min(0)] private int m_dexterityRequirement;
        [SerializeField, Min(0)] private int m_intelligenceRequirement;
        [SerializeField, Min(0)] private int m_faithRequirement;

        [Header("Action Costs")]
        [SerializeField, Min(0f)] private float m_baseStaminaCost;
        [SerializeField, Min(0f)] private float m_basePoiseDamage;

        /// <summary>Gets the prefab instantiated by an equipment slot.</summary>
        public GameObject WeaponModel => m_weaponModel;

        /// <summary>Gets whether this item represents the non-null unarmed fallback.</summary>
        public bool IsUnarmed => m_isUnarmed;

        /// <summary>Gets the weapon's base physical damage.</summary>
        public float PhysicalDamage => m_physicalDamage;

        /// <summary>Gets the weapon's base magic damage.</summary>
        public float MagicDamage => m_magicDamage;

        /// <summary>Gets the weapon's base fire damage.</summary>
        public float FireDamage => m_fireDamage;

        /// <summary>Gets the weapon's base lightning damage.</summary>
        public float LightningDamage => m_lightningDamage;

        /// <summary>Gets the weapon's base holy damage.</summary>
        public float HolyDamage => m_holyDamage;

        /// <summary>Gets the reserved strength requirement.</summary>
        public int StrengthRequirement => m_strengthRequirement;

        /// <summary>Gets the reserved dexterity requirement.</summary>
        public int DexterityRequirement => m_dexterityRequirement;

        /// <summary>Gets the reserved intelligence requirement.</summary>
        public int IntelligenceRequirement => m_intelligenceRequirement;

        /// <summary>Gets the reserved faith requirement.</summary>
        public int FaithRequirement => m_faithRequirement;

        /// <summary>Gets the base stamina cost reserved for weapon actions.</summary>
        public float BaseStaminaCost => m_baseStaminaCost;

        /// <summary>Gets the poise damage forwarded to the damage effect.</summary>
        public float BasePoiseDamage => m_basePoiseDamage;
    }
}
