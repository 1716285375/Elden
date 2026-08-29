using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Builds and validates the EP103-106 Level Up menu and Site entry.</summary>
    public static class LevelUpSystemSetup
    {
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        private static readonly Color s_overlayColor =
            new Color(0f, 0f, 0f, 0.76f);
        private static readonly Color s_panelColor =
            new Color(0.025f, 0.02f, 0.015f, 0.97f);
        private static readonly Color s_rowColor =
            new Color(0.12f, 0.105f, 0.08f, 0.78f);
        private static readonly Color s_goldColor =
            new Color(0.82f, 0.67f, 0.32f, 1f);
        private static readonly Color s_textColor =
            new Color(0.9f, 0.86f, 0.74f, 1f);

        private static readonly string[] s_attributeLabels =
        {
            "VIGOR",
            "MIND",
            "ENDURANCE",
            "STRENGTH",
            "DEXTERITY",
            "INTELLIGENCE",
            "FAITH"
        };

        [MenuItem("Tools/Elden/Configure Level Up System")]
        public static void ConfigureLevelUpSystem()
        {
            ConfigurePlayerUIPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateLevelUpSystem();
            Debug.Log(
                "[LevelUpSystemSetup] Configured shared menus, Level Up preview, and Site entry.");
        }

        [MenuItem("Tools/Elden/Validate Level Up System")]
        public static void ValidateLevelUpSystem()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);

            try
            {
                PlayerUIManager uiManager = RequireComponent<PlayerUIManager>(root);
                PlayerUICharacterMenuManager characterMenu =
                    RequireComponent<PlayerUICharacterMenuManager>(root);
                PlayerUIEquipmentManager equipmentMenu =
                    RequireComponent<PlayerUIEquipmentManager>(root);
                PlayerUISiteOfGraceManager siteMenu =
                    RequireComponent<PlayerUISiteOfGraceManager>(root);
                PlayerUITeleportLocationManager teleportMenu =
                    RequireComponent<PlayerUITeleportLocationManager>(root);
                PlayerUILevelUpManager levelUpMenu =
                    RequireComponent<PlayerUILevelUpManager>(root);

                ValidateMenuWindow(characterMenu, "Character Menu");
                ValidateMenuWindow(equipmentMenu, "Equipment Menu");
                ValidateMenuWindow(siteMenu, "Site Of Grace Menu");
                ValidateMenuWindow(teleportMenu, "Teleport Location Menu");
                ValidateMenuWindow(levelUpMenu, "Level Up Menu");
                ValidateLevelUpReferences(root, levelUpMenu, siteMenu);
                ValidateObjectReference(
                    uiManager,
                    "m_playerUILevelUpManager",
                    levelUpMenu);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log(
                "[LevelUpSystemValidation] Menu inheritance, preview rows, and actions are valid.");
        }

        private static void ConfigurePlayerUIPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);

            try
            {
                Transform playerUI = root.transform.Find("Player UI") ??
                    throw new InvalidOperationException(
                        "Player UI Manager prefab requires Player UI.");
                TMP_FontAsset font = root.GetComponentsInChildren<TMP_Text>(true)
                    .Select(text => text.font)
                    .FirstOrDefault(candidate => candidate != null) ??
                    TMP_Settings.defaultFontAsset;
                Button styleButton = root.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.name == "Save Game Button") ??
                    root.GetComponentInChildren<Button>(true);

                PlayerUIManager uiManager = GetOrAddComponent<PlayerUIManager>(root);
                PlayerUICharacterMenuManager characterManager =
                    GetOrAddComponent<PlayerUICharacterMenuManager>(root);
                PlayerUIEquipmentManager equipmentManager =
                    GetOrAddComponent<PlayerUIEquipmentManager>(root);
                PlayerUISiteOfGraceManager siteManager =
                    GetOrAddComponent<PlayerUISiteOfGraceManager>(root);
                PlayerUITeleportLocationManager teleportManager =
                    GetOrAddComponent<PlayerUITeleportLocationManager>(root);
                PlayerUILevelUpManager levelUpManager =
                    GetOrAddComponent<PlayerUILevelUpManager>(root);

                BindExistingMenuWindow(
                    characterManager,
                    playerUI.Find("Character Menu"));
                BindExistingMenuWindow(
                    equipmentManager,
                    playerUI.Find("Equipment Menu"));
                Transform siteMenu = playerUI.Find("Site Of Grace Menu");
                BindExistingMenuWindow(siteManager, siteMenu);
                BindExistingMenuWindow(
                    teleportManager,
                    playerUI.Find("Teleport Location Menu"));

                Button levelEntryButton = ConfigureSiteMenu(
                    siteMenu,
                    siteManager,
                    font,
                    styleButton);
                RectTransform levelMenu = ConfigureMenuRoot(
                    playerUI,
                    "Level Up Menu");
                LevelUpReferences references = ConfigureLevelUpMenu(
                    levelMenu,
                    levelUpManager,
                    siteManager,
                    font,
                    styleButton);

                BindExistingMenuWindow(levelUpManager, levelMenu);
                SetObjectReference(
                    siteManager,
                    "m_levelUpButton",
                    levelEntryButton);
                SetObjectReference(
                    levelUpManager,
                    "m_characterLevelText",
                    references.CharacterLevel);
                SetObjectReference(
                    levelUpManager,
                    "m_projectedCharacterLevelText",
                    references.ProjectedCharacterLevel);
                SetObjectReference(
                    levelUpManager,
                    "m_runesHeldText",
                    references.RunesHeld);
                SetObjectReference(
                    levelUpManager,
                    "m_projectedRunesHeldText",
                    references.ProjectedRunesHeld);
                SetObjectReference(
                    levelUpManager,
                    "m_runesNeededText",
                    references.RunesNeeded);
                SetObjectArray(
                    levelUpManager,
                    "m_attributeSliders",
                    references.AttributeSliders);
                SetObjectArray(
                    levelUpManager,
                    "m_currentAttributeTexts",
                    references.CurrentAttributeTexts);
                SetObjectArray(
                    levelUpManager,
                    "m_projectedAttributeTexts",
                    references.ProjectedAttributeTexts);
                SetObjectReference(
                    levelUpManager,
                    "m_confirmButton",
                    references.ConfirmButton);
                SetObjectReference(
                    uiManager,
                    "m_playerUILevelUpManager",
                    levelUpManager);

                levelMenu.gameObject.SetActive(false);
                SetUILayerRecursively(levelMenu.gameObject);
                EditorUtility.SetDirty(uiManager);
                EditorUtility.SetDirty(siteManager);
                EditorUtility.SetDirty(levelUpManager);
                PrefabUtility.SaveAsPrefabAsset(root, k_PlayerUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Button ConfigureSiteMenu(
            Transform siteMenu,
            PlayerUISiteOfGraceManager siteManager,
            TMP_FontAsset font,
            Button styleButton)
        {
            Transform panel = siteMenu?.Find("Panel") ??
                throw new InvalidOperationException(
                    "Site Of Grace Menu requires its Panel.");
            Button levelUpButton = ConfigureButton(
                panel,
                "Level Up Button",
                "LEVEL UP",
                new Vector2(0f, 55f),
                font,
                styleButton);
            Button travelButton = panel.Find("Travel Button")
                ?.GetComponent<Button>();
            Button returnButton = panel.Find("Return Button")
                ?.GetComponent<Button>();
            SetCenteredPosition(travelButton, new Vector2(0f, -25f));
            SetCenteredPosition(returnButton, new Vector2(0f, -105f));
            ConfigureButtonEvent(levelUpButton, siteManager.OpenLevelUpMenu);
            ConfigureVerticalNavigation(
                new[] { levelUpButton, travelButton, returnButton });
            return levelUpButton;
        }

        private static LevelUpReferences ConfigureLevelUpMenu(
            RectTransform levelMenu,
            PlayerUILevelUpManager levelUpManager,
            PlayerUISiteOfGraceManager siteManager,
            TMP_FontAsset font,
            Button styleButton)
        {
            RectTransform panel = GetOrCreateRectTransform(levelMenu, "Panel");
            ConfigureCenteredRect(panel, Vector2.zero, new Vector2(1220f, 940f));
            Image panelImage = GetOrAddComponent<Image>(panel.gameObject);
            panelImage.color = s_panelColor;
            panelImage.raycastTarget = true;

            TMP_Text title = ConfigureText(
                panel,
                "Title",
                "LEVEL UP",
                new Vector2(0f, 405f),
                new Vector2(1000f, 66f),
                40f,
                font,
                TextAlignmentOptions.Center);
            title.color = s_goldColor;

            LevelUpReferences references = new LevelUpReferences
            {
                CharacterLevel = ConfigureSummaryRow(
                    panel,
                    "Character Level",
                    "CHARACTER LEVEL",
                    310f,
                    font,
                    out TMP_Text projectedLevel),
                ProjectedCharacterLevel = projectedLevel,
                RunesHeld = ConfigureSummaryRow(
                    panel,
                    "Runes Held",
                    "RUNES HELD",
                    258f,
                    font,
                    out TMP_Text projectedRunes),
                ProjectedRunesHeld = projectedRunes,
                RunesNeeded = ConfigureSingleValueRow(
                    panel,
                    "Runes Needed",
                    "RUNES NEEDED",
                    206f,
                    font),
                AttributeSliders = new UICharacterAttributeSlider[7],
                CurrentAttributeTexts = new TMP_Text[7],
                ProjectedAttributeTexts = new TMP_Text[7]
            };

            ConfigureAttributeHeader(panel, font);
            for (int index = 0; index < s_attributeLabels.Length; index++)
            {
                ConfigureAttributeRow(
                    panel,
                    levelUpManager,
                    (CharacterAttribute)index,
                    s_attributeLabels[index],
                    100f - index * 66f,
                    font,
                    out UICharacterAttributeSlider slider,
                    out TMP_Text currentText,
                    out TMP_Text projectedText);
                references.AttributeSliders[index] = slider;
                references.CurrentAttributeTexts[index] = currentText;
                references.ProjectedAttributeTexts[index] = projectedText;
            }

            references.ConfirmButton = ConfigureButton(
                panel,
                "Confirm Button",
                "CONFIRM LEVELS",
                new Vector2(-235f, -384f),
                font,
                styleButton,
                new Vector2(400f, 64f));
            Button returnButton = ConfigureButton(
                panel,
                "Return Button",
                "RETURN",
                new Vector2(235f, -384f),
                font,
                styleButton,
                new Vector2(400f, 64f));
            ConfigureButtonEvent(
                references.ConfirmButton,
                levelUpManager.ConfirmLevels);
            ConfigureButtonEvent(returnButton, siteManager.OpenSiteOfGraceMenu);
            ConfigureLevelNavigation(
                references.AttributeSliders,
                references.ConfirmButton,
                returnButton);
            return references;
        }

        private static TMP_Text ConfigureSummaryRow(
            Transform parent,
            string objectName,
            string label,
            float positionY,
            TMP_FontAsset font,
            out TMP_Text projectedText)
        {
            RectTransform row = GetOrCreateRectTransform(parent, objectName);
            ConfigureCenteredRect(
                row,
                new Vector2(0f, positionY),
                new Vector2(900f, 46f));
            ConfigureRowBackground(row);
            ConfigureText(
                row,
                "Label",
                label,
                new Vector2(-300f, 0f),
                new Vector2(270f, 40f),
                21f,
                font,
                TextAlignmentOptions.MidlineLeft);
            TMP_Text currentText = ConfigureText(
                row,
                "Current",
                "0",
                new Vector2(85f, 0f),
                new Vector2(150f, 40f),
                23f,
                font,
                TextAlignmentOptions.MidlineRight);
            ConfigureText(
                row,
                "Arrow",
                ">",
                new Vector2(190f, 0f),
                new Vector2(50f, 40f),
                22f,
                font,
                TextAlignmentOptions.Center);
            projectedText = ConfigureText(
                row,
                "Projected",
                "0",
                new Vector2(310f, 0f),
                new Vector2(150f, 40f),
                23f,
                font,
                TextAlignmentOptions.MidlineRight);
            return currentText;
        }

        private static TMP_Text ConfigureSingleValueRow(
            Transform parent,
            string objectName,
            string label,
            float positionY,
            TMP_FontAsset font)
        {
            RectTransform row = GetOrCreateRectTransform(parent, objectName);
            ConfigureCenteredRect(
                row,
                new Vector2(0f, positionY),
                new Vector2(900f, 46f));
            ConfigureRowBackground(row);
            ConfigureText(
                row,
                "Label",
                label,
                new Vector2(-300f, 0f),
                new Vector2(270f, 40f),
                21f,
                font,
                TextAlignmentOptions.MidlineLeft);
            return ConfigureText(
                row,
                "Value",
                "0",
                new Vector2(310f, 0f),
                new Vector2(150f, 40f),
                23f,
                font,
                TextAlignmentOptions.MidlineRight);
        }

        private static void ConfigureAttributeHeader(
            Transform parent,
            TMP_FontAsset font)
        {
            ConfigureText(
                parent,
                "Attribute Header",
                "ATTRIBUTE                         ACTUAL       PROJECTED",
                new Vector2(0f, 151f),
                new Vector2(900f, 36f),
                18f,
                font,
                TextAlignmentOptions.Center).color = s_goldColor;
        }

        private static void ConfigureAttributeRow(
            Transform parent,
            PlayerUILevelUpManager levelUpManager,
            CharacterAttribute characterAttribute,
            string label,
            float positionY,
            TMP_FontAsset font,
            out UICharacterAttributeSlider attributeSlider,
            out TMP_Text currentText,
            out TMP_Text projectedText)
        {
            RectTransform row = GetOrCreateRectTransform(
                parent,
                $"{label} Attribute");
            ConfigureCenteredRect(
                row,
                new Vector2(0f, positionY),
                new Vector2(900f, 56f));
            Image rowImage = ConfigureRowBackground(row);
            Slider slider = GetOrAddComponent<Slider>(row.gameObject);
            slider.targetGraphic = rowImage;
            slider.transition = Selectable.Transition.ColorTint;
            slider.wholeNumbers = true;
            slider.minValue = 10f;
            slider.maxValue = 99f;
            slider.value = 10f;
            slider.fillRect = null;
            slider.handleRect = null;
            slider.direction = Slider.Direction.LeftToRight;
            ColorBlock colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.65f, 1f);
            colors.selectedColor = s_goldColor;
            colors.pressedColor = new Color(0.72f, 0.58f, 0.28f, 1f);
            slider.colors = colors;

            attributeSlider = GetOrAddComponent<UICharacterAttributeSlider>(
                row.gameObject);
            attributeSlider.Configure(characterAttribute, levelUpManager);
            ConfigureText(
                row,
                "Label",
                label,
                new Vector2(-300f, 0f),
                new Vector2(270f, 44f),
                21f,
                font,
                TextAlignmentOptions.MidlineLeft);
            currentText = ConfigureText(
                row,
                "Current",
                "10",
                new Vector2(85f, 0f),
                new Vector2(150f, 44f),
                23f,
                font,
                TextAlignmentOptions.MidlineRight);
            ConfigureText(
                row,
                "Arrow",
                ">",
                new Vector2(190f, 0f),
                new Vector2(50f, 44f),
                22f,
                font,
                TextAlignmentOptions.Center);
            projectedText = ConfigureText(
                row,
                "Projected",
                "10",
                new Vector2(310f, 0f),
                new Vector2(150f, 44f),
                23f,
                font,
                TextAlignmentOptions.MidlineRight);
        }

        private static Image ConfigureRowBackground(RectTransform row)
        {
            Image image = GetOrAddComponent<Image>(row.gameObject);
            image.color = s_rowColor;
            image.raycastTarget = true;
            return image;
        }

        private static RectTransform ConfigureMenuRoot(
            Transform parent,
            string objectName)
        {
            RectTransform menu = GetOrCreateRectTransform(parent, objectName);
            StretchToParent(menu);
            Image overlay = GetOrAddComponent<Image>(menu.gameObject);
            overlay.color = s_overlayColor;
            overlay.raycastTarget = true;
            GetOrAddComponent<PlayerUIToggleHUD>(menu.gameObject);
            return menu;
        }

        private static TMP_Text ConfigureText(
            Transform parent,
            string objectName,
            string value,
            Vector2 position,
            Vector2 size,
            float fontSize,
            TMP_FontAsset font,
            TextAlignmentOptions alignment)
        {
            RectTransform rectTransform = GetOrCreateRectTransform(
                parent,
                objectName);
            ConfigureCenteredRect(rectTransform, position, size);
            TextMeshProUGUI text = GetOrAddComponent<TextMeshProUGUI>(
                rectTransform.gameObject);
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = s_textColor;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        private static Button ConfigureButton(
            Transform parent,
            string objectName,
            string label,
            Vector2 position,
            TMP_FontAsset font,
            Button styleButton,
            Vector2? size = null)
        {
            RectTransform buttonRect = GetOrCreateRectTransform(
                parent,
                objectName);
            ConfigureCenteredRect(
                buttonRect,
                position,
                size ?? new Vector2(420f, 68f));
            Image image = GetOrAddComponent<Image>(buttonRect.gameObject);
            CopyImageStyle(styleButton?.targetGraphic as Image, image);
            Button button = GetOrAddComponent<Button>(buttonRect.gameObject);
            button.targetGraphic = image;
            CopyButtonStyle(styleButton, button);

            RectTransform labelRect = GetOrCreateRectTransform(
                buttonRect,
                "Label");
            StretchToParent(labelRect);
            labelRect.offsetMin = new Vector2(16f, 5f);
            labelRect.offsetMax = new Vector2(-16f, -5f);
            TextMeshProUGUI text = GetOrAddComponent<TextMeshProUGUI>(
                labelRect.gameObject);
            text.text = label;
            text.font = font;
            text.fontSize = 25f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = s_textColor;
            text.raycastTarget = false;
            return button;
        }

        private static void ConfigureLevelNavigation(
            UICharacterAttributeSlider[] sliders,
            Button confirmButton,
            Button returnButton)
        {
            Slider[] sliderComponents = sliders
                .Select(slider => slider.GetComponent<Slider>())
                .ToArray();
            for (int index = 0; index < sliderComponents.Length; index++)
            {
                sliderComponents[index].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = index == 0
                        ? returnButton
                        : sliderComponents[index - 1],
                    selectOnDown = index == sliderComponents.Length - 1
                        ? confirmButton
                        : sliderComponents[index + 1]
                };
            }

            confirmButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = sliderComponents[^1],
                selectOnDown = sliderComponents[0],
                selectOnLeft = returnButton,
                selectOnRight = returnButton
            };
            returnButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = sliderComponents[^1],
                selectOnDown = sliderComponents[0],
                selectOnLeft = confirmButton,
                selectOnRight = confirmButton
            };
        }

        private static void ConfigureVerticalNavigation(Button[] buttons)
        {
            Button[] validButtons = buttons
                .Where(button => button != null)
                .ToArray();
            for (int index = 0; index < validButtons.Length; index++)
            {
                validButtons[index].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = validButtons[
                        (index - 1 + validButtons.Length) % validButtons.Length],
                    selectOnDown = validButtons[(index + 1) % validButtons.Length]
                };
            }
        }

        private static void ConfigureButtonEvent(Button button, UnityAction action)
        {
            for (int index = button.onClick.GetPersistentEventCount() - 1;
                index >= 0;
                index--)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, index);
            }

            UnityEventTools.AddPersistentListener(button.onClick, action);
            EditorUtility.SetDirty(button);
        }

        private static void CopyImageStyle(Image source, Image destination)
        {
            if (source != null)
            {
                destination.sprite = source.sprite;
                destination.type = source.type;
                destination.material = source.material;
                destination.preserveAspect = source.preserveAspect;
                destination.color = source.color;
            }
            else
            {
                destination.color = s_rowColor;
            }

            destination.raycastTarget = true;
        }

        private static void CopyButtonStyle(Button source, Button destination)
        {
            if (source != null && source != destination)
            {
                destination.transition = source.transition;
                destination.colors = source.colors;
                destination.spriteState = source.spriteState;
                destination.animationTriggers = source.animationTriggers;
                return;
            }

            ColorBlock colors = destination.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.9f, 0.58f, 1f);
            colors.selectedColor = s_goldColor;
            colors.pressedColor = new Color(0.7f, 0.52f, 0.2f, 1f);
            colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
            destination.colors = colors;
        }

        private static void BindExistingMenuWindow(
            PlayerUIMenu manager,
            Transform menuWindow)
        {
            if (menuWindow == null)
            {
                throw new InvalidOperationException(
                    $"{manager.GetType().Name} is missing its menu window.");
            }

            SetObjectReference(manager, "m_menuWindow", menuWindow.gameObject);
        }

        private static void ValidateMenuWindow(
            PlayerUIMenu manager,
            string expectedName)
        {
            SerializedObject serializedObject = new SerializedObject(manager);
            GameObject menuWindow = serializedObject.FindProperty("m_menuWindow")
                .objectReferenceValue as GameObject;
            if (menuWindow == null ||
                menuWindow.name != expectedName ||
                menuWindow.activeSelf)
            {
                throw new InvalidOperationException(
                    $"{manager.GetType().Name} has an invalid menu window.");
            }
        }

        private static void ValidateLevelUpReferences(
            GameObject root,
            PlayerUILevelUpManager levelUpManager,
            PlayerUISiteOfGraceManager siteManager)
        {
            SerializedObject serializedLevelUp = new SerializedObject(
                levelUpManager);
            SerializedProperty sliders = serializedLevelUp.FindProperty(
                "m_attributeSliders");
            SerializedProperty currentTexts = serializedLevelUp.FindProperty(
                "m_currentAttributeTexts");
            SerializedProperty projectedTexts = serializedLevelUp.FindProperty(
                "m_projectedAttributeTexts");
            if (sliders.arraySize != 7 ||
                currentTexts.arraySize != 7 ||
                projectedTexts.arraySize != 7 ||
                serializedLevelUp.FindProperty("m_confirmButton")
                    .objectReferenceValue == null)
            {
                throw new InvalidOperationException(
                    "Level Up menu requires seven complete attribute rows.");
            }

            for (int index = 0; index < 7; index++)
            {
                UICharacterAttributeSlider slider = sliders
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue as UICharacterAttributeSlider;
                if (slider == null ||
                    slider.CharacterAttribute != (CharacterAttribute)index ||
                    currentTexts.GetArrayElementAtIndex(index)
                        .objectReferenceValue == null ||
                    projectedTexts.GetArrayElementAtIndex(index)
                        .objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"Level Up attribute row {index} is incomplete.");
                }
            }

            Button levelButton = root.transform
                .Find("Player UI/Site Of Grace Menu/Panel/Level Up Button")
                ?.GetComponent<Button>();
            if (!HasPersistentListener(
                    levelButton,
                    siteManager,
                    nameof(PlayerUISiteOfGraceManager.OpenLevelUpMenu)))
            {
                throw new InvalidOperationException(
                    "Site of Grace Level Up entry is not bound.");
            }
        }

        private static bool HasPersistentListener(
            Button button,
            UnityEngine.Object target,
            string methodName)
        {
            if (button == null)
            {
                return false;
            }

            for (int index = 0;
                index < button.onClick.GetPersistentEventCount();
                index++)
            {
                if (button.onClick.GetPersistentTarget(index) == target &&
                    button.onClick.GetPersistentMethodName(index) == methodName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object expected)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            if (serializedObject.FindProperty(propertyName)
                    .objectReferenceValue != expected)
            {
                throw new InvalidOperationException(
                    $"{target.name}.{propertyName} is not bound correctly.");
            }
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{propertyName} was not found.");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray<T>(
            UnityEngine.Object target,
            string propertyName,
            T[] values)
            where T : UnityEngine.Object
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform GetOrCreateRectTransform(
            Transform parent,
            string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                RectTransform existingRect =
                    existing.GetComponent<RectTransform>();
                return existingRect != null
                    ? existingRect
                    : existing.gameObject.AddComponent<RectTransform>();
            }

            GameObject gameObject = new GameObject(
                objectName,
                typeof(RectTransform));
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static void ConfigureCenteredRect(
            RectTransform rectTransform,
            Vector2 position,
            Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
        }

        private static void SetCenteredPosition(Button button, Vector2 position)
        {
            if (button?.transform is RectTransform rectTransform)
            {
                rectTransform.anchoredPosition = position;
            }
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static T RequireComponent<T>(GameObject gameObject)
            where T : Component
        {
            return gameObject.GetComponent<T>() ??
                throw new InvalidOperationException(
                    $"{gameObject.name} requires {typeof(T).Name}.");
        }

        private static void SetUILayerRecursively(GameObject root)
        {
            root.layer = LayerMask.NameToLayer("UI");
            foreach (Transform child in root.transform)
            {
                SetUILayerRecursively(child.gameObject);
            }
        }

        private sealed class LevelUpReferences
        {
            public TMP_Text CharacterLevel;
            public TMP_Text ProjectedCharacterLevel;
            public TMP_Text RunesHeld;
            public TMP_Text ProjectedRunesHeld;
            public TMP_Text RunesNeeded;
            public UICharacterAttributeSlider[] AttributeSliders;
            public TMP_Text[] CurrentAttributeTexts;
            public TMP_Text[] ProjectedAttributeTexts;
            public Button ConfirmButton;
        }
    }
}
