using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Owns the stable item catalog used to reconstruct replicated equipment identifiers.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public class WorldItemDatabase : MonoBehaviour
    {
        private static WorldItemDatabase s_instance;

        [SerializeField] private List<Item> m_items = new();
        [SerializeField] private List<HeadEquipmentItem> m_headEquipment = new();
        [SerializeField] private List<BodyEquipmentItem> m_bodyEquipment = new();
        [SerializeField] private List<HandEquipmentItem> m_handEquipment = new();
        [SerializeField] private List<LegEquipmentItem> m_legEquipment = new();

        /// <summary>Gets the persistent item catalog instance.</summary>
        public static WorldItemDatabase Instance => s_instance;

        /// <summary>
        /// Gets the authored item catalog in stable network identifier order.
        /// </summary>
        public IReadOnlyList<Item> Items => m_items;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            AssignItemIDs();
            DontDestroyOnLoad(gameObject);
        }

        private void OnValidate()
        {
            AssignItemIDs();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>
        /// Returns the weapon template assigned to a stable item identifier.
        /// </summary>
        public WeaponItem GetWeaponByID(int itemID)
        {
            return GetItemByID<WeaponItem>(itemID, null);
        }

        /// <summary>Returns the head-equipment template assigned to a stable item identifier.</summary>
        public HeadEquipmentItem GetHeadEquipmentByID(int itemID)
        {
            return GetItemByID(itemID, m_headEquipment);
        }

        /// <summary>Returns the body-equipment template assigned to a stable item identifier.</summary>
        public BodyEquipmentItem GetBodyEquipmentByID(int itemID)
        {
            return GetItemByID(itemID, m_bodyEquipment);
        }

        /// <summary>Returns the hand-equipment template assigned to a stable item identifier.</summary>
        public HandEquipmentItem GetHandEquipmentByID(int itemID)
        {
            return GetItemByID(itemID, m_handEquipment);
        }

        /// <summary>Returns the leg-equipment template assigned to a stable item identifier.</summary>
        public LegEquipmentItem GetLegEquipmentByID(int itemID)
        {
            return GetItemByID(itemID, m_legEquipment);
        }

        private void AssignItemIDs()
        {
            for (int itemIndex = 0; itemIndex < m_items.Count; itemIndex++)
            {
                m_items[itemIndex]?.AssignItemID(itemIndex);
            }
        }

        private T GetItemByID<T>(int itemID, List<T> typedItems) where T : Item
        {
            if (itemID < 0 || itemID >= m_items.Count)
            {
                return null;
            }

            T item = m_items[itemID] as T;
            if (item == null || item.ItemID != itemID)
            {
                return null;
            }

            return typedItems == null || typedItems.Contains(item) ? item : null;
        }
    }
}
