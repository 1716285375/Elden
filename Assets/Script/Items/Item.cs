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

        /// <summary>Gets the player-facing item name.</summary>
        public string ItemName => m_itemName;

        /// <summary>Gets the item icon used by inventory interfaces.</summary>
        public Sprite ItemIcon => m_itemIcon;

        /// <summary>Gets the player-facing item description.</summary>
        public string ItemDescription => m_itemDescription;

        /// <summary>Gets the stable database, network, and save identifier.</summary>
        public int ItemID => m_itemID;

        internal void AssignItemID(int itemID)
        {
            m_itemID = itemID;
        }
    }
}
