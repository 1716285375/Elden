using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Owns the dual-column Site of Grace Inventory and Storage menu.</summary>
    public sealed class PlayerUIStorageManager : PlayerUIMenu
    {
        private static readonly Color s_panelColor =
            new(0.035f, 0.035f, 0.035f, 0.97f);
        private static readonly Color s_buttonColor =
            new(0.12f, 0.1f, 0.075f, 0.98f);
        private static readonly Color s_goldColor =
            new(0.82f, 0.68f, 0.36f, 1f);

        private readonly List<UIStorageInventorySlot> m_inventorySlots = new();
        private readonly List<UIStorageInventorySlot> m_storageSlots = new();

        private RectTransform m_inventorySlotContainer;
        private RectTransform m_storageSlotContainer;
        private TMP_Text m_inventoryCurrentItemText;
        private TMP_Text m_storageCurrentItemText;
        private TMP_Text m_categoryText;
        private Button m_backButton;
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
            m_inventoryCurrentItemText.text = isSelectingFromPlayerInventory
                ? item?.ItemName ?? string.Empty
                : string.Empty;
            m_storageCurrentItemText.text = isSelectingFromPlayerInventory
                ? string.Empty
                : item?.ItemName ?? string.Empty;
        }

        /// <summary>Moves one complete entry between the two player containers.</summary>
        public bool SwapItemLocation(
            Item item,
            bool isSelectingFromPlayerInventory)
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            if (item == null || inventory == null)
            {
                return false;
            }

            bool didMove = isSelectingFromPlayerInventory
                ? inventory.MoveItemToStorage(item)
                : inventory.MoveItemToInventory(item);
            if (!didMove)
            {
                PlayerUIManager.Instance?.PlayUnableToContinueSound();
                return false;
            }

            PlayerUIManager.Instance?.PlayMenuConfirmSound();
            WorldSaveGameManager saveManager = WorldSaveGameManager.Instance;
            if (saveManager?.CanSaveGame == true)
            {
                saveManager.SaveGame();
            }

            RefreshStorage();
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

            DeselectAllButtons();
            m_currentItem = null;
            m_inventoryCurrentItemText.text = string.Empty;
            m_storageCurrentItemText.text = string.Empty;
            m_categoryText.text = m_currentCategory.ToString();
            PopulateItems(
                inventory.ItemsInInventory,
                m_inventorySlots,
                m_inventorySlotContainer,
                true);
            PopulateItems(
                inventory.ItemsInStorage,
                m_storageSlots,
                m_storageSlotContainer,
                false);
            SelectFirstButton();
        }

        /// <summary>Returns one wrapped category step for keyboard and controller input.</summary>
        public static ShopItemCategory GetCycledCategory(
            ShopItemCategory category,
            int direction)
        {
            return PlayerUIShopManager.GetCycledCategory(category, direction);
        }

        private void PopulateItems(
            IReadOnlyList<Item> items,
            List<UIStorageInventorySlot> slots,
            RectTransform slotContainer,
            bool isSelectingFromPlayerInventory)
        {
            int visibleIndex = 0;
            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                Item item = items[itemIndex];
                if (!PlayerUIShopManager.MatchesCategory(
                        item,
                        m_currentCategory))
                {
                    continue;
                }

                UIStorageInventorySlot slot = visibleIndex < slots.Count
                    ? slots[visibleIndex]
                    : CreateStorageSlot(slotContainer, slots);
                slot.gameObject.SetActive(true);
                slot.AddStorageItem(item, isSelectingFromPlayerInventory);
                visibleIndex++;
            }

            for (int slotIndex = visibleIndex; slotIndex < slots.Count; slotIndex++)
            {
                slots[slotIndex].gameObject.SetActive(false);
            }
        }

        private void DeselectAllButtons()
        {
            foreach (UIStorageInventorySlot slot in m_inventorySlots)
            {
                slot.DeselectSlot();
            }

            foreach (UIStorageInventorySlot slot in m_storageSlots)
            {
                slot.DeselectSlot();
            }
        }

        private void SelectFirstButton()
        {
            UIStorageInventorySlot firstSlot = FindFirstActiveSlot(
                m_inventorySlots) ?? FindFirstActiveSlot(m_storageSlots);
            if (firstSlot == null)
            {
                m_backButton?.Select();
                return;
            }

            firstSlot.GetComponent<Button>().Select();
        }

        private static UIStorageInventorySlot FindFirstActiveSlot(
            List<UIStorageInventorySlot> slots)
        {
            foreach (UIStorageInventorySlot slot in slots)
            {
                if (slot != null && slot.gameObject.activeInHierarchy)
                {
                    return slot;
                }
            }

            return null;
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

        private void OnCloseMenuPerformed(InputAction.CallbackContext context)
        {
            if (IsMenuOpen)
            {
                ReturnToSiteOfGrace();
            }
        }

        private void EnsureRuntimeUI()
        {
            if (MenuWindow != null)
            {
                return;
            }

            GameObject menuRoot = CreateUIObject("Storage UI", transform);
            Canvas canvas = menuRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = menuRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            menuRoot.AddComponent<GraphicRaycaster>();
            Image overlay = menuRoot.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.62f);
            StretchToParent(menuRoot.GetComponent<RectTransform>());
            SetMenuWindow(menuRoot);

            GameObject panel = CreatePanel(
                "Storage Window",
                menuRoot.transform,
                new Vector2(1320f, 880f));
            CreateText(
                "Title",
                panel.transform,
                "ITEM STORAGE",
                40f,
                TextAlignmentOptions.Center);
            CreateCategoryRow(panel.transform);
            m_categoryText = CreateText(
                "Current Category",
                panel.transform,
                string.Empty,
                22f,
                TextAlignmentOptions.Center);

            GameObject columns = CreateUIObject("Item Columns", panel.transform);
            HorizontalLayoutGroup columnsLayout =
                columns.AddComponent<HorizontalLayoutGroup>();
            columnsLayout.spacing = 24f;
            columnsLayout.childControlHeight = true;
            columnsLayout.childControlWidth = true;
            columnsLayout.childForceExpandHeight = true;
            columnsLayout.childForceExpandWidth = true;
            columns.AddComponent<LayoutElement>().flexibleHeight = 1f;

            m_inventorySlotContainer = CreateItemColumn(
                columns.transform,
                "INVENTORY",
                out m_inventoryCurrentItemText);
            m_storageSlotContainer = CreateItemColumn(
                columns.transform,
                "STORAGE",
                out m_storageCurrentItemText);
            m_backButton = CreateButton(
                panel.transform,
                "Back",
                ReturnToSiteOfGrace);
            menuRoot.SetActive(false);
        }

        private void CreateCategoryRow(Transform parent)
        {
            GameObject categoryRow = CreateUIObject("Categories", parent);
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

        private RectTransform CreateItemColumn(
            Transform parent,
            string title,
            out TMP_Text currentItemText)
        {
            GameObject column = CreateUIObject($"{title} Column", parent);
            Image columnImage = column.AddComponent<Image>();
            columnImage.color = new Color(0.07f, 0.065f, 0.055f, 0.96f);
            VerticalLayoutGroup layout = column.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            column.AddComponent<LayoutElement>().flexibleWidth = 1f;
            CreateText(
                "Column Title",
                column.transform,
                title,
                30f,
                TextAlignmentOptions.Center);
            currentItemText = CreateText(
                "Current Item",
                column.transform,
                string.Empty,
                22f,
                TextAlignmentOptions.Center);

            GameObject scrollView = CreateUIObject("Scroll View", column.transform);
            scrollView.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
            ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollView.AddComponent<LayoutElement>().flexibleHeight = 1f;
            GameObject viewport = CreateUIObject("Viewport", scrollView.transform);
            viewport.AddComponent<RectMask2D>();
            StretchToParent(viewport.GetComponent<RectTransform>());
            GameObject content = CreateUIObject("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            VerticalLayoutGroup contentLayout =
                content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 5f;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            return contentRect;
        }

        private UIStorageInventorySlot CreateStorageSlot(
            RectTransform parent,
            List<UIStorageInventorySlot> slots)
        {
            GameObject slotObject = CreateUIObject("Storage Item Slot", parent);
            Image background = slotObject.AddComponent<Image>();
            background.color = s_buttonColor;
            Button button = slotObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.highlightedColor = s_goldColor;
            colors.selectedColor = s_goldColor;
            button.colors = colors;
            HorizontalLayoutGroup layout =
                slotObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 6, 6);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            slotObject.AddComponent<LayoutElement>().preferredHeight = 56f;

            Image focus = CreateUIObject("Focus", slotObject.transform)
                .AddComponent<Image>();
            focus.color = new Color(s_goldColor.r, s_goldColor.g, s_goldColor.b, 0.28f);
            focus.raycastTarget = false;
            LayoutElement focusLayout = focus.gameObject.AddComponent<LayoutElement>();
            focusLayout.ignoreLayout = true;
            StretchToParent(focus.rectTransform);

            Image icon = CreateUIObject("Icon", slotObject.transform)
                .AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            LayoutElement iconLayout = icon.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 40f;
            iconLayout.preferredHeight = 40f;
            TMP_Text nameText = CreateText(
                "Name",
                slotObject.transform,
                string.Empty,
                22f,
                TextAlignmentOptions.MidlineLeft);
            nameText.GetComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_Text amountText = CreateText(
                "Amount",
                slotObject.transform,
                string.Empty,
                21f,
                TextAlignmentOptions.Center);
            amountText.GetComponent<LayoutElement>().preferredWidth = 64f;

            UIStorageInventorySlot slot =
                slotObject.AddComponent<UIStorageInventorySlot>();
            slot.SetVisualReferences(icon, focus, nameText);
            slot.Initialize(this, amountText);
            slots.Add(slot);
            return slot;
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
            panel.AddComponent<Image>().color = s_panelColor;
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
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            buttonObject.AddComponent<LayoutElement>().preferredHeight = 54f;
            TMP_Text text = CreateText(
                "Label",
                buttonObject.transform,
                label,
                26f,
                TextAlignmentOptions.Center);
            StretchToParent(text.rectTransform);
            PlayerUIManager.Instance?.ApplyGameplayButtonStyle(button, text);
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
                Mathf.Max(38f, fontSize + 12f);
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
