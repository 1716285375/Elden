using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Defines the authored identity and presentation shared by every catalog item.
    /// </summary>
    public abstract class Item : ScriptableObject
    {
        [Header("Item Information")]
        [SerializeField] private string m_itemName = "New Item";
        [SerializeField] private Sprite m_itemIcon;
        [SerializeField, TextArea(3, 6)] private string m_itemDescription;
        [SerializeField, HideInInspector] private int m_itemID = -1;

        [Header("Stack")]
        [SerializeField, Min(1)] private int m_maxItemAmount = 1;
        [SerializeField, Min(0)] private int m_currentItemAmount = 1;

        [Header("Shop")]
        [SerializeField, Min(0)] private int m_itemValue;
        [SerializeField] private bool m_isInfinite;

        [NonSerialized] private int m_runtimeShopStockAmount = -1;

        /// <summary>Gets the player-facing item name.</summary>
        public string ItemName => m_itemName;

        /// <summary>Gets the item icon used by inventory interfaces.</summary>
        public Sprite ItemIcon => m_itemIcon;

        /// <summary>Gets the player-facing item description.</summary>
        public string ItemDescription => m_itemDescription;

        /// <summary>Gets the stable database, network, and save identifier.</summary>
        public int ItemID => m_itemID;

        /// <summary>Gets the maximum number represented by this inventory entry.</summary>
        public int MaxItemAmount => Mathf.Max(1, m_maxItemAmount);

        /// <summary>Gets the current number represented by this inventory entry.</summary>
        public int CurrentItemAmount => Mathf.Clamp(
            m_currentItemAmount,
            0,
            MaxItemAmount);

        /// <summary>Gets whether equal catalog items can share one inventory entry.</summary>
        public bool IsStackable => MaxItemAmount > 1;

        /// <summary>Gets the full Rune value used when this item is purchased.</summary>
        public int ItemValue => Mathf.Max(0, m_itemValue);

        /// <summary>Gets whether this runtime shop entry has unlimited stock.</summary>
        public bool IsInfinite => m_isInfinite;

        /// <summary>Gets stock independent of the player's normal stack limit.</summary>
        public int ShopStockAmount => m_runtimeShopStockAmount >= 0
            ? m_runtimeShopStockAmount
            : CurrentItemAmount;

        internal void AssignItemID(int itemID)
        {
            m_itemID = itemID;
        }

        /// <summary>Clamps and applies a runtime stack amount.</summary>
        public void SetCurrentItemAmount(int itemAmount)
        {
            m_currentItemAmount = Mathf.Clamp(itemAmount, 0, MaxItemAmount);
        }

        /// <summary>Sets the runtime stock policy without changing the authored template.</summary>
        public void SetInfinite(bool isInfinite)
        {
            m_isInfinite = isInfinite;
        }

        /// <summary>Sets merchant stock without changing this item's stack capacity.</summary>
        public void SetShopStockAmount(int stockAmount)
        {
            m_runtimeShopStockAmount = Mathf.Max(0, stockAmount);
        }

        /// <summary>Adds as much as possible and returns the amount that did not fit.</summary>
        public int AddItemAmount(int itemAmount)
        {
            int sanitizedAmount = Mathf.Max(0, itemAmount);
            int availableSpace = MaxItemAmount - CurrentItemAmount;
            int addedAmount = Mathf.Min(sanitizedAmount, availableSpace);
            m_currentItemAmount = CurrentItemAmount + addedAmount;
            return sanitizedAmount - addedAmount;
        }

        /// <summary>Atomically removes an amount when this stack contains enough items.</summary>
        public bool TryRemoveItemAmount(int itemAmount)
        {
            int sanitizedAmount = Mathf.Max(0, itemAmount);
            if (sanitizedAmount == 0 || CurrentItemAmount < sanitizedAmount)
            {
                return false;
            }

            m_currentItemAmount = CurrentItemAmount - sanitizedAmount;
            return true;
        }

        private void OnValidate()
        {
            m_maxItemAmount = Mathf.Max(1, m_maxItemAmount);
            m_currentItemAmount = Mathf.Clamp(
                m_currentItemAmount,
                0,
                m_maxItemAmount);
            m_itemValue = Mathf.Max(0, m_itemValue);
        }
    }
}
