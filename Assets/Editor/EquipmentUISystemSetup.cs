using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP68-70 Character and Equipment menus.</summary>
    public static class EquipmentUISystemSetup
    {
        private const int k_EquipmentSlotCount = 10;
        private const string k_PlayerControlsPath = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_EquipmentSlotPrefabPath =
            "Assets/_Game/Prefabs/UI/Equipment Slot.prefab";
        private const string k_InventorySlotPrefabPath =
            "Assets/_Game/Prefabs/UI/Equipment Inventory Slot.prefab";
        private const string k_WorldScenePath =
            WorldScenePathLayout.MasterScenePath;
        private const string k_CharacterMenuName = "Character Menu";
        private const string k_EquipmentMenuName = "Equipment Menu";
        private const string k_EquipmentInventoryWindowName =
            "Equipment Inventory Window";
        private const string k_MenuEventSystemName = "Save Game EventSystem";

        private static readonly string[] s_equipmentSlotNames =
        {
            "Right Weapon 01",
            "Right Weapon 02",
            "Right Weapon 03",
            "Left Weapon 01",
            "Left Weapon 02",
            "Left Weapon 03",
            "Head",
            "Body",
            "Leg",
            "Hand"
        };

        private static readonly ArmorIconDefinition[] s_armorIcons =
        {
            new ArmorIconDefinition(
                "Assets/_Game/Data/Items/Armor/Starter Hood.asset",
                "Assets/Art/Textures/UI/Items/Dark_Hood_Icon_01.png"),
            new ArmorIconDefinition(
                "Assets/_Game/Data/Items/Armor/Starter Armor.asset",
                "Assets/Art/Textures/UI/Items/Dark_Leather_Armor_Icon_01.png"),
            new ArmorIconDefinition(
                "Assets/_Game/Data/Items/Armor/Starter Gauntlets.asset",
                "Assets/Art/Textures/UI/Items/Leather_Gloves_Icon_01.png"),
            new ArmorIconDefinition(
                "Assets/_Game/Data/Items/Armor/Starter Greaves.asset",
                "Assets/Art/Textures/UI/Items/Assassins_Boots_Icon_01.png")
        };

        private static readonly Color s_menuBackgroundColor =
            new Color(0.008f, 0.006f, 0.005f, 0.92f);
        private static readonly Color s_panelColor =
            new Color(0.035f, 0.028f, 0.022f, 0.96f);
        private static readonly Color s_buttonColor =
            new Color(0.09f, 0.075f, 0.055f, 0.96f);
        private static readonly Color s_borderColor =
            new Color(0.52f, 0.42f, 0.24f, 0.95f);
        private static readonly Color s_textColor =
            new Color(0.9f, 0.84f, 0.7f, 1f);
        private static readonly Color s_mutedTextColor =
            new Color(0.62f, 0.57f, 0.47f, 1f);
        private static readonly Color s_highlightColor =
            new Color(0.82f, 0.62f, 0.24f, 0.28f);

        [MenuItem("Tools/Elden/Configure Equipment UI System")]
        public static void ConfigureEquipmentUISystem()
        {
            ConfigureInputActions();
            ConfigureArmorIcons();
            TMP_FontAsset font = GetPlayerUIFont();
            ConfigureEquipmentSlotPrefab(font);
            ConfigureInventorySlotPrefab(font);
            ConfigurePlayerUIPrefab(font);
            ConfigureWorldEventSystem();
            AssetDatabase.SaveAssets();
            ValidateEquipmentUISystem();
            Debug.Log(
                "[EquipmentUISystemSetup] Configured Character Menu, ten equipment " +
                "slots, filtered inventory, equip/unequip, navigation, and modal input.");
        }

        [MenuItem("Tools/Elden/Validate Equipment UI System")]
        public static void ValidateEquipmentUISystem()
        {
            ValidateInputActions();
            ValidateRuntimeContracts();
            ValidateSlotPrefabs();
            ValidatePlayerUIPrefab();
            ValidateWorldEventSystem();
            Debug.Log(
                "[EquipmentUISystemValidation] Input isolation, HUD toggling, slot " +
                "filtering, dynamic inventory, navigation, and EventSystem are valid.");
        }

        private static void ConfigureInputActions()
        {
            InputActionAsset controls = LoadRequiredAsset<InputActionAsset>(
                k_PlayerControlsPath);
            InputActionMap uiMap = controls.FindActionMap("UI", true);
            InputAction legacySaveAction = uiMap.FindAction("Toggle Save Menu");
            if (legacySaveAction != null)
            {
                controls.RemoveAction("UI/Toggle Save Menu");
            }

            InputAction openMenu = GetOrCreateAction(
                uiMap,
                "Open Character Menu");
            InputAction closeMenu = GetOrCreateAction(uiMap, "Close Menu");
            InputAction unequipItem = GetOrCreateAction(uiMap, "Unequip Item");
            EnsureBinding(
                openMenu,
                "<Gamepad>/start",
                "Gamepad");
            EnsureBinding(
                openMenu,
                "<Keyboard>/escape",
                "Keyboard&Mouse");
            EnsureBinding(
                closeMenu,
                "<Gamepad>/buttonEast",
                "Gamepad");
            EnsureBinding(
                unequipItem,
                "<Gamepad>/buttonWest",
                "Gamepad");
            EnsureBinding(
                unequipItem,
                "<Keyboard>/x",
                "Keyboard&Mouse");
            EditorUtility.SetDirty(controls);
        }

        private static void ConfigureArmorIcons()
        {
            foreach (ArmorIconDefinition definition in s_armorIcons)
            {
                TextureImporter importer = AssetImporter.GetAtPath(definition.IconPath)
                    as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException(
                        $"Armor icon source is missing: {definition.IconPath}.");
                }

                if (importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Single ||
                    importer.mipmapEnabled)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }

                Item item = LoadRequiredAsset<Item>(definition.ItemPath);
                SerializedObject serializedItem = new SerializedObject(item);
                SetObjectReference(
                    serializedItem,
                    "m_itemIcon",
                    LoadRequiredAsset<Sprite>(definition.IconPath));
                serializedItem.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
            }
        }

        private static TMP_FontAsset GetPlayerUIFont()
        {
            GameObject playerUI = LoadRequiredAsset<GameObject>(k_PlayerUIPrefabPath);
            return playerUI.GetComponentsInChildren<TMP_Text>(true)
                .Select(text => text.font)
                .FirstOrDefault(font => font != null) ??
                throw new InvalidOperationException(
                    "Player UI Manager is missing a TMP font.");
        }

        private static void ConfigureEquipmentSlotPrefab(TMP_FontAsset font)
        {
            EditUIPrefab(
                k_EquipmentSlotPrefabPath,
                "Equipment Slot",
                root =>
                {
                    ConfigureRectSize(root.GetComponent<RectTransform>(), 330f, 82f);
                    Button button = ConfigureButton(root, s_buttonColor);
                    ConfigureText(
                        GetOrCreateRectTransform(root.transform, "Label"),
                        font,
                        "RIGHT WEAPON 01",
                        TextAlignmentOptions.MidlineLeft,
                        21f,
                        s_textColor);
                    ConfigureAnchoredRect(
                        root.transform.Find("Label") as RectTransform,
                        new Vector2(34f, 0f),
                        new Vector2(220f, 62f));
                    Image itemIcon = ConfigureImage(
                        GetOrCreateRectTransform(root.transform, "Item Icon"),
                        Color.white,
                        false);
                    itemIcon.preserveAspect = true;
                    itemIcon.enabled = false;
                    ConfigureAnchoredRect(
                        itemIcon.rectTransform,
                        new Vector2(-124f, 0f),
                        new Vector2(58f, 58f));
                    button.navigation = new Navigation
                    {
                        mode = Navigation.Mode.Automatic
                    };
                    SetLayerRecursively(root, 5);
                });
        }

        private static void ConfigureInventorySlotPrefab(TMP_FontAsset font)
        {
            EditUIPrefab(
                k_InventorySlotPrefabPath,
                "Equipment Inventory Slot",
                root =>
                {
                    ConfigureRectSize(root.GetComponent<RectTransform>(), 246f, 78f);
                    Button button = ConfigureButton(root, s_buttonColor);
                    Image highlight = ConfigureImage(
                        GetOrCreateRectTransform(root.transform, "Highlighted Icon"),
                        s_highlightColor,
                        false);
                    ConfigureFullStretch(highlight.rectTransform, 2f);
                    highlight.gameObject.SetActive(false);

                    Image itemIcon = ConfigureImage(
                        GetOrCreateRectTransform(root.transform, "Item Icon"),
                        Color.white,
                        false);
                    itemIcon.preserveAspect = true;
                    itemIcon.enabled = false;
                    ConfigureAnchoredRect(
                        itemIcon.rectTransform,
                        new Vector2(-90f, 0f),
                        new Vector2(58f, 58f));

                    TMP_Text itemName = ConfigureText(
                        GetOrCreateRectTransform(root.transform, "Item Name"),
                        font,
                        "ITEM",
                        TextAlignmentOptions.MidlineLeft,
                        18f,
                        s_textColor);
                    ConfigureAnchoredRect(
                        itemName.rectTransform,
                        new Vector2(30f, 0f),
                        new Vector2(168f, 58f));

                    UIEquipmentInventorySlot inventorySlot =
                        GetOrAddComponent<UIEquipmentInventorySlot>(root);
                    SetObjectReference(inventorySlot, "m_itemIcon", itemIcon);
                    SetObjectReference(inventorySlot, "m_highlightedIcon", highlight);
                    SetObjectReference(inventorySlot, "m_itemNameText", itemName);
                    ConfigureButtonEvent(button, inventorySlot.EquipItem);
                    ConfigureSelectionEvents(root, inventorySlot);
                    button.navigation = new Navigation
                    {
                        mode = Navigation.Mode.Automatic
                    };
                    SetLayerRecursively(root, 5);
                });
        }

        private static void ConfigurePlayerUIPrefab(TMP_FontAsset font)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerUIPrefabPath);
            try
            {
                Transform canvas = root.transform.Find("Player UI") ??
                    throw new InvalidOperationException(
                        "Player UI Manager is missing the Player UI Canvas.");
                PlayerUIManager playerUIManager =
                    GetRequiredComponent<PlayerUIManager>(root);
                PlayerUISaveGameManager saveGameManager =
                    GetRequiredComponent<PlayerUISaveGameManager>(root);
                PlayerUICharacterMenuManager characterMenuManager =
                    GetOrAddComponent<PlayerUICharacterMenuManager>(root);
                PlayerUIEquipmentManager equipmentManager =
                    GetOrAddComponent<PlayerUIEquipmentManager>(root);

                RectTransform characterMenu = ConfigureCharacterMenu(
                    canvas,
                    font,
                    characterMenuManager,
                    equipmentManager,
                    saveGameManager);
                EquipmentMenuReferences equipmentMenu = ConfigureEquipmentMenu(
                    canvas,
                    font,
                    characterMenuManager,
                    equipmentManager);
                ConfigureHUD(root);

                GameObject menuEventSystem = FindDescendant(
                        root.transform,
                        k_MenuEventSystemName)?.gameObject ??
                    throw new InvalidOperationException(
                        "Player UI Manager is missing its menu EventSystem fallback.");
                RectTransform saveMenu = FindDescendant(
                        root.transform,
                        "Save Game Menu") as RectTransform ??
                    throw new InvalidOperationException(
                        "Player UI Manager is missing Save Game Menu.");
                GetOrAddComponent<PlayerUIToggleHUD>(saveMenu.gameObject);

                SetObjectReference(
                    characterMenuManager,
                    "m_characterMenu",
                    characterMenu.gameObject);
                ConfigureEquipmentManager(
                    equipmentManager,
                    equipmentMenu);
                SetObjectReference(
                    playerUIManager,
                    "m_playerUICharacterMenuManager",
                    characterMenuManager);
                SetObjectReference(
                    playerUIManager,
                    "m_playerUIEquipmentManager",
                    equipmentManager);
                SetObjectReference(
                    playerUIManager,
                    "m_menuEventSystem",
                    menuEventSystem);

                menuEventSystem.SetActive(false);
                characterMenu.gameObject.SetActive(false);
                equipmentMenu.Menu.gameObject.SetActive(false);
                equipmentMenu.InventoryWindow.gameObject.SetActive(false);
                SavePrefab(root, k_PlayerUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static RectTransform ConfigureCharacterMenu(
            Transform canvas,
            TMP_FontAsset font,
            PlayerUICharacterMenuManager characterMenuManager,
            PlayerUIEquipmentManager equipmentManager,
            PlayerUISaveGameManager saveGameManager)
        {
            RectTransform menu = GetOrCreateRectTransform(canvas, k_CharacterMenuName);
            ConfigureFullStretch(menu);
            ConfigureImage(menu, s_menuBackgroundColor, false);
            GetOrAddComponent<PlayerUIToggleHUD>(menu.gameObject);

            RectTransform panel = GetOrCreateRectTransform(menu, "Menu Panel");
            ConfigureAnchoredRect(
                panel,
                new Vector2(-555f, 0f),
                new Vector2(650f, 760f));
            ConfigurePanel(panel);

            TMP_Text title = ConfigureText(
                GetOrCreateRectTransform(panel, "Title"),
                font,
                "CHARACTER",
                TextAlignmentOptions.Center,
                46f,
                s_textColor);
            ConfigureAnchoredRect(
                title.rectTransform,
                new Vector2(0f, 286f),
                new Vector2(560f, 90f));

            Button equipmentButton = ConfigureMenuButton(
                panel,
                font,
                "Equipment Button",
                "EQUIPMENT",
                new Vector2(0f, 130f));
            Button saveButton = ConfigureMenuButton(
                panel,
                font,
                "Save Game Button",
                "SAVE GAME",
                new Vector2(0f, 20f));
            Button returnButton = ConfigureMenuButton(
                panel,
                font,
                "Return Button",
                "RETURN",
                new Vector2(0f, -90f));
            ConfigureButtonEvent(
                equipmentButton,
                equipmentManager.OpenEquipmentManagerMenu);
            ConfigureButtonEvent(saveButton, saveGameManager.OpenSaveGameMenu);
            ConfigureButtonEvent(
                returnButton,
                characterMenuManager.CloseAllMenuWindowsAfterFixedUpdate);
            ConfigureVerticalNavigation(
                new[] { equipmentButton, saveButton, returnButton });
            GetOrAddComponent<PlayerUISelectButtonOnEnable>(
                equipmentButton.gameObject);
            RemoveComponent<PlayerUISelectButtonOnEnable>(saveButton.gameObject);
            RemoveComponent<PlayerUISelectButtonOnEnable>(returnButton.gameObject);

            TMP_Text hint = ConfigureText(
                GetOrCreateRectTransform(panel, "Input Hint"),
                font,
                "A / ENTER   SELECT        B / ESC   CLOSE",
                TextAlignmentOptions.Center,
                18f,
                s_mutedTextColor);
            ConfigureAnchoredRect(
                hint.rectTransform,
                new Vector2(0f, -290f),
                new Vector2(560f, 42f));
            SetLayerRecursively(menu.gameObject, canvas.gameObject.layer);
            return menu;
        }

        private static EquipmentMenuReferences ConfigureEquipmentMenu(
            Transform canvas,
            TMP_FontAsset font,
            PlayerUICharacterMenuManager characterMenuManager,
            PlayerUIEquipmentManager equipmentManager)
        {
            RectTransform menu = GetOrCreateRectTransform(canvas, k_EquipmentMenuName);
            ConfigureFullStretch(menu);
            ConfigureImage(menu, s_menuBackgroundColor, false);
            GetOrAddComponent<PlayerUIToggleHUD>(menu.gameObject);
            PlayerUIEquipmentManagerInputManager inputManager =
                GetOrAddComponent<PlayerUIEquipmentManagerInputManager>(
                    menu.gameObject);
            SetObjectReference(inputManager, "m_equipmentManager", equipmentManager);

            TMP_Text title = ConfigureText(
                GetOrCreateRectTransform(menu, "Title"),
                font,
                "EQUIPMENT",
                TextAlignmentOptions.MidlineLeft,
                42f,
                s_textColor);
            ConfigureAnchoredRect(
                title.rectTransform,
                new Vector2(-540f, 440f),
                new Vector2(700f, 70f));

            RectTransform slotsWindow = GetOrCreateRectTransform(
                menu,
                "Equipment Slots Window");
            ConfigureAnchoredRect(
                slotsWindow,
                new Vector2(-450f, 10f),
                new Vector2(780f, 760f));
            ConfigurePanel(slotsWindow);
            RectTransform slotsGrid = GetOrCreateRectTransform(
                slotsWindow,
                "Slots Grid");
            ConfigureAnchoredRect(
                slotsGrid,
                new Vector2(0f, 12f),
                new Vector2(700f, 570f));
            GridLayoutGroup slotsLayout = GetOrAddComponent<GridLayoutGroup>(
                slotsGrid.gameObject);
            slotsLayout.cellSize = new Vector2(330f, 82f);
            slotsLayout.spacing = new Vector2(24f, 22f);
            slotsLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            slotsLayout.constraintCount = 2;
            slotsLayout.childAlignment = TextAnchor.UpperCenter;

            GameObject equipmentSlotPrefab = LoadRequiredAsset<GameObject>(
                k_EquipmentSlotPrefabPath);
            Image[] slotIcons = new Image[k_EquipmentSlotCount];
            Button[] slotButtons = new Button[k_EquipmentSlotCount];
            for (int slotIndex = 0;
                slotIndex < k_EquipmentSlotCount;
                slotIndex++)
            {
                GameObject slot = GetOrCreatePrefabChild(
                    slotsGrid,
                    equipmentSlotPrefab,
                    s_equipmentSlotNames[slotIndex]);
                TMP_Text label = slot.transform.Find("Label")
                    ?.GetComponent<TMP_Text>();
                if (label != null)
                {
                    label.text = s_equipmentSlotNames[slotIndex].ToUpperInvariant();
                }

                slotIcons[slotIndex] = slot.transform.Find("Item Icon")
                    ?.GetComponent<Image>();
                slotButtons[slotIndex] = GetRequiredComponent<Button>(slot);
                ConfigureIntButtonEvent(
                    slotButtons[slotIndex],
                    equipmentManager.SelectEquipmentSlot,
                    slotIndex);
                if (slotIndex == 0)
                {
                    GetOrAddComponent<PlayerUISelectButtonOnEnable>(slot);
                }
                else
                {
                    RemoveComponent<PlayerUISelectButtonOnEnable>(slot);
                }
            }

            Button backButton = ConfigureMenuButton(
                slotsWindow,
                font,
                "Back Button",
                "BACK TO CHARACTER",
                new Vector2(0f, -320f));
            ConfigureButtonEvent(backButton, characterMenuManager.OpenCharacterMenu);

            RectTransform inventoryWindow = ConfigureInventoryWindow(
                menu,
                font,
                equipmentManager,
                out RectTransform content);
            SetLayerRecursively(menu.gameObject, canvas.gameObject.layer);
            return new EquipmentMenuReferences(
                menu,
                inventoryWindow,
                content,
                slotIcons,
                slotButtons);
        }

        private static RectTransform ConfigureInventoryWindow(
            RectTransform menu,
            TMP_FontAsset font,
            PlayerUIEquipmentManager equipmentManager,
            out RectTransform content)
        {
            RectTransform window = GetOrCreateRectTransform(
                menu,
                k_EquipmentInventoryWindowName);
            ConfigureAnchoredRect(
                window,
                new Vector2(440f, 10f),
                new Vector2(720f, 760f));
            ConfigurePanel(window);

            TMP_Text title = ConfigureText(
                GetOrCreateRectTransform(window, "Title"),
                font,
                "SELECT EQUIPMENT",
                TextAlignmentOptions.Center,
                30f,
                s_textColor);
            ConfigureAnchoredRect(
                title.rectTransform,
                new Vector2(0f, 322f),
                new Vector2(620f, 58f));

            RectTransform scrollView = GetOrCreateRectTransform(
                window,
                "Scroll View");
            ConfigureAnchoredRect(
                scrollView,
                new Vector2(-15f, 10f),
                new Vector2(620f, 570f));
            ConfigureImage(scrollView, new Color(0f, 0f, 0f, 0.32f), false);
            ScrollRect scrollRect = GetOrAddComponent<ScrollRect>(
                scrollView.gameObject);
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;

            RectTransform viewport = GetOrCreateRectTransform(
                scrollView,
                "Viewport");
            ConfigureFullStretch(viewport, 8f, 28f);
            ConfigureImage(viewport, Color.white, false);
            GetOrAddComponent<RectMask2D>(viewport.gameObject);
            content = GetOrCreateRectTransform(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 1200f);
            GridLayoutGroup grid = GetOrAddComponent<GridLayoutGroup>(
                content.gameObject);
            grid.cellSize = new Vector2(246f, 78f);
            grid.spacing = new Vector2(20f, 18f);
            grid.padding = new RectOffset(28, 28, 24, 24);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            scrollRect.viewport = viewport;
            scrollRect.content = content;

            Scrollbar scrollbar = ConfigureVerticalScrollbar(scrollView, font);
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.Permanent;
            scrollRect.verticalScrollbarSpacing = -18f;
            UIMatchScrollWheelToSelectedButton follower =
                GetOrAddComponent<UIMatchScrollWheelToSelectedButton>(
                    scrollView.gameObject);
            SetObjectReference(follower, "m_scrollRect", scrollRect);
            SetObjectReference(follower, "m_content", content);

            Button returnButton = ConfigureMenuButton(
                window,
                font,
                "Return To Slots Button",
                "RETURN TO SLOTS",
                new Vector2(0f, -322f));
            ConfigureButtonEvent(
                returnButton,
                equipmentManager.CloseEquipmentInventoryWindow);

            TMP_Text hint = ConfigureText(
                GetOrCreateRectTransform(window, "Unequip Hint"),
                font,
                "X / SQUARE   UNEQUIP",
                TextAlignmentOptions.MidlineRight,
                17f,
                s_mutedTextColor);
            ConfigureAnchoredRect(
                hint.rectTransform,
                new Vector2(190f, 322f),
                new Vector2(250f, 36f));
            return window;
        }

        private static Scrollbar ConfigureVerticalScrollbar(
            RectTransform scrollView,
            TMP_FontAsset font)
        {
            RectTransform scrollbarRect = GetOrCreateRectTransform(
                scrollView,
                "Vertical Scrollbar");
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = Vector2.one;
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.anchoredPosition = Vector2.zero;
            scrollbarRect.sizeDelta = new Vector2(18f, 0f);
            ConfigureImage(scrollbarRect, new Color(0.05f, 0.04f, 0.03f, 0.9f), false);
            Scrollbar scrollbar = GetOrAddComponent<Scrollbar>(
                scrollbarRect.gameObject);
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.interactable = false;

            RectTransform slidingArea = GetOrCreateRectTransform(
                scrollbarRect,
                "Sliding Area");
            ConfigureFullStretch(slidingArea, 2f);
            RectTransform handle = GetOrCreateRectTransform(slidingArea, "Handle");
            ConfigureFullStretch(handle);
            Image handleImage = ConfigureImage(handle, s_borderColor, false);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            return scrollbar;
        }

        private static void ConfigureEquipmentManager(
            PlayerUIEquipmentManager equipmentManager,
            EquipmentMenuReferences references)
        {
            SerializedObject serializedManager = new SerializedObject(equipmentManager);
            SetObjectReference(serializedManager, "m_equipmentMenu", references.Menu.gameObject);
            SetObjectReference(
                serializedManager,
                "m_equipmentInventoryWindow",
                references.InventoryWindow.gameObject);
            SetObjectReference(
                serializedManager,
                "m_equipmentInventoryContent",
                references.Content);
            SetObjectReference(
                serializedManager,
                "m_inventorySlotPrefab",
                LoadRequiredAsset<UIEquipmentInventorySlot>(
                    k_InventorySlotPrefabPath));
            SetObjectArray(
                serializedManager,
                "m_equipmentSlotIcons",
                references.SlotIcons.Cast<UnityEngine.Object>().ToArray());
            SetObjectArray(
                serializedManager,
                "m_equipmentSlotButtons",
                references.SlotButtons.Cast<UnityEngine.Object>().ToArray());
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(equipmentManager);
        }

        private static void ConfigureHUD(GameObject root)
        {
            Transform hud = FindDescendant(root.transform, "HUD") ??
                throw new InvalidOperationException("Player UI Manager is missing HUD.");
            PlayerUIHUDManager hudManager =
                GetRequiredComponent<PlayerUIHUDManager>(hud.gameObject);
            CanvasGroup canvasGroup = GetOrAddComponent<CanvasGroup>(hud.gameObject);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            SetObjectArray(
                new SerializedObject(hudManager),
                "m_hudCanvasGroups",
                new UnityEngine.Object[] { canvasGroup },
                true);
        }

        private static void ConfigureWorldEventSystem()
        {
            GameObject playerUIPrefab = LoadRequiredAsset<GameObject>(k_PlayerUIPrefabPath);
            InputSystemUIInputModule sourceModule = playerUIPrefab
                .GetComponentsInChildren<InputSystemUIInputModule>(true)
                .FirstOrDefault() ??
                throw new InvalidOperationException(
                    "Player UI Manager is missing its configured UI Input Module.");
            Scene scene = OpenSceneIfNeeded(k_WorldScenePath, out bool opened);
            try
            {
                EventSystem eventSystem = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
                    .FirstOrDefault();
                GameObject eventSystemRoot;
                if (eventSystem == null)
                {
                    eventSystemRoot = new GameObject("EventSystem");
                    SceneManager.MoveGameObjectToScene(eventSystemRoot, scene);
                    eventSystem = eventSystemRoot.AddComponent<EventSystem>();
                }
                else
                {
                    eventSystemRoot = eventSystem.gameObject;
                }

                StandaloneInputModule legacyModule =
                    eventSystemRoot.GetComponent<StandaloneInputModule>();
                if (legacyModule != null)
                {
                    UnityEngine.Object.DestroyImmediate(legacyModule);
                }

                InputSystemUIInputModule inputModule =
                    GetOrAddComponent<InputSystemUIInputModule>(eventSystemRoot);
                EditorUtility.CopySerialized(sourceModule, inputModule);
                eventSystem.sendNavigationEvents = true;
                eventSystemRoot.SetActive(true);
                EditorUtility.SetDirty(eventSystemRoot);
                EditorUtility.SetDirty(eventSystem);
                EditorUtility.SetDirty(inputModule);
                SaveScene(scene, "Equipment UI EventSystem");
            }
            finally
            {
                CloseSceneIfOpened(scene, opened);
            }
        }

        private static void ValidateInputActions()
        {
            InputActionAsset controls = LoadRequiredAsset<InputActionAsset>(
                k_PlayerControlsPath);
            InputActionMap uiMap = controls.FindActionMap("UI", true);
            InputAction openMenu = uiMap.FindAction("Open Character Menu", true);
            InputAction closeMenu = uiMap.FindAction("Close Menu", true);
            InputAction unequip = uiMap.FindAction("Unequip Item", true);
            if (uiMap.FindAction("Toggle Save Menu") != null ||
                !HasBinding(openMenu, "<Gamepad>/start") ||
                !HasBinding(openMenu, "<Keyboard>/escape") ||
                !HasBinding(closeMenu, "<Gamepad>/buttonEast") ||
                !HasBinding(unequip, "<Gamepad>/buttonWest") ||
                !HasBinding(unequip, "<Keyboard>/x"))
            {
                throw new InvalidOperationException(
                    "PlayerControls has an invalid EP68-70 menu input contract.");
            }
        }

        private static void ValidateRuntimeContracts()
        {
            BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;
            if (typeof(PlayerUIManager).GetMethod(
                    "CloseAllMenuWindows",
                    publicInstance) == null ||
                typeof(PlayerUIHUDManager).GetMethod(
                    "HideHUD",
                    publicInstance) == null ||
                typeof(PlayerUICharacterMenuManager).GetMethod(
                    "OpenCharacterMenu",
                    publicInstance) == null ||
                typeof(PlayerUIEquipmentManager).GetMethod(
                    "LoadEquipmentInventory",
                    publicInstance) == null ||
                typeof(PlayerUIEquipmentManager).GetMethod(
                    "UnequipSelectedItem",
                    publicInstance) == null ||
                typeof(PlayerInventoryManager).GetMethod(
                    "EquipItemInSlot",
                    publicInstance) == null ||
                typeof(PlayerInventoryManager).GetMethod(
                    "UnequipItemInSlot",
                    publicInstance) == null)
            {
                throw new InvalidOperationException(
                    "The Character or Equipment Menu runtime contract is incomplete.");
            }

            Item straightSword = LoadRequiredAsset<Item>(
                "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset");
            Item starterHood = LoadRequiredAsset<Item>(
                "Assets/_Game/Data/Items/Armor/Starter Hood.asset");
            if (!PlayerUIEquipmentManager.IsItemCompatibleWithSlot(
                    straightSword,
                    EquipmentSlotType.RightWeapon01) ||
                PlayerUIEquipmentManager.IsItemCompatibleWithSlot(
                    straightSword,
                    EquipmentSlotType.Head) ||
                !PlayerUIEquipmentManager.IsItemCompatibleWithSlot(
                    starterHood,
                    EquipmentSlotType.Head))
            {
                throw new InvalidOperationException(
                    "Equipment inventory filtering does not match slot item types.");
            }
        }

        private static void ValidateSlotPrefabs()
        {
            GameObject equipmentSlot = LoadRequiredAsset<GameObject>(
                k_EquipmentSlotPrefabPath);
            GameObject inventorySlot = LoadRequiredAsset<GameObject>(
                k_InventorySlotPrefabPath);
            EventTrigger eventTrigger = inventorySlot.GetComponent<EventTrigger>();
            bool hasSelect = eventTrigger?.triggers.Any(entry =>
                entry.eventID == EventTriggerType.Select) == true;
            bool hasDeselect = eventTrigger?.triggers.Any(entry =>
                entry.eventID == EventTriggerType.Deselect) == true;
            if (equipmentSlot.GetComponent<Button>() == null ||
                equipmentSlot.transform.Find("Item Icon")?.GetComponent<Image>() == null ||
                inventorySlot.GetComponent<Button>() == null ||
                inventorySlot.GetComponent<UIEquipmentInventorySlot>() == null ||
                inventorySlot.GetComponent<PlayerUISelectButtonOnEnable>() != null ||
                !hasSelect ||
                !hasDeselect)
            {
                throw new InvalidOperationException(
                    "Equipment UI slot prefabs are incomplete.");
            }
        }

        private static void ValidatePlayerUIPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerUIPrefabPath);
            try
            {
                PlayerUIManager playerUIManager =
                    GetRequiredComponent<PlayerUIManager>(root);
                PlayerUICharacterMenuManager characterManager =
                    GetRequiredComponent<PlayerUICharacterMenuManager>(root);
                PlayerUIEquipmentManager equipmentManager =
                    GetRequiredComponent<PlayerUIEquipmentManager>(root);
                Transform characterMenu = FindDescendant(
                    root.transform,
                    k_CharacterMenuName);
                Transform equipmentMenu = FindDescendant(
                    root.transform,
                    k_EquipmentMenuName);
                Transform slotsGrid = equipmentMenu?.Find(
                    "Equipment Slots Window/Slots Grid");
                Transform inventoryWindow = equipmentMenu?.Find(
                    k_EquipmentInventoryWindowName);
                ScrollRect scrollRect = inventoryWindow
                    ?.GetComponentInChildren<ScrollRect>(true);
                Scrollbar scrollbar = inventoryWindow
                    ?.GetComponentInChildren<Scrollbar>(true);
                PlayerUIHUDManager hudManager = root
                    .GetComponentsInChildren<PlayerUIHUDManager>(true)
                    .FirstOrDefault();
                SerializedObject serializedHUD = new SerializedObject(hudManager);
                SerializedProperty canvasGroups = GetRequiredProperty(
                    serializedHUD,
                    "m_hudCanvasGroups");
                int characterDefaults = characterMenu
                    ?.GetComponentsInChildren<PlayerUISelectButtonOnEnable>(true)
                    .Length ?? 0;
                int equipmentDefaults = equipmentMenu
                    ?.GetComponentsInChildren<PlayerUISelectButtonOnEnable>(true)
                    .Length ?? 0;
                if (characterMenu == null ||
                    characterMenu.gameObject.activeSelf ||
                    characterMenu.GetComponent<PlayerUIToggleHUD>() == null ||
                    characterDefaults != 1 ||
                    equipmentMenu == null ||
                    equipmentMenu.gameObject.activeSelf ||
                    equipmentMenu.GetComponent<PlayerUIToggleHUD>() == null ||
                    equipmentMenu.GetComponent<
                        PlayerUIEquipmentManagerInputManager>() == null ||
                    equipmentDefaults != 1 ||
                    slotsGrid?.childCount != k_EquipmentSlotCount ||
                    inventoryWindow == null ||
                    scrollRect == null ||
                    scrollRect.content == null ||
                    scrollRect.content.GetComponent<GridLayoutGroup>() == null ||
                    scrollbar == null ||
                    scrollbar.interactable ||
                    scrollRect.GetComponent<UIMatchScrollWheelToSelectedButton>() == null ||
                    canvasGroups.arraySize == 0 ||
                    canvasGroups.GetArrayElementAtIndex(0).objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        "Player UI Character or Equipment Menu hierarchy is invalid.");
                }

                ValidateObjectReference(
                    playerUIManager,
                    "m_playerUICharacterMenuManager",
                    characterManager);
                ValidateObjectReference(
                    playerUIManager,
                    "m_playerUIEquipmentManager",
                    equipmentManager);
                ValidateObjectReference(
                    playerUIManager,
                    "m_menuEventSystem",
                    FindDescendant(root.transform, k_MenuEventSystemName)?.gameObject);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateWorldEventSystem()
        {
            Scene scene = OpenSceneIfNeeded(k_WorldScenePath, out bool opened);
            try
            {
                EventSystem eventSystem = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
                    .FirstOrDefault();
                InputSystemUIInputModule inputModule =
                    eventSystem?.GetComponent<InputSystemUIInputModule>();
                if (eventSystem == null ||
                    !eventSystem.gameObject.activeSelf ||
                    inputModule == null ||
                    inputModule.actionsAsset == null)
                {
                    throw new InvalidOperationException(
                        "World Scene is missing its active Input System EventSystem.");
                }
            }
            finally
            {
                CloseSceneIfOpened(scene, opened);
            }
        }

        private static Button ConfigureMenuButton(
            Transform parent,
            TMP_FontAsset font,
            string objectName,
            string label,
            Vector2 anchoredPosition)
        {
            RectTransform rect = GetOrCreateRectTransform(parent, objectName);
            ConfigureAnchoredRect(rect, anchoredPosition, new Vector2(520f, 76f));
            Button button = ConfigureButton(rect.gameObject, s_buttonColor);
            TMP_Text text = ConfigureText(
                GetOrCreateRectTransform(rect, "Label"),
                font,
                label,
                TextAlignmentOptions.Center,
                25f,
                s_textColor);
            ConfigureFullStretch(text.rectTransform, 8f);
            return button;
        }

        private static Button ConfigureButton(GameObject gameObject, Color color)
        {
            Image image = GetOrAddComponent<Image>(gameObject);
            image.color = color;
            image.raycastTarget = true;
            Outline outline = GetOrAddComponent<Outline>(gameObject);
            outline.effectColor = s_borderColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
            Button button = GetOrAddComponent<Button>(gameObject);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.12f, 0.92f, 1f);
            colors.selectedColor = new Color(1.28f, 1.18f, 0.9f, 1f);
            colors.pressedColor = new Color(0.78f, 0.72f, 0.58f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        private static void ConfigurePanel(RectTransform panel)
        {
            ConfigureImage(panel, s_panelColor, false);
            Outline outline = GetOrAddComponent<Outline>(panel.gameObject);
            outline.effectColor = s_borderColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }

        private static Image ConfigureImage(
            RectTransform rect,
            Color color,
            bool raycastTarget)
        {
            Image image = GetOrAddComponent<Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static TMP_Text ConfigureText(
            RectTransform rect,
            TMP_FontAsset font,
            string content,
            TextAlignmentOptions alignment,
            float fontSize,
            Color color)
        {
            TextMeshProUGUI text = GetOrAddComponent<TextMeshProUGUI>(
                rect.gameObject);
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.SmallCaps;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void ConfigureSelectionEvents(
            GameObject gameObject,
            UIEquipmentInventorySlot inventorySlot)
        {
            EventTrigger eventTrigger = GetOrAddComponent<EventTrigger>(gameObject);
            eventTrigger.triggers = new List<EventTrigger.Entry>();
            EventTrigger.Entry selectEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.Select,
                callback = new EventTrigger.TriggerEvent()
            };
            UnityEventTools.AddPersistentListener(
                selectEntry.callback,
                inventorySlot.SelectSlot);
            EventTrigger.Entry deselectEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.Deselect,
                callback = new EventTrigger.TriggerEvent()
            };
            UnityEventTools.AddPersistentListener(
                deselectEntry.callback,
                inventorySlot.DeselectSlot);
            eventTrigger.triggers.Add(selectEntry);
            eventTrigger.triggers.Add(deselectEntry);
            EditorUtility.SetDirty(eventTrigger);
        }

        private static void ConfigureButtonEvent(Button button, UnityAction action)
        {
            RemoveButtonEvents(button);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            EditorUtility.SetDirty(button);
        }

        private static void ConfigureIntButtonEvent(
            Button button,
            UnityAction<int> action,
            int value)
        {
            RemoveButtonEvents(button);
            UnityEventTools.AddIntPersistentListener(button.onClick, action, value);
            EditorUtility.SetDirty(button);
        }

        private static void RemoveButtonEvents(Button button)
        {
            for (int eventIndex = button.onClick.GetPersistentEventCount() - 1;
                eventIndex >= 0;
                eventIndex--)
            {
                UnityEventTools.RemovePersistentListener(
                    button.onClick,
                    eventIndex);
            }
        }

        private static void ConfigureVerticalNavigation(Button[] buttons)
        {
            for (int buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
            {
                Button current = buttons[buttonIndex];
                Button previous = buttons[(buttonIndex - 1 + buttons.Length) %
                    buttons.Length];
                Button next = buttons[(buttonIndex + 1) % buttons.Length];
                current.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = previous,
                    selectOnDown = next,
                    selectOnLeft = current,
                    selectOnRight = current
                };
            }
        }

        private static InputAction GetOrCreateAction(
            InputActionMap map,
            string actionName)
        {
            return map.FindAction(actionName) ?? map.AddAction(
                actionName,
                InputActionType.Button,
                expectedControlLayout: "Button");
        }

        private static void EnsureBinding(
            InputAction action,
            string path,
            string groups)
        {
            for (int bindingIndex = 0;
                bindingIndex < action.bindings.Count;
                bindingIndex++)
            {
                InputBinding binding = action.bindings[bindingIndex];
                if (!string.Equals(
                        binding.path,
                        path,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                action.ChangeBinding(bindingIndex).WithGroups(groups);
                return;
            }

            action.AddBinding(path, groups: groups);
        }

        private static bool HasBinding(InputAction action, string path)
        {
            return action.bindings.Any(binding => string.Equals(
                binding.path,
                path,
                StringComparison.OrdinalIgnoreCase));
        }

        private static void EditUIPrefab(
            string prefabPath,
            string rootName,
            Action<GameObject> configure)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    configure(root);
                    SavePrefab(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                return;
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject(rootName, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, previewScene);
                configure(root);
                SavePrefab(root, prefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static GameObject GetOrCreatePrefabChild(
            Transform parent,
            GameObject prefab,
            string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(
                prefab,
                parent) as GameObject ??
                throw new InvalidOperationException(
                    $"Could not instantiate UI prefab {prefab.name}.");
            instance.name = objectName;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static RectTransform GetOrCreateRectTransform(
            Transform parent,
            string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                return existing as RectTransform ??
                    throw new InvalidOperationException(
                        $"{objectName} must use a RectTransform.");
            }

            GameObject child = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Transform FindDescendant(Transform parent, string objectName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == objectName)
                {
                    return child;
                }

                Transform descendant = FindDescendant(child, objectName);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private static void ConfigureRectSize(
            RectTransform rect,
            float width,
            float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void ConfigureAnchoredRect(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void ConfigureFullStretch(
            RectTransform rect,
            float inset = 0f,
            float rightInset = -1f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            float resolvedRightInset = rightInset >= 0f ? rightInset : inset;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-resolvedRightInset, -inset);
        }

        private static void SavePrefab(GameObject root, string prefabPath)
        {
            if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
            {
                throw new InvalidOperationException($"Could not save {prefabPath}.");
            }
        }

        private static Scene OpenSceneIfNeeded(string path, out bool opened)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            opened = !scene.IsValid() || !scene.isLoaded;
            return opened
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)
                : scene;
        }

        private static void SaveScene(Scene scene, string featureName)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    $"Could not save {featureName} in {scene.path}.");
            }
        }

        private static void CloseSceneIfOpened(Scene scene, bool opened)
        {
            if (opened && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}.");
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static T GetRequiredComponent<T>(GameObject gameObject)
            where T : Component
        {
            return gameObject.GetComponent<T>() ??
                throw new InvalidOperationException(
                    $"{gameObject.name} is missing {typeof(T).Name}.");
        }

        private static void RemoveComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component != null)
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} is missing " +
                    $"{propertyName}.");
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SetObjectReference(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetRequiredProperty(serializedObject, propertyName)
                .objectReferenceValue = value;
        }

        private static void SetObjectArray(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object[] values,
            bool applyChanges = false)
        {
            SerializedProperty property = GetRequiredProperty(
                serializedObject,
                propertyName);
            property.arraySize = values.Length;
            for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
            {
                property.GetArrayElementAtIndex(valueIndex).objectReferenceValue =
                    values[valueIndex];
            }

            if (applyChanges)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(serializedObject.targetObject);
            }
        }

        private static void ValidateObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object expectedValue)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            if (GetRequiredProperty(serializedObject, propertyName)
                    .objectReferenceValue != expectedValue)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{propertyName} is not configured.");
            }
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private readonly struct ArmorIconDefinition
        {
            public ArmorIconDefinition(string itemPath, string iconPath)
            {
                ItemPath = itemPath;
                IconPath = iconPath;
            }

            public string ItemPath { get; }
            public string IconPath { get; }
        }

        private readonly struct EquipmentMenuReferences
        {
            public EquipmentMenuReferences(
                RectTransform menu,
                RectTransform inventoryWindow,
                RectTransform content,
                Image[] slotIcons,
                Button[] slotButtons)
            {
                Menu = menu;
                InventoryWindow = inventoryWindow;
                Content = content;
                SlotIcons = slotIcons;
                SlotButtons = slotButtons;
            }

            public RectTransform Menu { get; }
            public RectTransform InventoryWindow { get; }
            public RectTransform Content { get; }
            public Image[] SlotIcons { get; }
            public Button[] SlotButtons { get; }
        }
    }
}
