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
        [SerializeField] private List<AshOfWar> m_ashesOfWar = new();
        [SerializeField] private List<SpellItem> m_spells = new();
        [SerializeField] private List<RangedProjectileItem> m_projectiles = new();
        [SerializeField] private List<QuickSlotItem> m_quickSlotItems = new();

        [Header("World Pickups")]
        [SerializeField] private GameObject m_creatureDropPickupPrefab;

        /// <summary>Gets the persistent item catalog instance.</summary>
        public static WorldItemDatabase Instance => s_instance;

        /// <summary>
        /// Gets the authored item catalog in stable network identifier order.
        /// </summary>
        public IReadOnlyList<Item> Items => m_items;

        /// <summary>Gets the authored Ashes of War registered in the item catalog.</summary>
        public IReadOnlyList<AshOfWar> AshesOfWar => m_ashesOfWar;

        /// <summary>Gets the authored spells registered in the global item catalog.</summary>
        public IReadOnlyList<SpellItem> Spells => m_spells;

        /// <summary>Gets every ammunition template registered in the item catalog.</summary>
        public IReadOnlyList<RangedProjectileItem> Projectiles => m_projectiles;

        /// <summary>Gets every gameplay quick-slot item registered in the catalog.</summary>
        public IReadOnlyList<QuickSlotItem> QuickSlotItems => m_quickSlotItems;

        /// <summary>Gets the server-spawned pickup presentation used by creature loot.</summary>
        public GameObject CreatureDropPickupPrefab => m_creatureDropPickupPrefab;

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

        /// <summary>Returns any registered item assigned to a stable persistent identifier.</summary>
        public Item GetItemByID(int itemID)
        {
            return GetItemByID<Item>(itemID, null);
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

        /// <summary>Returns the Ash of War assigned to a stable item identifier.</summary>
        public AshOfWar GetAshOfWarByID(int itemID)
        {
            return GetItemByID(itemID, m_ashesOfWar);
        }

        /// <summary>Returns the spell assigned to a stable item identifier.</summary>
        public SpellItem GetSpellByID(int itemID)
        {
            return GetItemByID(itemID, m_spells);
        }

        /// <summary>Returns the ammunition template assigned to a stable item identifier.</summary>
        public RangedProjectileItem GetProjectileByID(int itemID)
        {
            return GetItemByID(itemID, m_projectiles);
        }

        /// <summary>Returns the gameplay quick-slot item assigned to a stable identifier.</summary>
        public QuickSlotItem GetQuickSlotItemByID(int itemID)
        {
            return GetItemByID(itemID, m_quickSlotItems);
        }

        private void AssignItemIDs()
        {
            AppendMissingItems(m_spells);
            AppendMissingItems(m_projectiles);
            AppendMissingItems(m_quickSlotItems);
            for (int itemIndex = 0; itemIndex < m_items.Count; itemIndex++)
            {
                m_items[itemIndex]?.AssignItemID(itemIndex);
            }
        }

        private void AppendMissingItems<T>(IEnumerable<T> typedItems) where T : Item
        {
            if (typedItems == null)
            {
                return;
            }

            foreach (T item in typedItems)
            {
                if (item != null && !m_items.Contains(item))
                {
                    m_items.Add(item);
                }
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
