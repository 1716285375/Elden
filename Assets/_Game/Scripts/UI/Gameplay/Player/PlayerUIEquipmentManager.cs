using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>
    /// Presents equipped slots and transfers compatible items through the inventory boundary.
    /// </summary>
    public class PlayerUIEquipmentManager : PlayerUIMenu
    {
        private const int k_EquipmentSlotCount = 15;

        [Header("WINDOWS")]
        [SerializeField] private GameObject m_equipmentInventoryWindow;

        [Header("EQUIPMENT SLOTS")]
        [SerializeField] private Image[] m_equipmentSlotIcons =
            new Image[k_EquipmentSlotCount];
        [SerializeField] private Button[] m_equipmentSlotButtons =
            new Button[k_EquipmentSlotCount];
        [SerializeField] private TMP_Text[] m_equipmentSlotQuantityTexts =
            new TMP_Text[k_EquipmentSlotCount];

        [Header("EQUIPMENT INVENTORY")]
        [SerializeField] private RectTransform m_equipmentInventoryContent;
        [SerializeField] private UIEquipmentInventorySlot m_inventorySlotPrefab;

        private EquipmentSlotType m_currentSelectedEquipmentSlot;
        private EquipmentMenuView m_view;
        private EquipmentMenuView m_weaponView;
        private EquipmentMenuView m_armorView;
        private InventoryCategory m_category = InventoryCategory.Armor;
        private EquipmentSlotType m_lastWeaponSlot = EquipmentSlotType.RightWeapon01;
        private EquipmentSlotType m_lastArmorSlot = EquipmentSlotType.Head;

        public bool IsEquipmentMenuOpen => IsMenuOpen;
        public bool IsEquipmentInventoryOpen =>
            m_view != null ? m_view.IsBrowsingItems : m_equipmentInventoryWindow?.activeSelf == true;
        public bool IsEditingSearchText => m_view != null && m_view.IsEditingText;
        public EquipmentSlotType CurrentSelectedEquipmentSlot =>
            m_currentSelectedEquipmentSlot;
        public InventoryCategory Category => m_category;

        /// <summary>Opens the Equipment Menu at its equipped-slot overview.</summary>
        public void OpenEquipmentManagerMenu()
        {
            OpenCategory(InventoryCategory.Armor);
        }

        public void OpenWeaponMenu()
        {
            OpenCategory(InventoryCategory.Weapons);
        }

        private void OpenCategory(InventoryCategory category)
        {
            RememberSelectedSlot();
            m_weaponView?.gameObject.SetActive(false);
            m_armorView?.gameObject.SetActive(false);
            m_view = null;
            m_category = category;
            m_currentSelectedEquipmentSlot = category == InventoryCategory.Weapons ? m_lastWeaponSlot : m_lastArmorSlot;
            OpenMenu();
            if (!IsMenuOpen)
            {
                return;
            }

            m_equipmentInventoryWindow?.SetActive(false);
            if (Application.isPlaying)
            {
                EnsureToolkitMenu();
                m_view.ShowOverview();
            }
            RefreshMenu();
            SelectLastSelectedEquipmentSlot();
        }

        /// <summary>Closes the Equipment Menu and discards its generated candidate list.</summary>
        public void CloseEquipmentManagerMenu()
        {
            CloseMenu();
        }

        /// <inheritdoc />
        public override void CloseMenu()
        {
            RememberSelectedSlot();
            CloseEquipmentInventoryWindow(false);
            base.CloseMenu();
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

        /// <summary>Refreshes all weapon, armor, and ammunition slot icons.</summary>
        public void RefreshEquipmentSlotIcons()
        {
            if (m_view != null)
            {
                m_view.Refresh();
                return;
            }
            PlayerInventoryManager inventory = ResolveLocalInventory();
            PlayerManager player = inventory?.GetComponent<PlayerManager>();
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
                TMP_Text quantityText = GetArrayValue(
                    m_equipmentSlotQuantityTexts,
                    slotIndex);
                if (quantityText == null)
                {
                    continue;
                }

                int quantity = 0;
                bool shouldShowQuantity = shouldShowIcon &&
                    TryGetEquipmentQuantity(
                        equippedItem,
                        player,
                        out quantity);
                quantityText.text = shouldShowQuantity
                    ? quantity.ToString()
                    : string.Empty;
                quantityText.gameObject.SetActive(shouldShowQuantity);
            }
        }

        /// <summary>Opens the inventory filtered for the primary ammunition slot.</summary>
        public void LoadMainProjectileEquipment()
        {
            SelectEquipmentSlot((int)EquipmentSlotType.MainProjectile);
        }

        /// <summary>Opens the inventory filtered for the secondary ammunition slot.</summary>
        public void LoadSecondaryProjectileEquipment()
        {
            SelectEquipmentSlot((int)EquipmentSlotType.SecondaryProjectile);
        }

        /// <summary>Opens the inventory filtered for gameplay item slot one.</summary>
        public void LoadQuickSlot01Equipment()
        {
            SelectEquipmentSlot((int)EquipmentSlotType.QuickSlot01);
        }

        /// <summary>Opens the inventory filtered for gameplay item slot two.</summary>
        public void LoadQuickSlot02Equipment()
        {
            SelectEquipmentSlot((int)EquipmentSlotType.QuickSlot02);
        }

        /// <summary>Opens the inventory filtered for gameplay item slot three.</summary>
        public void LoadQuickSlot03Equipment()
        {
            SelectEquipmentSlot((int)EquipmentSlotType.QuickSlot03);
        }

        /// <summary>Generates only the inventory items compatible with the selected slot.</summary>
        public void LoadEquipmentInventory()
        {
            ClearEquipmentInventory();
            if (m_view != null)
            {
                m_view.ShowCandidates();
                return;
            }
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
            if (m_view != null)
            {
                m_view.ClearCandidates();
                return;
            }
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
            if (item != null && inventory?.GetEquipmentSlotItem(m_currentSelectedEquipmentSlot) == item)
            {
                CloseEquipmentInventoryWindow();
                return;
            }
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
            if (m_view != null)
            {
                m_view.FocusCurrentSlot();
                return;
            }
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
                EquipmentSlotType.MainProjectile or
                    EquipmentSlotType.SecondaryProjectile =>
                        item is RangedProjectileItem,
                EquipmentSlotType.QuickSlot01 or
                    EquipmentSlotType.QuickSlot02 or
                    EquipmentSlotType.QuickSlot03 =>
                        item is QuickSlotItem,
                _ => false
            };
        }

        /// <summary>Tracks native focus without opening candidates or changing equipment.</summary>
        public void HighlightEquipmentSlot(int equipmentSlot)
        {
            if (equipmentSlot >= 0 && equipmentSlot < k_EquipmentSlotCount)
            {
                m_currentSelectedEquipmentSlot = (EquipmentSlotType)equipmentSlot;
            }
        }

        private void CloseEquipmentInventoryWindow(bool restoreSelection)
        {
            ClearEquipmentInventory();
            m_view?.ShowOverview();
            m_equipmentInventoryWindow?.SetActive(false);
            if (restoreSelection && IsEquipmentMenuOpen)
            {
                SelectLastSelectedEquipmentSlot();
            }
        }

        private static PlayerInventoryManager ResolveLocalInventory()
        {
            return PlayerUIManager.Instance?.LocalPlayer?.InventoryManager;
        }

        private void EnsureToolkitMenu()
        {
            CanvasGroup legacy = MenuWindow.GetComponent<CanvasGroup>();
            if (legacy == null)
            {
                legacy = MenuWindow.AddComponent<CanvasGroup>();
            }
            legacy.alpha = 0f;
            legacy.interactable = false;
            legacy.blocksRaycasts = false;
            if (m_category == InventoryCategory.Weapons)
            {
                m_weaponView ??= EquipmentMenuView.Create(MenuWindow.transform, this, m_category);
                m_view = m_weaponView;
            }
            else
            {
                m_armorView ??= EquipmentMenuView.Create(MenuWindow.transform, this, m_category);
                m_view = m_armorView;
            }
            m_view.gameObject.SetActive(true);
        }

        private void RememberSelectedSlot()
        {
            if ((int)m_currentSelectedEquipmentSlot < 6)
            {
                m_lastWeaponSlot = m_currentSelectedEquipmentSlot;
            }
            else if ((int)m_currentSelectedEquipmentSlot < 10)
            {
                m_lastArmorSlot = m_currentSelectedEquipmentSlot;
            }
        }

        private static bool TryGetEquipmentQuantity(
            Item item,
            PlayerManager player,
            out int quantity)
        {
            if (item is RangedProjectileItem projectile)
            {
                quantity = projectile.CurrentAmmoAmount;
                return true;
            }

            if (item is QuickSlotItem quickSlotItem && quickSlotItem.IsConsumable)
            {
                quantity = quickSlotItem.GetCurrentAmount(player);
                return true;
            }

            quantity = 0;
            return false;
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
