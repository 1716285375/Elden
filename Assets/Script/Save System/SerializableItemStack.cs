using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Stores one generic stackable inventory item's stable identity and amount.</summary>
    [Serializable]
    public sealed class SerializableItemStack
    {
        [SerializeField] private int m_itemID = -1;
        [SerializeField, Min(0)] private int m_itemAmount;

        public SerializableItemStack()
        {
        }

        public SerializableItemStack(int itemID, int itemAmount)
        {
            ItemID = itemID;
            ItemAmount = itemAmount;
        }

        public int ItemID
        {
            get => m_itemID;
            set => m_itemID = Mathf.Max(-1, value);
        }

        public int ItemAmount
        {
            get => m_itemAmount;
            set => m_itemAmount = Mathf.Max(0, value);
        }
    }
}
