using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

        private static readonly Color s_panelColor =
            new(0.035f, 0.035f, 0.035f, 0.97f);
        private static readonly Color s_buttonColor =
            new(0.12f, 0.1f, 0.075f, 0.98f);
        private static readonly Color s_goldColor =
            new(0.82f, 0.68f, 0.36f, 1f);

        private readonly List<UIShopInventorySlot> m_slotPool = new();

        private GameObject m_contextPanel;
        private GameObject m_shopPanel;
        private Button m_talkButton;
        private Button m_buyButton;
        private Button m_sellButton;
        private Button m_smithButton;
        private RectTransform m_slotContainer;
        private TMP_Text m_shopTitleText;
        private TMP_Text m_modeText;
        private TMP_Text m_categoryText;
        private TMP_Text m_currentItemNameText;
        private TMP_Text m_currentItemValueText;
        private TMP_Text m_runesText;
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
            m_playerControls.UI.CloseMenu.performed += OnCloseMenuPerformed;
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
            m_playerControls.UI.CloseMenu.performed -= OnCloseMenuPerformed;
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
            m_contextPanel.SetActive(true);
            m_shopPanel.SetActive(false);
            m_buyButton.gameObject.SetActive(
                m_shopkeeperInventory?.IsShop == true);
            m_sellButton.gameObject.SetActive(
                m_shopkeeperInventory?.IsShop == true);
            m_smithButton.gameObject.SetActive(
                dialogueSource.CharacterDialogueID ==
                    CharacterDialogueID.Blacksmith);
            m_shopTitleText.text = dialogueSource.CharacterDialogueID.ToString();
            m_playerControls.UI.Enable();
            m_talkButton.Select();
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

            m_shopPanel.SetActive(false);
            m_contextPanel.SetActive(true);
            m_talkButton.Select();
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
            SelectItem(item);
            bool didComplete = m_currentMode == ShopBuyOrSell.Buying
                ? m_shopkeeperInventory?.TryPurchaseItem(item, m_player) == true
                : m_shopkeeperInventory?.TrySellItem(item, m_player) == true;
            if (!didComplete)
            {
                PlayerUIManager.Instance?.PlayUnableToContinueSound();
                RefreshDetails();
                return false;
            }

            PlayerUIManager.Instance?.PlayMenuConfirmSound();
            PopulateShopInventory();
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
            m_currentCategory = category;
            PopulateShopInventory();
        }

        /// <summary>Cycles the three filters with wrapping for LB/RB input.</summary>
        public void CycleCategory(int direction)
        {
            if (!IsMenuOpen || !m_shopPanel.activeSelf || direction == 0)
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
            m_contextPanel.SetActive(false);
            m_shopPanel.SetActive(true);
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

        private void OnCloseMenuPerformed(InputAction.CallbackContext context)
        {
            if (!IsMenuOpen)
            {
                return;
            }

            if (m_shopPanel.activeSelf)
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
            if (m_slotContainer == null || m_player == null)
            {
                return;
            }

            IReadOnlyList<Item> source = m_currentMode == ShopBuyOrSell.Buying
                ? m_shopkeeperInventory?.ItemsInInventory
                : m_player.InventoryManager?.ItemsInInventory;
            int activeSlotCount = 0;
            if (source != null)
            {
                foreach (Item item in source)
                {
                    if (!MatchesCategory(item, m_currentCategory))
                    {
                        continue;
                    }

                    UIShopInventorySlot slot = GetOrCreateSlot(activeSlotCount);
                    slot.gameObject.SetActive(true);
                    slot.AddItem(item);
                    activeSlotCount++;
                }
            }

            for (int slotIndex = activeSlotCount;
                slotIndex < m_slotPool.Count;
                slotIndex++)
            {
                m_slotPool[slotIndex].gameObject.SetActive(false);
            }

            m_modeText.text = m_currentMode == ShopBuyOrSell.Buying
                ? "Purchase"
                : "Sell";
            m_categoryText.text = $"Category: {m_currentCategory}   [LB / RB]";
            if (activeSlotCount > 0)
            {
                UIShopInventorySlot firstSlot = m_slotPool[0];
                EventSystem.current?.SetSelectedGameObject(
                    firstSlot.gameObject);
                SelectItem(firstSlot.CurrentItem);
            }
            else
            {
                SelectItem(null);
            }
        }

        private UIShopInventorySlot GetOrCreateSlot(int slotIndex)
        {
            if (slotIndex < m_slotPool.Count)
            {
                return m_slotPool[slotIndex];
            }

            GameObject slotObject = CreateUIObject(
                $"Shop Slot {slotIndex + 1}",
                m_slotContainer);
            Image background = slotObject.AddComponent<Image>();
            background.color = s_buttonColor;
            Button button = slotObject.AddComponent<Button>();
            button.targetGraphic = background;
            LayoutElement layout = slotObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 58f;
            HorizontalLayoutGroup row =
                slotObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(8, 8, 5, 5);
            row.spacing = 10f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlHeight = true;
            row.childControlWidth = true;
            row.childForceExpandHeight = true;
            row.childForceExpandWidth = false;

            Image focus = CreateUIObject("Focus", slotObject.transform)
                .AddComponent<Image>();
            focus.color = new Color(s_goldColor.r, s_goldColor.g, s_goldColor.b, 0.2f);
            RectTransform focusRect = focus.rectTransform;
            focusRect.anchorMin = Vector2.zero;
            focusRect.anchorMax = Vector2.one;
            focusRect.offsetMin = Vector2.zero;
            focusRect.offsetMax = Vector2.zero;
            focus.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            focus.transform.SetAsFirstSibling();

            Image icon = CreateUIObject("Icon", slotObject.transform)
                .AddComponent<Image>();
            icon.preserveAspect = true;
            LayoutElement iconLayout = icon.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 48f;
            iconLayout.preferredHeight = 48f;
            TMP_Text nameText = CreateText(
                "Item Name",
                slotObject.transform,
                string.Empty,
                24f,
                TextAlignmentOptions.MidlineLeft);
            nameText.GetComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_Text amountText = CreateText(
                "Amount",
                slotObject.transform,
                string.Empty,
                22f,
                TextAlignmentOptions.Center);
            amountText.GetComponent<LayoutElement>().preferredWidth = 70f;
            TMP_Text valueText = CreateText(
                "Value",
                slotObject.transform,
                string.Empty,
                22f,
                TextAlignmentOptions.Center);
            valueText.color = s_goldColor;
            valueText.GetComponent<LayoutElement>().preferredWidth = 100f;

            UIShopInventorySlot slot =
                slotObject.AddComponent<UIShopInventorySlot>();
            slot.SetVisualReferences(icon, focus, nameText);
            slot.Initialize(this, valueText, amountText);
            m_slotPool.Add(slot);
            return slot;
        }

        private void RefreshDetails()
        {
            if (m_currentItemNameText == null)
            {
                return;
            }

            m_currentItemNameText.text = m_currentItem != null
                ? m_currentItem.ItemName
                : "No items available";
            int displayedValue = GetDisplayedValue(m_currentItem);
            m_currentItemValueText.text = m_currentItem != null
                ? $"Value: {displayedValue}"
                : "Value: --";
            int runes = m_player?.PlayerStatsManager?.Runes ?? 0;
            m_runesText.text = $"Runes: {runes}";
            m_currentItemValueText.color =
                m_currentMode == ShopBuyOrSell.Buying &&
                displayedValue > runes
                    ? new Color(0.9f, 0.25f, 0.2f, 1f)
                    : s_goldColor;
        }

        private void EnsureRuntimeUI()
        {
            if (MenuWindow != null)
            {
                return;
            }

            GameObject menuRoot = CreateUIObject("Shop UI", transform);
            Canvas canvas = menuRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = menuRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            menuRoot.AddComponent<GraphicRaycaster>();
            Image overlay = menuRoot.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.62f);
            StretchToParent(menuRoot.GetComponent<RectTransform>());
            SetMenuWindow(menuRoot);

            m_contextPanel = CreatePanel(
                "NPC Context Menu",
                menuRoot.transform,
                new Vector2(430f, 520f));
            m_shopTitleText = CreateText(
                "NPC Title",
                m_contextPanel.transform,
                "NPC",
                38f,
                TextAlignmentOptions.Center);
            m_talkButton = CreateButton(
                m_contextPanel.transform,
                "Talk",
                Talk);
            m_buyButton = CreateButton(
                m_contextPanel.transform,
                "Buy",
                OpenBuyMenu);
            m_sellButton = CreateButton(
                m_contextPanel.transform,
                "Sell",
                OpenSellMenu);
            m_smithButton = CreateButton(
                m_contextPanel.transform,
                "Smith",
                OpenSmithService);
            CreateButton(m_contextPanel.transform, "Leave", CloseMenu);

            m_shopPanel = CreatePanel(
                "Shop Window",
                menuRoot.transform,
                new Vector2(1050f, 880f));
            m_modeText = CreateText(
                "Mode",
                m_shopPanel.transform,
                "Purchase",
                38f,
                TextAlignmentOptions.Center);
            GameObject categoryRow = CreateUIObject(
                "Categories",
                m_shopPanel.transform);
            HorizontalLayoutGroup categoryLayout =
                categoryRow.AddComponent<HorizontalLayoutGroup>();
            categoryLayout.spacing = 12f;
            categoryLayout.childControlHeight = true;
            categoryLayout.childControlWidth = true;
            categoryLayout.childForceExpandWidth = true;
            categoryRow.AddComponent<LayoutElement>().preferredHeight = 52f;
            CreateCategoryButton(
                categoryRow.transform,
                "All",
                ShopItemCategory.All);
            CreateCategoryButton(
                categoryRow.transform,
                "Armor",
                ShopItemCategory.Armor);
            CreateCategoryButton(
                categoryRow.transform,
                "Weapons",
                ShopItemCategory.Weapons);
            m_categoryText = CreateText(
                "Current Category",
                m_shopPanel.transform,
                string.Empty,
                22f,
                TextAlignmentOptions.Center);

            GameObject slotContainer = CreateUIObject(
                "Inventory Slots",
                m_shopPanel.transform);
            m_slotContainer = slotContainer.GetComponent<RectTransform>();
            VerticalLayoutGroup slotLayout =
                slotContainer.AddComponent<VerticalLayoutGroup>();
            slotLayout.spacing = 5f;
            slotLayout.childControlHeight = true;
            slotLayout.childControlWidth = true;
            slotLayout.childForceExpandHeight = false;
            slotLayout.childForceExpandWidth = true;
            LayoutElement slotsLayout = slotContainer.AddComponent<LayoutElement>();
            slotsLayout.flexibleHeight = 1f;
            slotsLayout.minHeight = 360f;

            m_currentItemNameText = CreateText(
                "Selected Item",
                m_shopPanel.transform,
                string.Empty,
                28f,
                TextAlignmentOptions.Center);
            m_currentItemValueText = CreateText(
                "Selected Value",
                m_shopPanel.transform,
                string.Empty,
                25f,
                TextAlignmentOptions.Center);
            m_runesText = CreateText(
                "Runes",
                m_shopPanel.transform,
                string.Empty,
                25f,
                TextAlignmentOptions.Center);
            CreateButton(
                m_shopPanel.transform,
                "Back",
                ReturnToContextMenu);

            menuRoot.SetActive(false);
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

        private void CreateCategoryButton(
            Transform parent,
            string label,
            ShopItemCategory category)
        {
            Button button = CreateButton(parent, label, null);
            button.gameObject.AddComponent<UIItemCategory>()
                .Initialize(category, SetCategory);
        }

        private static GameObject CreatePanel(
            string panelName,
            Transform parent,
            Vector2 size)
        {
            GameObject panel = CreateUIObject(panelName, parent);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            Image image = panel.AddComponent<Image>();
            image.color = s_panelColor;
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return panel;
        }

        private static Button CreateButton(
            Transform parent,
            string label,
            UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = CreateUIObject($"{label} Button", parent);
            Image image = buttonObject.AddComponent<Image>();
            image.color = s_buttonColor;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = s_goldColor;
            colors.selectedColor = s_goldColor;
            button.colors = colors;
            button.onClick.AddListener(action);
            buttonObject.AddComponent<LayoutElement>().preferredHeight = 54f;
            TMP_Text text = CreateText(
                "Label",
                buttonObject.transform,
                label,
                26f,
                TextAlignmentOptions.Center);
            StretchToParent(text.rectTransform);
            return button;
        }

        private static TMP_Text CreateText(
            string objectName,
            Transform parent,
            string text,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateUIObject(objectName, parent);
            TextMeshProUGUI textComponent =
                textObject.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.alignment = alignment;
            textComponent.color = Color.white;
            textComponent.raycastTarget = false;
            textObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(40f, fontSize + 14f);
            return textComponent;
        }

        private static GameObject CreateUIObject(
            string objectName,
            Transform parent)
        {
            GameObject uiObject = new(objectName, typeof(RectTransform));
            uiObject.transform.SetParent(parent, false);
            return uiObject;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
