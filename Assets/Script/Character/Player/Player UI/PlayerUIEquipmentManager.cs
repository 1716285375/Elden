using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>
    /// Presents equipped slots and transfers compatible items through the inventory boundary.
    /// </summary>
    public class PlayerUIEquipmentManager : MonoBehaviour
    {
        private const int k_EquipmentSlotCount = 10;

        [Header("WINDOWS")]
        [SerializeField] private GameObject m_equipmentMenu;
        [SerializeField] private GameObject m_equipmentInventoryWindow;

        [Header("EQUIPMENT SLOTS")]
        [SerializeField] private Image[] m_equipmentSlotIcons =
            new Image[k_EquipmentSlotCount];
        [SerializeField] private Button[] m_equipmentSlotButtons =
            new Button[k_EquipmentSlotCount];

        [Header("EQUIPMENT INVENTORY")]
        [SerializeField] private RectTransform m_equipmentInventoryContent;
        [SerializeField] private UIEquipmentInventorySlot m_inventorySlotPrefab;

        private EquipmentSlotType m_currentSelectedEquipmentSlot;

        public bool IsEquipmentMenuOpen => m_equipmentMenu?.activeSelf == true;
        public bool IsEquipmentInventoryOpen =>
            m_equipmentInventoryWindow?.activeSelf == true;
        public EquipmentSlotType CurrentSelectedEquipmentSlot =>
            m_currentSelectedEquipmentSlot;

        /// <summary>Opens the Equipment Menu at its equipped-slot overview.</summary>
        public void OpenEquipmentManagerMenu()
        {
            PlayerUIManager playerUIManager = PlayerUIManager.Instance;
            if (playerUIManager?.CanOpenMenuWindows != true)
            {
                return;
            }

            playerUIManager.CloseAllMenuWindows();
            m_equipmentMenu?.SetActive(true);
            m_equipmentInventoryWindow?.SetActive(false);
            playerUIManager.NotifyMenuWindowOpened();
            RefreshMenu();
            SelectLastSelectedEquipmentSlot();
        }

        /// <summary>Closes the Equipment Menu and discards its generated candidate list.</summary>
        public void CloseEquipmentManagerMenu()
        {
            CloseEquipmentInventoryWindow(false);
            m_equipmentMenu?.SetActive(false);
            PlayerUIManager.Instance?.RefreshMenuWindowState();
        }

        /// <summary>Refreshes data-driven equipment presentation without reopening a window.</summary>
        public void RefreshMenu()
        {
            RefreshEquipmentSlotIcons();
        }

        /// <summary>Stores a UI slot identifier and loads compatible inventory candidates.</summary>
        public void SelectEquipmentSlot(int equipmentSlot)
        {
            if (equipmentSlot < 0 || equipmentSlot >= k_EquipmentSlotCount)
            {
                Debug.LogWarning(
                    $"Equipment slot index {equipmentSlot} is outside the supported range.",
                    this);
                return;
            }

            m_currentSelectedEquipmentSlot = (EquipmentSlotType)equipmentSlot;
            LoadEquipmentInventory();
        }

        /// <summary>Refreshes all six weapon and four armor slot icons.</summary>
        public void RefreshEquipmentSlotIcons()
        {
            PlayerInventoryManager inventory = ResolveLocalInventory();
            for (int slotIndex = 0; slotIndex < k_EquipmentSlotCount; slotIndex++)
            {
                Image slotIcon = GetArrayValue(m_equipmentSlotIcons, slotIndex);
                if (slotIcon == null)
                {
                    continue;
                }

                Item equippedItem = inventory?.GetEquipmentSlotItem(
                    (EquipmentSlotType)slotIndex);
                bool shouldShowIcon = equippedItem?.ItemIcon != null &&
                    (!(equippedItem is WeaponItem weapon) || !weapon.IsUnarmed);
                slotIcon.sprite = shouldShowIcon ? equippedItem.ItemIcon : null;
                slotIcon.enabled = shouldShowIcon;
            }
        }

        /// <summary>Generates only the inventory items compatible with the selected slot.</summary>
        public void LoadEquipmentInventory()
        {
            ClearEquipmentInventory();
            PlayerInventoryManager inventory = ResolveLocalInventory();
            if (inventory == null ||
                m_equipmentInventoryContent == null ||
                m_inventorySlotPrefab == null)
            {
                return;
            }

            m_equipmentInventoryWindow?.SetActive(true);
            bool hasSelectedFirstInventorySlot = false;
            foreach (Item item in inventory.ItemsInInventory)
            {
                if (!IsItemCompatibleWithSlot(item, m_currentSelectedEquipmentSlot))
                {
                    continue;
                }

                UIEquipmentInventorySlot inventorySlot = Instantiate(
                    m_inventorySlotPrefab,
                    m_equipmentInventoryContent);
                inventorySlot.SetEquipmentManager(this);
                if (!inventorySlot.AddItem(item))
                {
                    Destroy(inventorySlot.gameObject);
                    continue;
                }

                if (hasSelectedFirstInventorySlot)
                {
                    continue;
                }

                Button button = inventorySlot.GetComponent<Button>();
                button?.Select();
                button?.OnSelect(null);
                hasSelectedFirstInventorySlot = true;
            }
        }

        /// <summary>Destroys every dynamically generated equipment candidate.</summary>
        public void ClearEquipmentInventory()
        {
            if (m_equipmentInventoryContent == null)
            {
                return;
            }

            for (int childIndex = m_equipmentInventoryContent.childCount - 1;
                childIndex >= 0;
                childIndex--)
            {
                Destroy(m_equipmentInventoryContent.GetChild(childIndex).gameObject);
            }
        }

        /// <summary>Equips one selected candidate and restores focus to its equipment slot.</summary>
        public void EquipItem(Item item)
        {
            PlayerInventoryManager inventory = ResolveLocalInventory();
            if (inventory == null ||
                !IsItemCompatibleWithSlot(item, m_currentSelectedEquipmentSlot) ||
                !inventory.EquipItemInSlot(m_currentSelectedEquipmentSlot, item))
            {
                return;
            }

            RefreshMenu();
            CloseEquipmentInventoryWindow();
        }

        /// <summary>Returns the current item to inventory and resets the selected slot.</summary>
        public void UnequipSelectedItem()
        {
            PlayerInventoryManager inventory = ResolveLocalInventory();
            if (inventory == null ||
                !inventory.UnequipItemInSlot(m_currentSelectedEquipmentSlot))
            {
                return;
            }

            RefreshMenu();
            CloseEquipmentInventoryWindow();
        }

        /// <summary>Closes the candidate window and restores controller focus.</summary>
        public void CloseEquipmentInventoryWindow()
        {
            CloseEquipmentInventoryWindow(true);
        }

        /// <summary>Restores selection to the equipment slot that opened the candidate list.</summary>
        public void SelectLastSelectedEquipmentSlot()
        {
            int slotIndex = (int)m_currentSelectedEquipmentSlot;
            Button button = GetArrayValue(m_equipmentSlotButtons, slotIndex);
            if (button == null || !button.IsInteractable())
            {
                return;
            }

            button.Select();
            button.OnSelect(null);
        }

        /// <summary>Returns whether an item type can occupy the supplied equipment slot.</summary>
        public static bool IsItemCompatibleWithSlot(
            Item item,
            EquipmentSlotType equipmentSlot)
        {
            return equipmentSlot switch
            {
                EquipmentSlotType.RightWeapon01 or
                    EquipmentSlotType.RightWeapon02 or
                    EquipmentSlotType.RightWeapon03 or
                    EquipmentSlotType.LeftWeapon01 or
                    EquipmentSlotType.LeftWeapon02 or
                    EquipmentSlotType.LeftWeapon03 =>
                        item is WeaponItem weapon && !weapon.IsUnarmed,
                EquipmentSlotType.Head => item is HeadEquipmentItem,
                EquipmentSlotType.Body => item is BodyEquipmentItem,
                EquipmentSlotType.Leg => item is LegEquipmentItem,
                EquipmentSlotType.Hand => item is HandEquipmentItem,
                _ => false
            };
        }

        private void CloseEquipmentInventoryWindow(bool restoreSelection)
        {
            ClearEquipmentInventory();
            m_equipmentInventoryWindow?.SetActive(false);
            if (restoreSelection && IsEquipmentMenuOpen)
            {
                SelectLastSelectedEquipmentSlot();
            }
        }

        private static PlayerInventoryManager ResolveLocalInventory()
        {
            NetworkObject playerObject =
                NetworkManager.Singleton?.LocalClient?.PlayerObject;
            return playerObject?.GetComponent<PlayerInventoryManager>();
        }

        private static T GetArrayValue<T>(T[] values, int index)
            where T : Object
        {
            return values != null && index >= 0 && index < values.Length
                ? values[index]
                : null;
        }
    }
}
