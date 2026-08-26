using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Stores one ammunition template identifier and its remaining amount.</summary>
    [Serializable]
    public sealed class SerializableRangeProjectile
    {
        [SerializeField] private int m_itemID = -1;
        [SerializeField, Min(0)] private int m_itemAmount;

        public SerializableRangeProjectile()
        {
        }

        public SerializableRangeProjectile(int itemID, int itemAmount)
        {
            ItemID = itemID;
            ItemAmount = itemAmount;
        }

        /// <summary>Gets or sets the stable ammunition-template identifier.</summary>
        public int ItemID
        {
            get => m_itemID;
            set => m_itemID = Mathf.Max(-1, value);
        }

        /// <summary>Gets or sets the remaining ammunition count.</summary>
        public int ItemAmount
        {
            get => m_itemAmount;
            set => m_itemAmount = Mathf.Max(0, value);
        }
    }
}
