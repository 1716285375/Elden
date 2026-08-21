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
            if (itemID < 0 || itemID >= m_items.Count)
            {
                return null;
            }

            WeaponItem weapon = m_items[itemID] as WeaponItem;
            return weapon != null && weapon.ItemID == itemID ? weapon : null;
        }

        private void AssignItemIDs()
        {
            for (int itemIndex = 0; itemIndex < m_items.Count; itemIndex++)
            {
                m_items[itemIndex]?.AssignItemID(itemIndex);
            }
        }
    }
}
