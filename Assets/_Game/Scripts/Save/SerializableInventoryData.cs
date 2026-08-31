using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>Stores one complete unequipped item container independently of runtime objects.</summary>
    [Serializable]
    public sealed class SerializableInventoryData
    {
        [SerializeField] private List<SerializableWeapon> m_weapons = new();
        [SerializeField] private List<SerializableRangeProjectile> m_projectiles = new();
        [SerializeField] private List<SerializableQuickSlotItem> m_quickSlotItems = new();
        [SerializeField] private List<SerializableItemStack> m_stackableItems = new();
        [SerializeField] private List<int> m_headEquipment = new();
        [SerializeField] private List<int> m_bodyEquipment = new();
        [SerializeField] private List<int> m_handEquipment = new();
        [SerializeField] private List<int> m_legEquipment = new();

        public List<SerializableWeapon> Weapons =>
            m_weapons ??= new List<SerializableWeapon>();

        public List<SerializableRangeProjectile> Projectiles =>
            m_projectiles ??= new List<SerializableRangeProjectile>();

        public List<SerializableQuickSlotItem> QuickSlotItems =>
            m_quickSlotItems ??= new List<SerializableQuickSlotItem>();

        public List<SerializableItemStack> StackableItems =>
            m_stackableItems ??= new List<SerializableItemStack>();

        public List<int> HeadEquipment =>
            m_headEquipment ??= new List<int>();

        public List<int> BodyEquipment =>
            m_bodyEquipment ??= new List<int>();

        public List<int> HandEquipment =>
            m_handEquipment ??= new List<int>();

        public List<int> LegEquipment =>
            m_legEquipment ??= new List<int>();

        /// <summary>Removes every saved entry before rebuilding one runtime snapshot.</summary>
        public void Clear()
        {
            Weapons.Clear();
            Projectiles.Clear();
            QuickSlotItems.Clear();
            StackableItems.Clear();
            HeadEquipment.Clear();
            BodyEquipment.Clear();
            HandEquipment.Clear();
            LegEquipment.Clear();
        }
    }
}
