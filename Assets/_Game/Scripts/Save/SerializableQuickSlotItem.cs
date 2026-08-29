using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Stores one gameplay quick-slot item and its instance amount.</summary>
    [Serializable]
    public sealed class SerializableQuickSlotItem
    {
        [SerializeField] private int m_itemID = -1;
        [SerializeField, Min(0)] private int m_itemAmount;

        public SerializableQuickSlotItem()
        {
        }

        public SerializableQuickSlotItem(int itemID, int itemAmount)
        {
            ItemID = itemID;
            ItemAmount = itemAmount;
        }

        /// <summary>Gets or sets the stable quick-slot item identifier.</summary>
        public int ItemID
        {
            get => m_itemID;
            set => m_itemID = Mathf.Max(-1, value);
        }

        /// <summary>Gets or sets the remaining instance amount.</summary>
        public int ItemAmount
        {
            get => m_itemAmount;
            set => m_itemAmount = Mathf.Max(0, value);
        }
    }
}
