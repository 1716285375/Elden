using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ
{
    /// <summary>Owns the shared NPC context, Buy, Sell, filter, and transaction UI.</summary>
    public sealed class PlayerUIShopManager : PlayerUIMenu
    {
        public enum ShopBuyOrSell
        {
            Buying,
            Selling
        }

        private ShopMenuView m_view;
        private bool m_isTrading;
        private int m_lastBackFrame = -1;
        private Coroutine m_openMenuRoutine;
        private PlayerControls m_playerControls;
        private AICharacterInventoryManager m_shopkeeperInventory;
        private AICharacterSoundFXManager m_dialogueSource;
        private PlayerManager m_player;
        private Item m_currentItem;
        private ShopItemCategory m_currentCategory;
        private ShopBuyOrSell m_currentMode;

        public ShopItemCategory CurrentCategory => m_currentCategory;
        public ShopBuyOrSell CurrentMode => m_currentMode;
        public Item CurrentItem => m_currentItem;
        public AICharacterInventoryManager CurrentShopkeeper =>
            m_shopkeeperInventory;

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

        /// <summary>Defers the context menu until dialogue presentation has closed.</summary>
        public void OpenInteractionMenuAfterFixedFrame(
            AICharacterSoundFXManager dialogueSource)
        {
            if (m_openMenuRoutine != null)
            {
                StopCoroutine(m_openMenuRoutine);
            }

            m_openMenuRoutine = StartCoroutine(
                OpenInteractionMenuAfterFixedFrameRoutine(dialogueSource));
        }

        /// <summary>Builds Talk and capability-specific actions for one NPC.</summary>
        public void OpenInteractionMenu(
            AICharacterSoundFXManager dialogueSource)
        {
            if (dialogueSource == null)
            {
                return;
            }

            EnsureRuntimeUI();
            base.OpenMenu();
            if (!IsMenuOpen)
            {
                return;
            }

            m_dialogueSource = dialogueSource;
            m_shopkeeperInventory = dialogueSource
                .GetComponentInParent<AICharacterInventoryManager>();
            m_player = PlayerUIManager.Instance?.LocalPlayer;
            m_isTrading = false;
            m_view.ShowContext(
                m_shopkeeperInventory?.IsShop == true,
                dialogueSource.CharacterDialogueID == CharacterDialogueID.Blacksmith);
            m_playerControls.UI.Enable();
        }

        /// <summary>Closes this menu only when it belongs to the leaving NPC.</summary>
        public void CloseForDialogueSource(
            AICharacterSoundFXManager dialogueSource)
        {
            if (m_dialogueSource == dialogueSource)
            {
                CloseMenu();
            }
        }

        /// <summary>Returns to the NPC context menu without ending interaction range.</summary>
        public void ReturnToContextMenu()
        {
            if (!IsMenuOpen || m_dialogueSource == null)
            {
                return;
            }

            m_isTrading = false;
            m_currentItem = null;
            m_view.ShowContext(
                m_shopkeeperInventory?.IsShop == true,
                m_dialogueSource.CharacterDialogueID == CharacterDialogueID.Blacksmith);
        }

        public void OpenBuyMenu()
        {
            OpenShopMenu(ShopBuyOrSell.Buying);
        }

        public void OpenSellMenu()
        {
            OpenShopMenu(ShopBuyOrSell.Selling);
        }

        public void Talk()
        {
            AICharacterSoundFXManager dialogueSource = m_dialogueSource;
            PlayerManager player = m_player;
            CloseMenu();
            dialogueSource?.PlayCurrentDialogueEvent(player);
        }

        public void OpenSmithService()
        {
            CloseMenu();
            PlayerUIManager.Instance?.PlayerUIWeaponUpgradeManager
                ?.OpenWeaponUpgradeMenu();
        }

        /// <summary>Routes one slot click through the current Buy or Sell policy.</summary>
        public bool BuyOrSellItem(Item item)
        {
            if (!IsMenuOpen || !m_isTrading || m_shopkeeperInventory?.CanTradeWith(m_player) != true)
            {
                CloseMenu();
                return false;
            }
            string itemName = item != null ? item.ItemName : string.Empty;
            SelectItem(item);
            bool didComplete = m_currentMode == ShopBuyOrSell.Buying
                ? m_shopkeeperInventory?.TryPurchaseItem(item, m_player) == true
                : m_shopkeeperInventory?.TrySellItem(item, m_player) == true;
            if (!didComplete)
            {
                PlayerUIManager.Instance?.PlayUnableToContinueSound();
                RefreshDetails();
                m_view?.SetFeedback("交易未完成：请检查余额、库存或背包容量。");
                return false;
            }

            PlayerUIManager.Instance?.PlayMenuConfirmSound();
            PopulateShopInventory();
            m_view?.SetFeedback($"交易完成 · {itemName}");
            return true;
        }

        /// <summary>Updates details for keyboard, controller, and pointer selection.</summary>
        public void SelectItem(Item item)
        {
            m_currentItem = item;
            RefreshDetails();
        }

        public int GetDisplayedValue(Item item)
        {
            return m_currentMode == ShopBuyOrSell.Buying
                ? item?.ItemValue ?? 0
                : AICharacterInventoryManager.CalculateSellValue(item);
        }

        public int GetDisplayedAmount(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            return m_currentMode == ShopBuyOrSell.Buying
                ? item.ShopStockAmount
                : item.IsStackable
                    ? item.CurrentItemAmount
                    : 1;
        }

        public void SetAllCategory()
        {
            SetCategory(ShopItemCategory.All);
        }

        public void SetArmorCategory()
        {
            SetCategory(ShopItemCategory.Armor);
        }

        public void SetWeaponsCategory()
        {
            SetCategory(ShopItemCategory.Weapons);
        }

        /// <summary>Applies the same category path for mouse and shoulder actions.</summary>
        public void SetCategory(ShopItemCategory category)
        {
            if (m_currentCategory == category)
            {
                return;
            }

            m_currentCategory = category;
            PopulateShopInventory();
        }

        /// <summary>Cycles the three filters with wrapping for LB/RB input.</summary>
        public void CycleCategory(int direction)
        {
            if (!IsMenuOpen || !m_isTrading || direction == 0)
            {
                return;
            }

            SetCategory(GetCycledCategory(m_currentCategory, direction));
        }

        /// <inheritdoc />
        public override void CloseMenu()
        {
            if (m_openMenuRoutine != null)
            {
                StopCoroutine(m_openMenuRoutine);
                m_openMenuRoutine = null;
            }

            base.CloseMenu();
            m_playerControls?.UI.Disable();
            m_currentItem = null;
            m_shopkeeperInventory = null;
            m_dialogueSource = null;
            m_player = null;
        }

        private IEnumerator OpenInteractionMenuAfterFixedFrameRoutine(
            AICharacterSoundFXManager dialogueSource)
        {
            yield return new WaitForFixedUpdate();
            m_openMenuRoutine = null;
            OpenInteractionMenu(dialogueSource);
        }

        private void OpenShopMenu(ShopBuyOrSell mode)
        {
            if (!IsMenuOpen ||
                m_shopkeeperInventory?.EnsureShopInventory() != true)
            {
                PlayerUIManager.Instance?.PlayUnableToContinueSound();
                return;
            }

            m_currentMode = mode;
            m_currentCategory = ShopItemCategory.All;
            m_isTrading = true;
            m_view.ShowTrading();
            PopulateShopInventory();
        }

        private void OnPreviousCategoryPerformed(
            InputAction.CallbackContext context)
        {
            CycleCategory(-1);
        }

        private void OnNextCategoryPerformed(
            InputAction.CallbackContext context)
        {
            CycleCategory(1);
        }

        /// <summary>Returns one menu level for either Escape or controller cancel, once per frame.</summary>
        public void NavigateBack()
        {
            if (!IsMenuOpen || m_lastBackFrame == Time.frameCount)
            {
                return;
            }

            m_lastBackFrame = Time.frameCount;
            if (m_view?.IsConfirmationOpen == true)
            {
                m_view.CancelTrade();
                return;
            }
            if (m_isTrading)
            {
                ReturnToContextMenu();
            }
            else
            {
                CloseMenu();
            }
        }

        private void PopulateShopInventory()
        {
            IReadOnlyList<Item> source = m_currentMode == ShopBuyOrSell.Buying
                ? m_shopkeeperInventory?.ItemsInInventory
                : m_player?.InventoryManager?.ItemsInInventory;
            m_view?.RefreshItems(source);
        }

        private void RefreshDetails()
        {
            m_view?.ShowDetails(m_currentItem, m_player?.PlayerStatsManager?.Runes ?? 0);
        }

        private void EnsureRuntimeUI()
        {
            if (m_view != null)
            {
                return;
            }
            GameObject menuRoot = new GameObject("Shop UI");
            menuRoot.SetActive(false);
            menuRoot.transform.SetParent(transform, false);
            SetMenuWindow(menuRoot);
            m_view = ShopMenuView.Create(menuRoot.transform, this);
        }

        private void LateUpdate()
        {
            if (IsMenuOpen && (m_player == null || m_player.IsDead || m_dialogueSource == null ||
                (m_player.transform.position - m_dialogueSource.transform.position).sqrMagnitude > 36f))
            {
                CloseMenu();
            }
        }

        /// <summary>Returns one wrapped category step for shared input tests.</summary>
        public static ShopItemCategory GetCycledCategory(
            ShopItemCategory category,
            int direction)
        {
            const int categoryCount = 3;
            int nextCategory = ((int)category +
                (direction >= 0 ? 1 : -1) + categoryCount) % categoryCount;
            return (ShopItemCategory)nextCategory;
        }

        /// <summary>Returns whether one item belongs to the requested shop filter.</summary>
        public static bool MatchesCategory(
            Item item,
            ShopItemCategory category)
        {
            return item != null && category switch
            {
                ShopItemCategory.Armor => item is ArmorItem,
                ShopItemCategory.Weapons => item is WeaponItem,
                _ => true
            };
        }

    }
}
