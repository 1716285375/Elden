using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>Stores one merchant's stock using stable shop and catalog identifiers.</summary>
    [Serializable]
    public sealed class SerializableShopInventory
    {
        [SerializeField] private Shops m_shop = Shops.NoShop;
        [SerializeField] private List<int> m_itemIDs = new();
        [SerializeField] private List<int> m_itemAmounts = new();
        [SerializeField] private List<bool> m_infiniteItems = new();

        public SerializableShopInventory()
        {
        }

        public SerializableShopInventory(
            Shops shop,
            IReadOnlyList<int> itemIDs,
            IReadOnlyList<int> itemAmounts,
            IReadOnlyList<bool> infiniteItems)
        {
            m_shop = shop;
            ReplaceItems(itemIDs, itemAmounts, infiniteItems);
        }

        public Shops Shop => m_shop;
        public int ItemCount => Mathf.Min(
            m_itemIDs?.Count ?? 0,
            Mathf.Min(
                m_itemAmounts?.Count ?? 0,
                m_infiniteItems?.Count ?? 0));

        /// <summary>Gets one saved catalog ID, or -1 for an invalid index.</summary>
        public int GetItemID(int itemIndex)
        {
            return itemIndex >= 0 && itemIndex < ItemCount
                ? m_itemIDs[itemIndex]
                : -1;
        }

        /// <summary>Gets one non-negative saved stock count.</summary>
        public int GetItemAmount(int itemIndex)
        {
            return itemIndex >= 0 && itemIndex < ItemCount
                ? Mathf.Max(0, m_itemAmounts[itemIndex])
                : 0;
        }

        /// <summary>Gets whether one saved stock entry is unlimited.</summary>
        public bool GetIsInfinite(int itemIndex)
        {
            return itemIndex >= 0 && itemIndex < ItemCount &&
                m_infiniteItems[itemIndex];
        }

        /// <summary>Replaces stock using only serializer-safe primitive data.</summary>
        public void ReplaceItems(
            IReadOnlyList<int> itemIDs,
            IReadOnlyList<int> itemAmounts,
            IReadOnlyList<bool> infiniteItems)
        {
            m_itemIDs = new List<int>();
            m_itemAmounts = new List<int>();
            m_infiniteItems = new List<bool>();
            int itemCount = Mathf.Min(
                itemIDs?.Count ?? 0,
                Mathf.Min(
                    itemAmounts?.Count ?? 0,
                    infiniteItems?.Count ?? 0));
            for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                if (itemIDs[itemIndex] < 0 || itemAmounts[itemIndex] <= 0)
                {
                    continue;
                }

                m_itemIDs.Add(itemIDs[itemIndex]);
                m_itemAmounts.Add(itemAmounts[itemIndex]);
                m_infiniteItems.Add(infiniteItems[itemIndex]);
            }
        }
    }
}
