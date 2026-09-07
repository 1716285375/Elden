using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>Defines immutable starting stock for one merchant archetype.</summary>
    [GameAsset(MenuName = "Items/Shops/Character Shop")]
    public sealed class CharacterShop : ScriptableObject
    {
        [SerializeField] private List<Item> m_items = new();
        [SerializeField] private List<int> m_itemAmounts = new();
        [SerializeField] private List<bool> m_infiniteItems = new();

        /// <summary>Gets the number of aligned, valid stock definitions.</summary>
        public int StockCount => Mathf.Min(
            m_items?.Count ?? 0,
            Mathf.Min(
                m_itemAmounts?.Count ?? 0,
                m_infiniteItems?.Count ?? 0));

        /// <summary>Creates private stock copies without mutating catalog assets.</summary>
        public List<Item> CreateRuntimeInventory(WorldItemDatabase database)
        {
            List<Item> runtimeInventory = new();
            for (int stockIndex = 0; stockIndex < StockCount; stockIndex++)
            {
                Item template = m_items[stockIndex];
                if (template == null || m_itemAmounts[stockIndex] <= 0)
                {
                    continue;
                }

                Item runtimeItem = database != null
                    ? database.GetRuntimeItemByID(template.ItemID)
                    : Instantiate(template);
                if (runtimeItem == null)
                {
                    continue;
                }

                runtimeItem.name = $"{template.name} (Shop Stock)";
                runtimeItem.hideFlags = HideFlags.DontSave;
                runtimeItem.SetCurrentItemAmount(m_itemAmounts[stockIndex]);
                runtimeItem.SetShopStockAmount(m_itemAmounts[stockIndex]);
                runtimeItem.SetInfinite(m_infiniteItems[stockIndex]);
                runtimeInventory.Add(runtimeItem);
            }

            return runtimeInventory;
        }

        private void OnValidate()
        {
            m_items ??= new List<Item>();
            m_itemAmounts ??= new List<int>();
            m_infiniteItems ??= new List<bool>();
            while (m_itemAmounts.Count < m_items.Count)
            {
                m_itemAmounts.Add(1);
            }

            while (m_infiniteItems.Count < m_items.Count)
            {
                m_infiniteItems.Add(false);
            }

            if (m_itemAmounts.Count > m_items.Count)
            {
                m_itemAmounts.RemoveRange(
                    m_items.Count,
                    m_itemAmounts.Count - m_items.Count);
            }

            if (m_infiniteItems.Count > m_items.Count)
            {
                m_infiniteItems.RemoveRange(
                    m_items.Count,
                    m_infiniteItems.Count - m_items.Count);
            }

            for (int amountIndex = 0;
                amountIndex < m_itemAmounts.Count;
                amountIndex++)
            {
                m_itemAmounts[amountIndex] =
                    Mathf.Max(1, m_itemAmounts[amountIndex]);
            }
        }
    }
}
