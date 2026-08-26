using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Defines the starting attributes and equipment applied by one character class.
    /// </summary>
    [Serializable]
    public class CharacterClass
    {
        private const int k_QuickSlotCount = 3;

        [SerializeField] private string m_className = "Wanderer";

        [Header("Attributes")]
        [SerializeField, Min(1)] private int m_vitality = 10;
        [SerializeField, Min(1)] private int m_endurance = 10;
        [SerializeField, Min(1)] private int m_mind = 10;
        [SerializeField, Min(1)] private int m_strength = 10;
        [SerializeField, Min(1)] private int m_dexterity = 10;
        [SerializeField, Min(1)] private int m_intelligence = 10;
        [SerializeField, Min(1)] private int m_faith = 10;

        [Header("Equipment")]
        [SerializeField] private WeaponItem[] m_rightHandWeapons =
            new WeaponItem[k_QuickSlotCount];
        [SerializeField] private WeaponItem[] m_leftHandWeapons =
            new WeaponItem[k_QuickSlotCount];
        [SerializeField] private HeadEquipmentItem m_headEquipment;
        [SerializeField] private BodyEquipmentItem m_bodyEquipment;
        [SerializeField] private HandEquipmentItem m_handEquipment;
        [SerializeField] private LegEquipmentItem m_legEquipment;
        [SerializeField] private QuickSlotItem[] m_quickSlotItems =
            new QuickSlotItem[k_QuickSlotCount];
        [SerializeField] private int[] m_quickSlotItemAmounts = { 3, 1, 0 };

        /// <summary>Creates an Inspector-authored class with default values.</summary>
        public CharacterClass()
        {
        }

        /// <summary>Creates one runtime class from catalog-backed starting items.</summary>
        public CharacterClass(
            string className,
            int vitality,
            int endurance,
            int mind,
            int strength,
            int dexterity,
            int intelligence,
            int faith,
            WeaponItem[] rightHandWeapons,
            WeaponItem[] leftHandWeapons,
            HeadEquipmentItem headEquipment,
            BodyEquipmentItem bodyEquipment,
            HandEquipmentItem handEquipment,
            LegEquipmentItem legEquipment,
            QuickSlotItem[] quickSlotItems,
            int[] quickSlotItemAmounts)
        {
            m_className = className;
            m_vitality = vitality;
            m_endurance = endurance;
            m_mind = mind;
            m_strength = strength;
            m_dexterity = dexterity;
            m_intelligence = intelligence;
            m_faith = faith;
            m_rightHandWeapons = rightHandWeapons;
            m_leftHandWeapons = leftHandWeapons;
            m_headEquipment = headEquipment;
            m_bodyEquipment = bodyEquipment;
            m_handEquipment = handEquipment;
            m_legEquipment = legEquipment;
            m_quickSlotItems = quickSlotItems;
            m_quickSlotItemAmounts = quickSlotItemAmounts;
        }

        /// <summary>Gets the player-facing class name.</summary>
        public string ClassName => string.IsNullOrWhiteSpace(m_className)
            ? "Wanderer"
            : m_className.Trim();
        /// <summary>Gets starting Vitality.</summary>
        public int Vitality => Mathf.Max(1, m_vitality);
        /// <summary>Gets starting Endurance.</summary>
        public int Endurance => Mathf.Max(1, m_endurance);
        /// <summary>Gets starting Mind.</summary>
        public int Mind => Mathf.Max(1, m_mind);
        /// <summary>Gets starting Strength.</summary>
        public int Strength => Mathf.Max(1, m_strength);
        /// <summary>Gets starting Dexterity.</summary>
        public int Dexterity => Mathf.Max(1, m_dexterity);
        /// <summary>Gets starting Intelligence.</summary>
        public int Intelligence => Mathf.Max(1, m_intelligence);
        /// <summary>Gets starting Faith.</summary>
        public int Faith => Mathf.Max(1, m_faith);
        /// <summary>Gets the three right-hand starting slots.</summary>
        public WeaponItem[] RightHandWeapons => m_rightHandWeapons;
        /// <summary>Gets the three left-hand starting slots.</summary>
        public WeaponItem[] LeftHandWeapons => m_leftHandWeapons;
        /// <summary>Gets starting head equipment.</summary>
        public HeadEquipmentItem HeadEquipment => m_headEquipment;
        /// <summary>Gets starting body equipment.</summary>
        public BodyEquipmentItem BodyEquipment => m_bodyEquipment;
        /// <summary>Gets starting hand equipment.</summary>
        public HandEquipmentItem HandEquipment => m_handEquipment;
        /// <summary>Gets starting leg equipment.</summary>
        public LegEquipmentItem LegEquipment => m_legEquipment;
        /// <summary>Gets the three gameplay quick slots.</summary>
        public QuickSlotItem[] QuickSlotItems => m_quickSlotItems;

        /// <summary>Returns the authored starting amount for one quick slot.</summary>
        public int GetQuickSlotItemAmount(int slotIndex)
        {
            return m_quickSlotItemAmounts != null &&
                slotIndex >= 0 &&
                slotIndex < m_quickSlotItemAmounts.Length
                    ? Mathf.Max(0, m_quickSlotItemAmounts[slotIndex])
                    : 0;
        }
    }
}
