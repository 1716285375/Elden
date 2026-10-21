using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ
{
    /// <summary>Owns the dual-column Site of Grace Inventory and Storage menu.</summary>
    public sealed class PlayerUIStorageManager : PlayerUIMenu
    {
        private StorageMenuView m_view;
        private PlayerControls m_playerControls;
        private PlayerManager m_player;
        private Item m_currentItem;
        private ShopItemCategory m_currentCategory;
        private bool m_isSelectingFromPlayerInventory;

        public Item CurrentItem => m_currentItem;
        public ShopItemCategory CurrentCategory => m_currentCategory;
        public bool IsSelectingFromPlayerInventory =>
            m_isSelectingFromPlayerInventory;

        private void Awake()
        {
            EnsureRuntimeUI();
            m_playerControls = new PlayerControls();
            m_playerControls.UI.PreviousShopCategory.performed +=
                OnPreviousCategoryPerformed;
            m_playerControls.UI.NextShopCategory.performed +=
                OnNextCategoryPerformed;
        }

        protected override void OnDisable()
        {
            m_playerControls?.UI.Disable();
            base.OnDisable();
        }

        private void OnDestroy()
        {
            if (m_playerControls == null)
            {
                return;
            }

            m_playerControls.UI.PreviousShopCategory.performed -=
                OnPreviousCategoryPerformed;
            m_playerControls.UI.NextShopCategory.performed -=
                OnNextCategoryPerformed;
            m_playerControls.Dispose();
        }

        /// <summary>Opens Storage for the locally owned player and rebuilds both columns.</summary>
        public void OpenStorageMenu()
        {
            EnsureRuntimeUI();
            PlayerManager player = PlayerUIManager.Instance?.LocalPlayer;
            if (player?.InventoryManager == null)
            {
                PlayerUIManager.Instance?.PlayUnableToContinueSound();
                return;
            }

            base.OpenMenu();
            if (!IsMenuOpen)
            {
                return;
            }

            m_player = player;
            m_currentCategory = ShopItemCategory.All;
            m_playerControls.UI.Enable();
            RefreshStorage();
            m_view.FocusSelection();
        }

        public override void CloseMenu()
        {
            m_playerControls?.UI.Disable();
            m_currentItem = null;
            m_player = null;
            base.CloseMenu();
        }

        /// <summary>Returns from Storage to the Site of Grace command list.</summary>
        public void ReturnToSiteOfGrace()
        {
            CloseMenu();
            PlayerUIManager.Instance?.PlayerUISiteOfGraceManager
                ?.OpenSiteOfGraceMenu();
        }

        /// <summary>Applies one category to Inventory and Storage at the same time.</summary>
        public void SetCategory(ShopItemCategory category)
        {
            if (m_currentCategory == category)
            {
                return;
            }

            m_currentCategory = category;
            RefreshStorage();
        }

        /// <summary>Updates the highlighted item name on the side that owns selection.</summary>
        public void SelectItem(
            Item item,
            bool isSelectingFromPlayerInventory)
        {
            m_currentItem = item;
            m_isSelectingFromPlayerInventory =
                isSelectingFromPlayerInventory;
            m_view?.ShowDetails(item, isSelectingFromPlayerInventory);
        }

        /// <summary>Moves one complete entry between the two player containers.</summary>
        public bool SwapItemLocation(
            Item item,
            bool isSelectingFromPlayerInventory)
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            if (!IsMenuOpen || item == null || inventory == null || m_player.IsDead)
            {
                return false;
            }

            string itemName = item.ItemName;
            bool didMove = isSelectingFromPlayerInventory
                ? inventory.MoveItemToStorage(item)
                : inventory.MoveItemToInventory(item);
            if (!didMove)
            {
                PlayerUIManager.Instance?.PlayUnableToContinueSound();
                m_view?.SetFeedback("转移未完成：请检查目标容量或物品数量。");
                return false;
            }

            PlayerUIManager.Instance?.PlayMenuConfirmSound();
            WorldSaveGameManager saveManager = WorldSaveGameManager.Instance;
            if (saveManager?.CanSaveGame == true)
            {
                saveManager.SaveGame();
            }

            RefreshStorage();
            m_view?.SetFeedback($"{(isSelectingFromPlayerInventory ? "已存入" : "已取出")} · {itemName}");
            return true;
        }

        /// <summary>Rebuilds both filtered columns and restores controller selection.</summary>
        public void RefreshStorage()
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            if (inventory == null)
            {
                return;
            }

            m_view?.RefreshItems(inventory.ItemsInInventory, inventory.ItemsInStorage);
        }

        /// <summary>Returns one wrapped category step for keyboard and controller input.</summary>
        public static ShopItemCategory GetCycledCategory(
            ShopItemCategory category,
            int direction)
        {
            return PlayerUIShopManager.GetCycledCategory(category, direction);
        }

        private void OnPreviousCategoryPerformed(InputAction.CallbackContext context)
        {
            if (!IsMenuOpen)
            {
                return;
            }

            SetCategory(GetCycledCategory(m_currentCategory, -1));
        }

        private void OnNextCategoryPerformed(InputAction.CallbackContext context)
        {
            if (!IsMenuOpen)
            {
                return;
            }

            SetCategory(GetCycledCategory(m_currentCategory, 1));
        }

        private void EnsureRuntimeUI()
        {
            if (m_view != null)
            {
                return;
            }
            GameObject menuRoot = new GameObject("Storage UI");
            menuRoot.SetActive(false);
            menuRoot.transform.SetParent(transform, false);
            SetMenuWindow(menuRoot);
            m_view = StorageMenuView.Create(menuRoot.transform, this);
        }
    }
}
