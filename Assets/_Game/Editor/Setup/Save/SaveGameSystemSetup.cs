using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZZ.Editor
{
    public static class SaveGameSystemSetup
    {
        private const string k_MainMenuScenePath = WorldScenePathLayout.MainMenuScenePath;
        private const string k_PlayerControlsPath = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_PlayerUIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_WorldSaveManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/World Save Game Manager.prefab";

        private static readonly Color s_menuOverlayColor = new Color(0f, 0f, 0f, 0.82f);
        private static readonly Color s_panelColor = new Color(0.06f, 0.06f, 0.06f, 0.98f);
        private static readonly Color s_buttonColor = new Color(0.13f, 0.13f, 0.13f, 0.96f);
        private static readonly Color s_selectedButtonColor = new Color(0.55f, 0.42f, 0.16f, 1f);
        private static Button s_pressStartButtonStyle;
        private static TMP_Text s_pressStartTextStyle;

        [MenuItem("Tools/Elden/Configure Save Game System")]
        public static void ConfigureSaveGameSystem()
        {
            ConfigureWorldSaveManagerPrefab();
            Scene scene = GetMainMenuSceneForEditing();
            ConfigureMainMenuScene(scene);
            ConfigurePlayerUIManagerPrefab(
                FindGameObject(scene, "EventSystem")?.GetComponent<InputSystemUIInputModule>());
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();
            ValidateSaveGameSystem();
            Debug.Log(
                "[SaveGameSystemSetup] Configured persistence, title menus, and the in-world Save Game flow.");
        }

        [MenuItem("Tools/Elden/Validate Save Game System")]
        public static void ValidateSaveGameSystem()
        {
            ValidatePersistenceBoundary();
            ValidateWorldSaveManagerPrefab();
            ValidatePlayerPrefab();
            ValidatePlayerUIManagerPrefab();
            ValidateInputActions();
            ValidateMainMenuScene();
            Debug.Log(
                "[SaveGameSystemValidation] Persistence, title UI, world UI, input, and ownership are valid.");
        }

        private static void ConfigureWorldSaveManagerPrefab()
        {
            GameObject managerRoot = PrefabUtility.LoadPrefabContents(k_WorldSaveManagerPrefabPath);

            try
            {
                WorldSaveGameManager manager = GetOrAddComponent<WorldSaveGameManager>(managerRoot);
                SetString(
                    manager,
                    "m_worldSceneName",
                    WorldScenePathLayout.MasterSceneName);
                SetInteger(manager, "m_startingSceneIndex", 1);
                EditorUtility.SetDirty(manager);
                PrefabUtility.SaveAsPrefabAsset(managerRoot, k_WorldSaveManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(managerRoot);
            }
        }

        private static void ConfigurePlayerUIManagerPrefab(InputSystemUIInputModule inputModuleTemplate)
        {
            if (inputModuleTemplate == null)
            {
                throw new InvalidOperationException(
                    "The title Scene EventSystem is required to configure world UI navigation.");
            }

            GameObject playerUIRoot = PrefabUtility.LoadPrefabContents(k_PlayerUIManagerPrefabPath);
            try
            {
                PlayerUIManager playerUIManager = GetOrAddComponent<PlayerUIManager>(playerUIRoot);
                Transform canvas = playerUIRoot.transform.Find("Player UI");
                if (canvas == null)
                {
                    throw new InvalidOperationException(
                        "The Player UI Manager prefab is missing its Player UI Canvas.");
                }

                PlayerUISaveGameManager saveGameManager =
                    GetOrAddComponent<PlayerUISaveGameManager>(playerUIRoot);
                RectTransform saveMenu = GetOrCreateRectTransform(canvas, "Save Game Menu");
                StretchToParent(saveMenu);
                saveMenu.SetAsLastSibling();
                Image overlay = GetOrAddComponent<Image>(saveMenu.gameObject);
                overlay.color = s_menuOverlayColor;
                overlay.raycastTarget = true;

                RectTransform panel = GetOrCreateRectTransform(saveMenu, "Panel");
                ConfigureCenteredRect(panel, Vector2.zero, new Vector2(640f, 500f));
                Image panelImage = GetOrAddComponent<Image>(panel.gameObject);
                panelImage.color = s_panelColor;
                panelImage.raycastTarget = true;

                RectTransform title = GetOrCreateRectTransform(panel, "Title");
                ConfigureCenteredRect(title, new Vector2(0f, 180f), new Vector2(560f, 64f));
                ConfigureText(
                    title.gameObject,
                    "SAVE GAME",
                    40f,
                    TextAlignmentOptions.Center,
                    s_pressStartTextStyle?.font);

                RectTransform characterName = GetOrCreateRectTransform(panel, "Character Name");
                ConfigureCenteredRect(
                    characterName,
                    new Vector2(0f, 105f),
                    new Vector2(560f, 46f));
                ConfigureText(
                    characterName.gameObject,
                    "UNNAMED",
                    30f,
                    TextAlignmentOptions.Center,
                    s_pressStartTextStyle?.font);

                RectTransform saveDetails = GetOrCreateRectTransform(panel, "Save Details");
                ConfigureCenteredRect(
                    saveDetails,
                    new Vector2(0f, 62f),
                    new Vector2(560f, 32f));
                ConfigureText(
                    saveDetails.gameObject,
                    "SLOT 01   00:00:00",
                    22f,
                    TextAlignmentOptions.Center,
                    s_pressStartTextStyle?.font);

                RectTransform feedback = GetOrCreateRectTransform(panel, "Feedback");
                ConfigureCenteredRect(
                    feedback,
                    new Vector2(0f, 18f),
                    new Vector2(560f, 32f));
                ConfigureText(
                    feedback.gameObject,
                    "SAVE THE CURRENT POSITION AND PLAY TIME",
                    18f,
                    TextAlignmentOptions.Center,
                    s_pressStartTextStyle?.font);

                RectTransform saveButtonRect = GetOrCreateRectTransform(panel, "Save Game Button");
                ConfigureCenteredRect(
                    saveButtonRect,
                    new Vector2(0f, -62f),
                    new Vector2(360f, 50f));
                Button saveButton = ConfigureButton(
                    saveButtonRect.gameObject,
                    "SAVE GAME",
                    s_pressStartTextStyle?.font);
                ConfigureButtonEvent(saveButton, saveGameManager.SaveCurrentGame);

                RectTransform returnButtonRect = GetOrCreateRectTransform(panel, "Return To Game Button");
                ConfigureCenteredRect(
                    returnButtonRect,
                    new Vector2(0f, -130f),
                    new Vector2(360f, 50f));
                Button returnButton = ConfigureButton(
                    returnButtonRect.gameObject,
                    "RETURN TO GAME",
                    s_pressStartTextStyle?.font);
                ConfigureButtonEvent(returnButton, saveGameManager.CloseSaveGameMenu);
                SetVerticalExplicitNavigation(saveButton, returnButton);

                RectTransform inputHint = GetOrCreateRectTransform(panel, "Input Hint");
                ConfigureCenteredRect(
                    inputHint,
                    new Vector2(0f, -208f),
                    new Vector2(560f, 28f));
                ConfigureText(
                    inputHint.gameObject,
                    "ESC / MENU   CLOSE",
                    17f,
                    TextAlignmentOptions.Center,
                    s_pressStartTextStyle?.font);

                GameObject menuEventSystem =
                    GetOrCreateGameObject(playerUIRoot.transform, "Save Game EventSystem");
                GetOrAddComponent<EventSystem>(menuEventSystem);
                InputSystemUIInputModule inputModule =
                    GetOrAddComponent<InputSystemUIInputModule>(menuEventSystem);
                EditorUtility.CopySerialized(inputModuleTemplate, inputModule);

                SetObjectReference(saveGameManager, "m_saveGameMenu", saveMenu.gameObject);
                SetObjectReference(saveGameManager, "m_saveGameButton", saveButton);
                SetObjectReference(saveGameManager, "m_returnToGameButton", returnButton);
                SetObjectReference(
                    saveGameManager,
                    "m_characterNameText",
                    characterName.GetComponent<TMP_Text>());
                SetObjectReference(
                    saveGameManager,
                    "m_saveDetailsText",
                    saveDetails.GetComponent<TMP_Text>());
                SetObjectReference(
                    saveGameManager,
                    "m_feedbackText",
                    feedback.GetComponent<TMP_Text>());
                SetObjectReference(playerUIManager, "m_menuEventSystem", menuEventSystem);
                SetObjectReference(
                    playerUIManager,
                    "m_playerUISaveGameManager",
                    saveGameManager);

                saveMenu.gameObject.SetActive(false);
                menuEventSystem.SetActive(false);
                EditorUtility.SetDirty(saveGameManager);
                EditorUtility.SetDirty(playerUIManager);
                PrefabUtility.SaveAsPrefabAsset(playerUIRoot, k_PlayerUIManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerUIRoot);
            }
        }

        private static Scene GetMainMenuSceneForEditing()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == k_MainMenuScenePath)
            {
                return activeScene;
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene openScene = SceneManager.GetSceneAt(index);
                if (openScene.isDirty)
                {
                    throw new InvalidOperationException(
                        $"Save the open Scene '{openScene.name}' before configuring the main menu.");
                }
            }

            return EditorSceneManager.OpenScene(k_MainMenuScenePath, OpenSceneMode.Single);
        }

        private static void ConfigureMainMenuScene(Scene scene)
        {
            GameObject canvasObject = FindGameObject(scene, "Title Screen Canvas");
            GameObject backgroundObject = FindGameObject(scene, "Title Screen Background");
            GameObject pressStartObject = FindGameObject(scene, "Press Start Button");
            GameObject mainMenuObject = FindGameObject(scene, "Title Screen Main Menu");
            GameObject newGameObject = FindGameObject(scene, "New Game Start Button");
            if (canvasObject == null ||
                backgroundObject == null ||
                pressStartObject == null ||
                mainMenuObject == null ||
                newGameObject == null)
            {
                throw new InvalidOperationException(
                    "The main menu Scene is missing its existing title-screen hierarchy.");
            }

            TitleScreenManager titleScreenManager =
                backgroundObject.GetComponent<TitleScreenManager>();
            if (titleScreenManager == null)
            {
                titleScreenManager = GetOrAddComponent<TitleScreenManager>(backgroundObject);
            }

            TitleScreenManager duplicateManager = canvasObject.GetComponent<TitleScreenManager>();
            if (duplicateManager != null && duplicateManager != titleScreenManager)
            {
                UnityEngine.Object.DestroyImmediate(duplicateManager);
            }
            Button pressStartButton = GetOrAddComponent<Button>(pressStartObject);
            Button newGameButton = GetOrAddComponent<Button>(newGameObject);
            s_pressStartButtonStyle = pressStartButton;
            s_pressStartTextStyle = pressStartObject.GetComponentInChildren<TMP_Text>(true);
            TMP_Text templateText = s_pressStartTextStyle;
            TMP_FontAsset templateFont = templateText != null ? templateText.font : null;

            RectTransform pressStartRect = pressStartObject.GetComponent<RectTransform>();
            RectTransform mainMenuRect = mainMenuObject.GetComponent<RectTransform>();
            StretchToParent(mainMenuRect);
            float pressStartY = pressStartRect.anchoredPosition.y;
            Vector2 mainMenuButtonSize = pressStartRect.sizeDelta;

            ConfigureExistingMainMenuButton(
                newGameButton,
                "NEW GAME",
                new Vector2(0f, pressStartY + 32f),
                mainMenuButtonSize);
            Button loadGameButton = CreateMainMenuButton(
                mainMenuObject.transform,
                newGameObject,
                "Load Game Button",
                "LOAD GAME",
                new Vector2(0f, pressStartY - 32f),
                mainMenuButtonSize);

            GameObject loadMenu = ConfigureLoadGameMenu(
                backgroundObject.transform,
                titleScreenManager,
                templateFont,
                out Button returnButton,
                out UICharacterSaveSlot[] saveSlots);
            GameObject noFreeSlotsPopup = ConfigureNoFreeSlotsPopup(
                backgroundObject.transform,
                titleScreenManager,
                templateFont,
                out Button noFreeSlotsCloseButton);
            GameObject deletePopup = ConfigureDeletePopup(
                backgroundObject.transform,
                titleScreenManager,
                templateFont,
                out Button confirmDeleteButton);

            ConfigureButtonEvent(pressStartButton, titleScreenManager.PressStart);
            ConfigureButtonEvent(newGameButton, titleScreenManager.StartNewGame);
            ConfigureButtonEvent(loadGameButton, titleScreenManager.OpenLoadGameMenu);
            ConfigureTitleScreenManager(
                titleScreenManager,
                pressStartObject,
                mainMenuObject,
                loadMenu,
                noFreeSlotsPopup,
                deletePopup,
                newGameButton,
                loadGameButton,
                returnButton,
                noFreeSlotsCloseButton,
                confirmDeleteButton,
                saveSlots);

            loadMenu.SetActive(false);
            noFreeSlotsPopup.SetActive(false);
            deletePopup.SetActive(false);
            EditorUtility.SetDirty(titleScreenManager);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ConfigureExistingMainMenuButton(
            Button button,
            string label,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            RectTransform rectTransform = button.GetComponent<RectTransform>();
            ConfigureBottomAnchoredRect(rectTransform, anchoredPosition, size);
            Image image = GetOrAddComponent<Image>(button.gameObject);
            ConfigureButtonImage(image);
            button.targetGraphic = image;
            ConfigureButtonVisual(button);
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                CopyPressStartTextStyle(text);
                text.text = label;
                text.raycastTarget = false;
            }
        }

        private static Button CreateMainMenuButton(
            Transform parent,
            GameObject template,
            string objectName,
            string label,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            Transform existing = parent.Find(objectName);
            GameObject buttonObject;
            if (existing != null)
            {
                buttonObject = existing.gameObject;
            }
            else
            {
                buttonObject = UnityEngine.Object.Instantiate(template, parent);
                buttonObject.name = objectName;
            }

            Button button = GetOrAddComponent<Button>(buttonObject);
            ConfigureExistingMainMenuButton(button, label, anchoredPosition, size);
            return button;
        }

        private static GameObject ConfigureLoadGameMenu(
            Transform parent,
            TitleScreenManager titleScreenManager,
            TMP_FontAsset font,
            out Button returnButton,
            out UICharacterSaveSlot[] saveSlots)
        {
            RectTransform loadMenu = GetOrCreateRectTransform(parent, "Load Character Menu");
            StretchToParent(loadMenu);
            Image overlay = GetOrAddComponent<Image>(loadMenu.gameObject);
            overlay.color = s_menuOverlayColor;
            overlay.raycastTarget = true;

            RectTransform title = GetOrCreateRectTransform(loadMenu, "Title");
            ConfigureCenteredRect(title, new Vector2(0f, 420f), new Vector2(800f, 80f));
            ConfigureText(title.gameObject, "LOAD CHARACTER", 44f, TextAlignmentOptions.Center, font);

            RectTransform scrollView = GetOrCreateRectTransform(loadMenu, "Scroll View");
            ConfigureCenteredRect(scrollView, new Vector2(0f, 40f), new Vector2(920f, 620f));
            Image scrollBackground = GetOrAddComponent<Image>(scrollView.gameObject);
            scrollBackground.color = new Color(0f, 0f, 0f, 0.25f);

            RectTransform viewport = GetOrCreateRectTransform(scrollView, "Viewport");
            StretchToParent(viewport);
            viewport.offsetMax = new Vector2(-34f, 0f);
            Image viewportImage = GetOrAddComponent<Image>(viewport.gameObject);
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportImage.raycastTarget = true;
            Mask mask = GetOrAddComponent<Mask>(viewport.gameObject);
            mask.showMaskGraphic = false;

            RectTransform content = GetOrCreateRectTransform(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            ConfigureSlotLayout(content.gameObject);

            saveSlots = ConfigureCharacterSlots(content, titleScreenManager, font);
            Scrollbar scrollbar = ConfigureVerticalScrollbar(scrollView);
            ScrollRect scrollRect = GetOrAddComponent<ScrollRect>(scrollView.gameObject);
            scrollRect.content = content;
            scrollRect.viewport = viewport;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 35f;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            UIMatchScrollWheelToSelectedButton selectionFollower =
                GetOrAddComponent<UIMatchScrollWheelToSelectedButton>(scrollView.gameObject);
            SetObjectReference(selectionFollower, "m_scrollRect", scrollRect);
            SetObjectReference(selectionFollower, "m_content", content);

            RectTransform returnRect = GetOrCreateRectTransform(loadMenu, "Return Button");
            ConfigureCenteredRect(returnRect, new Vector2(0f, -360f), new Vector2(320f, 68f));
            returnButton = ConfigureButton(returnRect.gameObject, "RETURN", font);
            ConfigureButtonEvent(returnButton, titleScreenManager.CloseLoadGameMenu);
            UITitleScreenSelectNoSlot selectionReset =
                GetOrAddComponent<UITitleScreenSelectNoSlot>(returnRect.gameObject);
            SetObjectReference(selectionReset, "m_titleScreenManager", titleScreenManager);

            TitleScreenLoadMenuInputManager inputManager =
                GetOrAddComponent<TitleScreenLoadMenuInputManager>(loadMenu.gameObject);
            SetObjectReference(inputManager, "m_titleScreenManager", titleScreenManager);
            return loadMenu.gameObject;
        }

        private static void ConfigureSlotLayout(GameObject content)
        {
            VerticalLayoutGroup layoutGroup = GetOrAddComponent<VerticalLayoutGroup>(content);
            layoutGroup.padding = new RectOffset(18, 18, 18, 18);
            layoutGroup.spacing = 14f;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            ContentSizeFitter fitter = GetOrAddComponent<ContentSizeFitter>(content);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static UICharacterSaveSlot[] ConfigureCharacterSlots(
            RectTransform content,
            TitleScreenManager titleScreenManager,
            TMP_FontAsset font)
        {
            List<UICharacterSaveSlot> slots = new List<UICharacterSaveSlot>(10);
            for (int slotNumber = 1; slotNumber <= 10; slotNumber++)
            {
                string slotName = $"Character Slot {slotNumber:00}";
                RectTransform slotRect = GetOrCreateRectTransform(content, slotName);
                slotRect.sizeDelta = new Vector2(0f, 82f);
                LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(slotRect.gameObject);
                layoutElement.preferredHeight = 82f;
                layoutElement.minHeight = 82f;

                Button button = ConfigureButton(slotRect.gameObject, string.Empty, font);
                RectTransform defaultLabel = slotRect.Find("Label") as RectTransform;
                if (defaultLabel != null)
                {
                    UnityEngine.Object.DestroyImmediate(defaultLabel.gameObject);
                }

                RectTransform nameText = GetOrCreateRectTransform(slotRect, "Character Name");
                SetHorizontalTextRect(nameText, 0f, 0.68f, 28f, 22f);
                ConfigureText(
                    nameText.gameObject,
                    "CHARACTER",
                    28f,
                    TextAlignmentOptions.MidlineLeft,
                    font);

                RectTransform timeText = GetOrCreateRectTransform(slotRect, "Time Played");
                SetHorizontalTextRect(timeText, 0.68f, 1f, 22f, 22f);
                ConfigureText(
                    timeText.gameObject,
                    "00:00:00",
                    24f,
                    TextAlignmentOptions.MidlineRight,
                    font);

                UICharacterSaveSlot saveSlot =
                    GetOrAddComponent<UICharacterSaveSlot>(slotRect.gameObject);
                SetEnum(saveSlot, "m_characterSlot", slotNumber);
                SetObjectReference(saveSlot, "m_characterNameText", nameText.GetComponent<TMP_Text>());
                SetObjectReference(saveSlot, "m_timePlayedText", timeText.GetComponent<TMP_Text>());
                SetObjectReference(saveSlot, "m_titleScreenManager", titleScreenManager);
                SetObjectReference(saveSlot, "m_button", button);
                ConfigureButtonEvent(button, saveSlot.LoadGameFromCharacterSlot);
                slots.Add(saveSlot);
            }

            return slots.ToArray();
        }

        private static Scrollbar ConfigureVerticalScrollbar(RectTransform scrollView)
        {
            RectTransform scrollbarRect = GetOrCreateRectTransform(scrollView, "Vertical Scrollbar");
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = Vector2.one;
            scrollbarRect.pivot = Vector2.one;
            scrollbarRect.anchoredPosition = Vector2.zero;
            scrollbarRect.sizeDelta = new Vector2(28f, 0f);
            Image background = GetOrAddComponent<Image>(scrollbarRect.gameObject);
            background.color = new Color(0f, 0f, 0f, 0.55f);

            RectTransform slidingArea = GetOrCreateRectTransform(scrollbarRect, "Sliding Area");
            StretchToParent(slidingArea);
            slidingArea.offsetMin = new Vector2(4f, 4f);
            slidingArea.offsetMax = new Vector2(-4f, -4f);

            RectTransform handle = GetOrCreateRectTransform(slidingArea, "Handle");
            StretchToParent(handle);
            Image handleImage = GetOrAddComponent<Image>(handle.gameObject);
            handleImage.color = new Color(0.65f, 0.52f, 0.22f, 1f);

            Scrollbar scrollbar = GetOrAddComponent<Scrollbar>(scrollbarRect.gameObject);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.size = 0.25f;
            scrollbar.interactable = false;
            scrollbar.navigation = new Navigation
            {
                mode = Navigation.Mode.None
            };
            return scrollbar;
        }

        private static GameObject ConfigureNoFreeSlotsPopup(
            Transform parent,
            TitleScreenManager titleScreenManager,
            TMP_FontAsset font,
            out Button closeButton)
        {
            RectTransform popup = ConfigurePopupRoot(parent, "No Free Character Slots Popup");
            RectTransform panel = ConfigurePopupPanel(popup);
            RectTransform message = GetOrCreateRectTransform(panel, "Message");
            ConfigureCenteredRect(message, new Vector2(0f, 55f), new Vector2(560f, 120f));
            ConfigureText(
                message.gameObject,
                "NO FREE CHARACTER SLOTS",
                34f,
                TextAlignmentOptions.Center,
                font);

            RectTransform closeRect = GetOrCreateRectTransform(panel, "Close Button");
            ConfigureCenteredRect(closeRect, new Vector2(0f, -85f), new Vector2(260f, 64f));
            closeButton = ConfigureButton(closeRect.gameObject, "CLOSE", font);
            ConfigureButtonEvent(closeButton, titleScreenManager.CloseNoFreeCharacterSlotsPopup);
            SetSelfNavigation(closeButton);
            popup.SetAsLastSibling();
            return popup.gameObject;
        }

        private static GameObject ConfigureDeletePopup(
            Transform parent,
            TitleScreenManager titleScreenManager,
            TMP_FontAsset font,
            out Button confirmButton)
        {
            RectTransform popup = ConfigurePopupRoot(parent, "Delete Character Slot Popup");
            RectTransform panel = ConfigurePopupPanel(popup);
            RectTransform message = GetOrCreateRectTransform(panel, "Message");
            ConfigureCenteredRect(message, new Vector2(0f, 65f), new Vector2(600f, 120f));
            ConfigureText(
                message.gameObject,
                "DELETE THIS CHARACTER?",
                34f,
                TextAlignmentOptions.Center,
                font);

            RectTransform confirmRect = GetOrCreateRectTransform(panel, "Confirm Button");
            ConfigureCenteredRect(confirmRect, new Vector2(-155f, -85f), new Vector2(260f, 64f));
            confirmButton = ConfigureButton(confirmRect.gameObject, "CONFIRM", font);
            ConfigureButtonEvent(confirmButton, titleScreenManager.DeleteCharacterSlot);

            RectTransform noRect = GetOrCreateRectTransform(panel, "No Button");
            ConfigureCenteredRect(noRect, new Vector2(155f, -85f), new Vector2(260f, 64f));
            Button noButton = ConfigureButton(noRect.gameObject, "NO", font);
            ConfigureButtonEvent(noButton, titleScreenManager.CloseDeleteCharacterPopup);
            SetHorizontalExplicitNavigation(confirmButton, noButton);
            popup.SetAsLastSibling();
            return popup.gameObject;
        }

        private static RectTransform ConfigurePopupRoot(Transform parent, string objectName)
        {
            RectTransform popup = GetOrCreateRectTransform(parent, objectName);
            StretchToParent(popup);
            Image overlay = GetOrAddComponent<Image>(popup.gameObject);
            overlay.color = s_menuOverlayColor;
            overlay.raycastTarget = true;
            return popup;
        }

        private static RectTransform ConfigurePopupPanel(RectTransform popup)
        {
            RectTransform panel = GetOrCreateRectTransform(popup, "Panel");
            ConfigureCenteredRect(panel, Vector2.zero, new Vector2(680f, 330f));
            Image panelImage = GetOrAddComponent<Image>(panel.gameObject);
            panelImage.color = s_panelColor;
            panelImage.raycastTarget = true;
            return panel;
        }

        private static Button ConfigureButton(GameObject buttonObject, string label, TMP_FontAsset font)
        {
            Image image = GetOrAddComponent<Image>(buttonObject);
            ConfigureButtonImage(image);
            Button button = GetOrAddComponent<Button>(buttonObject);
            button.targetGraphic = image;
            ConfigureButtonVisual(button);

            RectTransform labelRect = GetOrCreateRectTransform(buttonObject.transform, "Label");
            StretchToParent(labelRect);
            labelRect.offsetMin = new Vector2(16f, 8f);
            labelRect.offsetMax = new Vector2(-16f, -8f);
            ConfigureText(
                labelRect.gameObject,
                label,
                28f,
                TextAlignmentOptions.Center,
                font);
            return button;
        }

        private static void ConfigureButtonVisual(Button button)
        {
            if (s_pressStartButtonStyle != null)
            {
                button.transition = s_pressStartButtonStyle.transition;
                button.colors = s_pressStartButtonStyle.colors;
                button.spriteState = s_pressStartButtonStyle.spriteState;
                button.animationTriggers = s_pressStartButtonStyle.animationTriggers;
                return;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.9f, 0.58f, 1f);
            colors.selectedColor = s_selectedButtonColor;
            colors.pressedColor = new Color(0.75f, 0.58f, 0.22f, 1f);
            colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
            colors.colorMultiplier = 1f;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = colors;
        }

        private static void ConfigureButtonImage(Image image)
        {
            Image templateImage = s_pressStartButtonStyle?.targetGraphic as Image;
            if (templateImage == null)
            {
                image.color = s_buttonColor;
                return;
            }

            image.sprite = templateImage.sprite;
            image.overrideSprite = templateImage.overrideSprite;
            image.type = templateImage.type;
            image.preserveAspect = templateImage.preserveAspect;
            image.fillCenter = templateImage.fillCenter;
            image.fillMethod = templateImage.fillMethod;
            image.fillAmount = templateImage.fillAmount;
            image.fillClockwise = templateImage.fillClockwise;
            image.fillOrigin = templateImage.fillOrigin;
            image.pixelsPerUnitMultiplier = templateImage.pixelsPerUnitMultiplier;
            image.material = templateImage.material;
            image.color = templateImage.color;
            image.raycastTarget = true;
        }

        private static void CopyPressStartTextStyle(TMP_Text text)
        {
            if (s_pressStartTextStyle == null || text == s_pressStartTextStyle)
            {
                return;
            }

            text.font = s_pressStartTextStyle.font;
            text.fontSharedMaterial = s_pressStartTextStyle.fontSharedMaterial;
            text.color = s_pressStartTextStyle.color;
            text.fontStyle = s_pressStartTextStyle.fontStyle;
            text.fontWeight = s_pressStartTextStyle.fontWeight;
            text.characterSpacing = s_pressStartTextStyle.characterSpacing;
            text.wordSpacing = s_pressStartTextStyle.wordSpacing;
            text.lineSpacing = s_pressStartTextStyle.lineSpacing;
        }

        private static void ConfigureTitleScreenManager(
            TitleScreenManager titleScreenManager,
            GameObject pressStartMenu,
            GameObject mainMenu,
            GameObject loadMenu,
            GameObject noFreeSlotsPopup,
            GameObject deletePopup,
            Button newGameButton,
            Button loadGameButton,
            Button returnButton,
            Button noFreeSlotsCloseButton,
            Button confirmDeleteButton,
            UICharacterSaveSlot[] saveSlots)
        {
            SerializedObject serializedManager = new SerializedObject(titleScreenManager);
            SetObjectReference(serializedManager, "m_pressStartMenu", pressStartMenu);
            SetObjectReference(serializedManager, "m_mainMenu", mainMenu);
            SetObjectReference(serializedManager, "m_loadGameMenu", loadMenu);
            SetObjectReference(serializedManager, "m_noFreeCharacterSlotsPopup", noFreeSlotsPopup);
            SetObjectReference(serializedManager, "m_deleteCharacterSlotPopup", deletePopup);
            SetObjectReference(serializedManager, "m_newGameButton", newGameButton);
            SetObjectReference(serializedManager, "m_loadGameButton", loadGameButton);
            SetObjectReference(serializedManager, "m_loadGameReturnButton", returnButton);
            SetObjectReference(serializedManager, "m_noFreeSlotsCloseButton", noFreeSlotsCloseButton);
            SetObjectReference(serializedManager, "m_confirmDeleteButton", confirmDeleteButton);

            SerializedProperty slotArray = serializedManager.FindProperty("m_characterSaveSlots");
            slotArray.arraySize = saveSlots.Length;
            for (int index = 0; index < saveSlots.Length; index++)
            {
                slotArray.GetArrayElementAtIndex(index).objectReferenceValue = saveSlots[index];
            }

            serializedManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureText(
            GameObject textObject,
            string textValue,
            float fontSize,
            TextAlignmentOptions alignment,
            TMP_FontAsset font)
        {
            TextMeshProUGUI text = GetOrAddComponent<TextMeshProUGUI>(textObject);
            CopyPressStartTextStyle(text);
            text.text = textValue;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (font != null)
            {
                text.font = font;
            }

            EditorUtility.SetDirty(text);
        }

        private static void SetHorizontalTextRect(
            RectTransform rectTransform,
            float minimumAnchor,
            float maximumAnchor,
            float leftPadding,
            float rightPadding)
        {
            rectTransform.anchorMin = new Vector2(minimumAnchor, 0f);
            rectTransform.anchorMax = new Vector2(maximumAnchor, 1f);
            rectTransform.offsetMin = new Vector2(leftPadding, 10f);
            rectTransform.offsetMax = new Vector2(-rightPadding, -10f);
        }

        private static void ConfigureCenteredRect(
            RectTransform rectTransform,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private static void ConfigureBottomAnchoredRect(
            RectTransform rectTransform,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }

        private static void SetHorizontalExplicitNavigation(Button confirmButton, Button noButton)
        {
            Navigation confirmNavigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = noButton,
                selectOnRight = noButton,
                selectOnUp = confirmButton,
                selectOnDown = confirmButton
            };
            Navigation noNavigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = confirmButton,
                selectOnRight = confirmButton,
                selectOnUp = noButton,
                selectOnDown = noButton
            };
            confirmButton.navigation = confirmNavigation;
            noButton.navigation = noNavigation;
        }

        private static void SetVerticalExplicitNavigation(Button upperButton, Button lowerButton)
        {
            upperButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = upperButton,
                selectOnRight = upperButton,
                selectOnUp = lowerButton,
                selectOnDown = lowerButton
            };
            lowerButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = lowerButton,
                selectOnRight = lowerButton,
                selectOnUp = upperButton,
                selectOnDown = upperButton
            };
        }

        private static void SetSelfNavigation(Button button)
        {
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = button,
                selectOnRight = button,
                selectOnUp = button,
                selectOnDown = button
            };
        }

        private static void ConfigureButtonEvent(Button button, UnityAction action)
        {
            for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, index);
            }

            UnityEventTools.AddPersistentListener(button.onClick, action);
            EditorUtility.SetDirty(button);
        }

        private static RectTransform GetOrCreateRectTransform(Transform parent, string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing is RectTransform existingRect)
            {
                return existingRect;
            }

            GameObject child = new GameObject(objectName, typeof(RectTransform));
            RectTransform rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static GameObject GetOrCreateGameObject(Transform parent, string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static GameObject FindGameObject(Scene scene, string objectName)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (Transform candidate in rootObject.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == objectName)
                    {
                        return candidate.gameObject;
                    }
                }
            }

            return null;
        }

        private static void SetObjectReference(Component component, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SetObjectReference(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name}.{propertyName} was not found.");
            }

            property.objectReferenceValue = value;
        }

        private static void SetString(Component component, string propertyName, string value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInteger(Component component, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Component component, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.enumValueIndex = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidatePersistenceBoundary()
        {
            string testDirectory = Path.Combine(
                Path.GetTempPath(),
                "EldenSaveGameValidation",
                Guid.NewGuid().ToString("N"));

            try
            {
                SaveFileDataWriter writer = new SaveFileDataWriter(testDirectory, "CharacterSlot01");
                CharacterSaveData expectedData = new CharacterSaveData
                {
                    CharacterName = "TestKnight",
                    SecondsPlayed = 123.5f,
                    XPosition = 10f,
                    YPosition = 2f,
                    ZPosition = 30f,
                    SceneIndex = 1
                };

                writer.SaveFile(expectedData);
                CharacterSaveData loadedData = writer.LoadSaveFile();
                if (!writer.CheckToSeeIfFileExists() ||
                    loadedData == null ||
                    loadedData.CharacterName != "TestKnight" ||
                    !Mathf.Approximately(loadedData.SecondsPlayed, 123.5f) ||
                    !Mathf.Approximately(loadedData.XPosition, 10f) ||
                    !Mathf.Approximately(loadedData.YPosition, 2f) ||
                    !Mathf.Approximately(loadedData.ZPosition, 30f) ||
                    loadedData.SceneIndex != 1)
                {
                    throw new InvalidOperationException(
                        "SaveFileDataWriter did not round-trip the expected character data.");
                }

                writer.DeleteSaveFile();
                if (writer.CheckToSeeIfFileExists())
                {
                    throw new InvalidOperationException("SaveFileDataWriter did not delete its save file.");
                }
            }
            finally
            {
                if (Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, true);
                }
            }
        }

        private static void ValidateWorldSaveManagerPrefab()
        {
            GameObject managerRoot = PrefabUtility.LoadPrefabContents(k_WorldSaveManagerPrefabPath);

            try
            {
                WorldSaveGameManager manager = managerRoot.GetComponent<WorldSaveGameManager>();
                if (manager == null)
                {
                    throw new InvalidOperationException("The World Save Manager prefab is missing its manager.");
                }

                HashSet<string> fileNames = new HashSet<string>();
                for (int slotNumber = 1; slotNumber <= 10; slotNumber++)
                {
                    string expectedFileName = $"CharacterSlot{slotNumber:00}";
                    string actualFileName = manager.DecideCharacterFileNameBasedOnCharacterSlot(
                        (CharacterSlot)slotNumber);
                    if (actualFileName != expectedFileName || !fileNames.Add(actualFileName))
                    {
                        throw new InvalidOperationException(
                            "Every fixed character slot must map to one unique canonical filename.");
                    }
                }

                SerializedObject serializedManager = new SerializedObject(manager);
                if (serializedManager.FindProperty("m_startingSceneIndex").intValue != 1)
                {
                    throw new InvalidOperationException("New characters must begin in Scene build index 1.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(managerRoot);
            }
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                PlayerNetworkManager networkManager = playerRoot.GetComponent<PlayerNetworkManager>();
                if (networkManager == null ||
                    networkManager.CharacterName.ReadPerm != NetworkVariableReadPermission.Everyone ||
                    networkManager.CharacterName.WritePerm != NetworkVariableWritePermission.Owner)
                {
                    throw new InvalidOperationException(
                        "CharacterName must be readable by everyone and writable only by its Owner.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidatePlayerUIManagerPrefab()
        {
            GameObject playerUIRoot = PrefabUtility.LoadPrefabContents(k_PlayerUIManagerPrefabPath);
            try
            {
                PlayerUIManager playerUIManager = playerUIRoot.GetComponent<PlayerUIManager>();
                PlayerUISaveGameManager saveGameManager =
                    playerUIRoot.GetComponent<PlayerUISaveGameManager>();
                GameObject saveMenu = FindGameObjectInChildren(playerUIRoot, "Save Game Menu");
                GameObject menuEventSystem =
                    FindGameObjectInChildren(playerUIRoot, "Save Game EventSystem");
                GameObject saveButtonObject =
                    FindGameObjectInChildren(playerUIRoot, "Save Game Button");
                GameObject returnButtonObject =
                    FindGameObjectInChildren(playerUIRoot, "Return To Game Button");
                Button saveButton = saveButtonObject?.GetComponent<Button>();
                Button returnButton = returnButtonObject?.GetComponent<Button>();
                InputSystemUIInputModule inputModule =
                    menuEventSystem?.GetComponent<InputSystemUIInputModule>();
                SerializedObject serializedPlayerUI = playerUIManager != null
                    ? new SerializedObject(playerUIManager)
                    : null;

                if (playerUIManager == null ||
                    saveGameManager == null ||
                    saveMenu == null ||
                    saveMenu.activeSelf ||
                    menuEventSystem == null ||
                    menuEventSystem.activeSelf ||
                    menuEventSystem.GetComponent<EventSystem>() == null ||
                    inputModule == null ||
                    saveButton == null ||
                    returnButton == null ||
                    saveButton.navigation.mode != Navigation.Mode.Explicit ||
                    returnButton.navigation.mode != Navigation.Mode.Explicit ||
                    serializedPlayerUI.FindProperty("m_playerUISaveGameManager")
                        ?.objectReferenceValue != saveGameManager ||
                    new SerializedObject(saveGameManager)
                        .FindProperty("m_saveGameMenu")?.objectReferenceValue != saveMenu ||
                    serializedPlayerUI.FindProperty("m_menuEventSystem")
                        ?.objectReferenceValue != menuEventSystem)
                {
                    throw new InvalidOperationException(
                        "The Player UI Manager prefab is missing the modal Save Game menu or navigation.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerUIRoot);
            }
        }

        private static void ValidateInputActions()
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                k_PlayerControlsPath);
            InputAction deleteAction = inputActions?.FindActionMap("UI")?.FindAction("Delete");
            InputAction openCharacterMenuAction = inputActions
                ?.FindActionMap("UI")
                ?.FindAction("Open Character Menu");
            if (deleteAction == null ||
                !HasBinding(deleteAction, "<Gamepad>/buttonWest") ||
                !HasBinding(deleteAction, "<Keyboard>/delete") ||
                openCharacterMenuAction == null ||
                !HasBinding(openCharacterMenuAction, "<Gamepad>/start") ||
                !HasBinding(openCharacterMenuAction, "<Keyboard>/escape"))
            {
                throw new InvalidOperationException(
                    "PlayerControls must support title deletion and keyboard/controller menu input.");
            }
        }

        private static void ValidateMainMenuScene()
        {
            Scene scene = GetMainMenuSceneForEditing();
            ValidateNoMissingScripts(scene);
            GameObject canvas = FindGameObject(scene, "Title Screen Canvas");
            GameObject loadMenu = FindGameObject(scene, "Load Character Menu");
            GameObject scrollView = FindGameObject(scene, "Scroll View");
            GameObject viewport = FindGameObject(scene, "Viewport");
            GameObject content = FindGameObject(scene, "Content");
            GameObject deletePopup = FindGameObject(scene, "Delete Character Slot Popup");
            GameObject pressStartObject = FindGameObject(scene, "Press Start Button");
            GameObject mainMenuObject = FindGameObject(scene, "Title Screen Main Menu");
            GameObject newGameObject = FindGameObject(scene, "New Game Start Button");
            GameObject loadGameObject = FindGameObject(scene, "Load Game Button");
            UICharacterSaveSlot[] slots = loadMenu?.GetComponentsInChildren<UICharacterSaveSlot>(true);
            ScrollRect scrollRect = scrollView?.GetComponent<ScrollRect>();
            Button confirmButton = deletePopup != null
                ? FindGameObjectInChildren(deletePopup, "Confirm Button")?.GetComponent<Button>()
                : null;
            Button noButton = deletePopup != null
                ? FindGameObjectInChildren(deletePopup, "No Button")?.GetComponent<Button>()
                : null;

            TitleScreenManager[] titleScreenManagers =
                canvas?.GetComponentsInChildren<TitleScreenManager>(true);
            RectTransform pressStartRect = pressStartObject?.GetComponent<RectTransform>();
            RectTransform mainMenuRect = mainMenuObject?.GetComponent<RectTransform>();
            RectTransform newGameRect = newGameObject?.GetComponent<RectTransform>();
            RectTransform loadGameRect = loadGameObject?.GetComponent<RectTransform>();
            Button pressStartButton = pressStartObject?.GetComponent<Button>();
            Button[] saveGameButtons = canvas?.GetComponentsInChildren<Button>(true);
            if (titleScreenManagers == null ||
                titleScreenManagers.Length != 1 ||
                loadMenu?.GetComponent<TitleScreenLoadMenuInputManager>() == null ||
                FindGameObjectInChildren(loadMenu, "Return Button")
                    ?.GetComponent<UITitleScreenSelectNoSlot>() == null ||
                slots == null ||
                slots.Length != 10 ||
                viewport?.GetComponent<Mask>() == null ||
                content?.GetComponent<VerticalLayoutGroup>() == null ||
                scrollRect == null ||
                !scrollRect.vertical ||
                scrollRect.horizontal ||
                scrollRect.movementType != ScrollRect.MovementType.Clamped ||
                scrollRect.content != content?.GetComponent<RectTransform>() ||
                scrollRect.viewport != viewport?.GetComponent<RectTransform>() ||
                scrollRect.verticalScrollbar == null ||
                scrollRect.verticalScrollbar.interactable ||
                scrollRect.verticalScrollbar.navigation.mode != Navigation.Mode.None ||
                confirmButton == null ||
                noButton == null ||
                confirmButton.navigation.mode != Navigation.Mode.Explicit ||
                noButton.navigation.mode != Navigation.Mode.Explicit ||
                pressStartRect == null ||
                mainMenuRect == null ||
                mainMenuRect.anchorMin != Vector2.zero ||
                mainMenuRect.anchorMax != Vector2.one ||
                newGameRect == null ||
                loadGameRect == null ||
                newGameRect.anchorMin.y != 0f ||
                loadGameRect.anchorMin.y != 0f ||
                !Mathf.Approximately(
                    newGameRect.anchoredPosition.y,
                    pressStartRect.anchoredPosition.y + 32f) ||
                !Mathf.Approximately(
                    loadGameRect.anchoredPosition.y,
                    pressStartRect.anchoredPosition.y - 32f) ||
                newGameRect.sizeDelta != pressStartRect.sizeDelta ||
                loadGameRect.sizeDelta != pressStartRect.sizeDelta ||
                pressStartButton == null ||
                saveGameButtons == null ||
                !HaveMatchingButtonStyles(saveGameButtons, pressStartButton))
            {
                throw new InvalidOperationException(
                    "The title UI is missing its fixed-slot flow, Press Start styling, or lower menu layout.");
            }
        }

        private static GameObject FindGameObjectInChildren(GameObject parent, string objectName)
        {
            foreach (Transform candidate in parent.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == objectName)
                {
                    return candidate.gameObject;
                }
            }

            return null;
        }

        private static void ValidateNoMissingScripts(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (Transform candidate in rootObject.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(candidate.gameObject) > 0)
                    {
                        throw new InvalidOperationException(
                            $"The main menu contains a missing script on '{candidate.name}'.");
                    }
                }
            }
        }

        private static bool HasBinding(InputAction action, string effectivePath)
        {
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.effectivePath == effectivePath)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HaveMatchingButtonStyles(Button[] buttons, Button template)
        {
            Image templateImage = template.targetGraphic as Image;
            foreach (Button button in buttons)
            {
                Image image = button.targetGraphic as Image;
                if (image == null ||
                    templateImage == null ||
                    image.sprite != templateImage.sprite ||
                    image.type != templateImage.type ||
                    image.material != templateImage.material ||
                    image.color != templateImage.color ||
                    button.transition != template.transition ||
                    !button.colors.Equals(template.colors))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
