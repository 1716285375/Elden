using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    public static class PlayerHealthSetup
    {
        private const string k_PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_PlayerUIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const float k_DefaultMaximumHealth = 150f;
        private const float k_DefaultMaximumStamina = 100f;
        private const float k_StatBarHeight = 24f;
        private const float k_WidthScaleMultiplier = 2f;

        private static readonly Color s_statBarBackgroundColor =
            new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color s_healthFillColor =
            new Color(0.78f, 0.08f, 0.08f, 1f);

        [MenuItem("Tools/Elden/Configure Player Health")]
        public static void ConfigurePlayerHealth()
        {
            ConfigurePlayerPrefab();
            ConfigurePlayerUIManagerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidatePlayerHealth();
            Debug.Log(
                "[PlayerHealthSetup] Configured networked Health, save data, " +
                "and the dynamic owner HUD.");
        }

        [MenuItem("Tools/Elden/Validate Player Health")]
        public static void ValidatePlayerHealth()
        {
            ValidatePlayerPrefab();
            ValidatePlayerUIManagerPrefab();
            ValidateSaveDataDefaults();
            ValidateEventDrivenUI();
            Debug.Log(
                "[PlayerHealthValidation] Health rules, network permissions, save defaults, " +
                "and dynamic HUD layout are valid.");
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                CharacterNetworkManager networkManager =
                    GetRequiredComponent<CharacterNetworkManager>(playerRoot);
                PlayerStatsManager statsManager =
                    GetRequiredComponent<PlayerStatsManager>(playerRoot);
                EditorUtility.SetDirty(networkManager);
                EditorUtility.SetDirty(statsManager);
                PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigurePlayerUIManagerPrefab()
        {
            GameObject managerRoot = PrefabUtility.LoadPrefabContents(
                k_PlayerUIManagerPrefabPath);

            try
            {
                Transform playerUI = GetRequiredChild(managerRoot.transform, "Player UI");
                Transform hud = GetRequiredChild(playerUI, "HUD");
                RectTransform statusBars = GetRequiredChild(hud, "Status Bars")
                    as RectTransform;
                RectTransform staminaBar = GetRequiredChild(statusBars, "Stamina Bar")
                    as RectTransform;
                if (statusBars == null || staminaBar == null)
                {
                    throw new InvalidOperationException(
                        "The HUD status bars must use RectTransform components.");
                }

                VerticalLayoutGroup layoutGroup =
                    GetOrAddComponent<VerticalLayoutGroup>(statusBars.gameObject);
                layoutGroup.childControlWidth = false;
                layoutGroup.childControlHeight = false;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childForceExpandHeight = false;

                RectTransform healthBar = GetOrCreateRectTransform(statusBars, "Health Bar");
                CopyRectLayout(staminaBar, healthBar);
                UIStatBar healthStatBar = ConfigureHealthBar(healthBar);
                healthBar.SetSiblingIndex(staminaBar.GetSiblingIndex());

                UIStatBar staminaStatBar =
                    GetRequiredComponent<UIStatBar>(staminaBar.gameObject);
                ConfigureStatBarScaling(
                    staminaStatBar,
                    staminaBar,
                    k_DefaultMaximumStamina);

                PlayerUIHUDManager hudManager =
                    GetRequiredComponent<PlayerUIHUDManager>(hud.gameObject);
                SetObjectReference(hudManager, "m_healthBar", healthStatBar);
                SetObjectReference(hudManager, "m_staminaBar", staminaStatBar);
                SetUILayerRecursively(healthBar.gameObject, statusBars.gameObject.layer);

                EditorUtility.SetDirty(layoutGroup);
                EditorUtility.SetDirty(hudManager);
                PrefabUtility.SaveAsPrefabAsset(managerRoot, k_PlayerUIManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(managerRoot);
            }
        }

        private static UIStatBar ConfigureHealthBar(RectTransform healthBar)
        {
            ConfigureBarSize(healthBar, k_DefaultMaximumHealth);

            RectTransform background = GetOrCreateRectTransform(healthBar, "Background");
            StretchToParent(background);
            Image backgroundImage = GetOrAddComponent<Image>(background.gameObject);
            backgroundImage.color = s_statBarBackgroundColor;
            backgroundImage.raycastTarget = false;

            RectTransform fillArea = GetOrCreateRectTransform(healthBar, "Fill Area");
            StretchToParent(fillArea);
            fillArea.offsetMin = new Vector2(2f, 2f);
            fillArea.offsetMax = new Vector2(-2f, -2f);

            RectTransform fill = GetOrCreateRectTransform(fillArea, "Fill");
            StretchToParent(fill);
            Image fillImage = GetOrAddComponent<Image>(fill.gameObject);
            fillImage.color = s_healthFillColor;
            fillImage.raycastTarget = false;

            Slider slider = GetOrAddComponent<Slider>(healthBar.gameObject);
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill;
            slider.handleRect = null;
            slider.targetGraphic = null;
            slider.minValue = 0f;
            slider.maxValue = k_DefaultMaximumHealth;
            slider.value = k_DefaultMaximumHealth;
            slider.wholeNumbers = false;

            UIStatBar statBar = GetOrAddComponent<UIStatBar>(healthBar.gameObject);
            SetObjectReference(statBar, "m_slider", slider);
            ConfigureStatBarScaling(statBar, healthBar, k_DefaultMaximumHealth);

            EditorUtility.SetDirty(backgroundImage);
            EditorUtility.SetDirty(fillImage);
            EditorUtility.SetDirty(slider);
            EditorUtility.SetDirty(statBar);
            return statBar;
        }

        private static void ConfigureStatBarScaling(
            UIStatBar statBar,
            RectTransform rectTransform,
            float defaultMaximumValue)
        {
            ConfigureBarSize(rectTransform, defaultMaximumValue);
            SerializedObject serializedStatBar = new SerializedObject(statBar);
            GetRequiredProperty(serializedStatBar, "m_rectTransform").objectReferenceValue =
                rectTransform;
            GetRequiredProperty(
                serializedStatBar,
                "m_shouldScaleBarLengthWithStats").boolValue = true;
            GetRequiredProperty(serializedStatBar, "m_widthScaleMultiplier").floatValue =
                k_WidthScaleMultiplier;
            serializedStatBar.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(statBar);
        }

        private static void ConfigureBarSize(
            RectTransform rectTransform,
            float defaultMaximumValue)
        {
            float width = defaultMaximumValue * k_WidthScaleMultiplier;
            rectTransform.sizeDelta = new Vector2(width, k_StatBarHeight);
            LayoutElement layoutElement =
                GetOrAddComponent<LayoutElement>(rectTransform.gameObject);
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = k_StatBarHeight;
            EditorUtility.SetDirty(layoutElement);
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                CharacterNetworkManager networkManager =
                    GetRequiredComponent<CharacterNetworkManager>(playerRoot);
                PlayerStatsManager statsManager =
                    GetRequiredComponent<PlayerStatsManager>(playerRoot);

                ValidateOwnerVariable(networkManager.Vitality, "Vitality");
                ValidateOwnerVariable(networkManager.Endurance, "Endurance");
                ValidateOwnerVariable(networkManager.CurrentHealth, "CurrentHealth");
                ValidateOwnerVariable(networkManager.MaxHealth, "MaxHealth");
                ValidateOwnerVariable(networkManager.CurrentStamina, "CurrentStamina");
                ValidateOwnerVariable(networkManager.MaxStamina, "MaxStamina");
                if (networkManager.Vitality.Value != 10 ||
                    networkManager.Endurance.Value != 10 ||
                    !Mathf.Approximately(
                        statsManager.CalculateHealthBasedOnVitalityLevel(10),
                        k_DefaultMaximumHealth) ||
                    !Mathf.Approximately(
                        statsManager.CalculateStaminaBasedOnEnduranceLevel(10),
                        k_DefaultMaximumStamina))
                {
                    throw new InvalidOperationException(
                        "Default Vitality and Endurance must calculate valid resource maxima.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidatePlayerUIManagerPrefab()
        {
            GameObject managerRoot = PrefabUtility.LoadPrefabContents(
                k_PlayerUIManagerPrefabPath);

            try
            {
                Transform hud = GetRequiredChild(
                    GetRequiredChild(managerRoot.transform, "Player UI"),
                    "HUD");
                RectTransform statusBars = GetRequiredChild(hud, "Status Bars")
                    as RectTransform;
                RectTransform healthBar = GetRequiredChild(statusBars, "Health Bar")
                    as RectTransform;
                RectTransform staminaBar = GetRequiredChild(statusBars, "Stamina Bar")
                    as RectTransform;
                if (statusBars == null || healthBar == null || staminaBar == null)
                {
                    throw new InvalidOperationException(
                        "Health and Stamina bars must use RectTransform components.");
                }

                VerticalLayoutGroup layoutGroup =
                    GetRequiredComponent<VerticalLayoutGroup>(statusBars.gameObject);
                PlayerUIHUDManager hudManager =
                    GetRequiredComponent<PlayerUIHUDManager>(hud.gameObject);
                UIStatBar healthStatBar =
                    GetRequiredComponent<UIStatBar>(healthBar.gameObject);
                UIStatBar staminaStatBar =
                    GetRequiredComponent<UIStatBar>(staminaBar.gameObject);
                if (layoutGroup.childControlWidth ||
                    layoutGroup.childControlHeight ||
                    layoutGroup.childForceExpandWidth ||
                    layoutGroup.childForceExpandHeight ||
                    healthBar.GetSiblingIndex() >= staminaBar.GetSiblingIndex())
                {
                    throw new InvalidOperationException(
                        "Health must appear above Stamina without layout expansion.");
                }

                ValidateObjectReference(hudManager, "m_healthBar", healthStatBar);
                ValidateObjectReference(hudManager, "m_staminaBar", staminaStatBar);
                ValidateStatBar(healthStatBar, healthBar, k_DefaultMaximumHealth);
                ValidateStatBar(staminaStatBar, staminaBar, k_DefaultMaximumStamina);
                ValidateHealthBarColors(healthBar);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(managerRoot);
            }
        }

        private static void ValidateStatBar(
            UIStatBar statBar,
            RectTransform rectTransform,
            float defaultMaximumValue)
        {
            Slider slider = GetRequiredComponent<Slider>(statBar.gameObject);
            SerializedObject serializedStatBar = new SerializedObject(statBar);
            if (!GetRequiredProperty(
                    serializedStatBar,
                    "m_shouldScaleBarLengthWithStats").boolValue ||
                !Mathf.Approximately(
                    GetRequiredProperty(
                        serializedStatBar,
                        "m_widthScaleMultiplier").floatValue,
                    k_WidthScaleMultiplier) ||
                GetRequiredProperty(
                    serializedStatBar,
                    "m_rectTransform").objectReferenceValue != rectTransform ||
                slider.fillRect == null ||
                slider.fillRect.anchorMax.x < 0.99f ||
                slider.handleRect != null ||
                slider.interactable)
            {
                throw new InvalidOperationException(
                    $"{statBar.name} is not configured as a dynamic read-only stat bar.");
            }

            float originalHeight = rectTransform.sizeDelta.y;
            statBar.SetMaxStat(defaultMaximumValue + 25f);
            if (!Mathf.Approximately(
                    rectTransform.sizeDelta.x,
                    (defaultMaximumValue + 25f) * k_WidthScaleMultiplier) ||
                !Mathf.Approximately(rectTransform.sizeDelta.y, originalHeight))
            {
                throw new InvalidOperationException(
                    "Dynamic stat bars must scale width without changing height.");
            }
        }

        private static void ValidateHealthBarColors(RectTransform healthBar)
        {
            Image background = healthBar.Find("Background")?.GetComponent<Image>();
            Image fill = healthBar.Find("Fill Area/Fill")?.GetComponent<Image>();
            if (background == null ||
                fill == null ||
                !AreColorsApproximatelyEqual(
                    background.color,
                    s_statBarBackgroundColor) ||
                !AreColorsApproximatelyEqual(fill.color, s_healthFillColor))
            {
                throw new InvalidOperationException(
                    "The Health Bar needs the configured black background and red fill.");
            }
        }

        private static void ValidateSaveDataDefaults()
        {
            CharacterSaveData characterData = new CharacterSaveData();
            if (characterData.Vitality != 10 ||
                characterData.Endurance != 10 ||
                !Mathf.Approximately(
                    characterData.CurrentHealth,
                    k_DefaultMaximumHealth) ||
                !Mathf.Approximately(
                    characterData.CurrentStamina,
                    k_DefaultMaximumStamina))
            {
                throw new InvalidOperationException(
                    "New character save data must contain non-zero starting stats.");
            }

            ValidateLegacySaveMigration();
        }

        private static void ValidateLegacySaveMigration()
        {
            string testDirectory = Path.Combine(
                Path.GetTempPath(),
                "EldenHealthValidation",
                Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(testDirectory);
                File.WriteAllText(
                    Path.Combine(testDirectory, "CharacterSlot01.json"),
                    "{\"m_characterName\":\"LegacyKnight\",\"m_sceneIndex\":1}");
                SaveFileDataWriter writer = new SaveFileDataWriter(
                    testDirectory,
                    "CharacterSlot01");
                CharacterSaveData migratedData = writer.LoadSaveFile();
                if (migratedData == null ||
                    migratedData.Vitality != 10 ||
                    migratedData.Endurance != 10 ||
                    !Mathf.Approximately(
                        migratedData.CurrentHealth,
                        k_DefaultMaximumHealth) ||
                    !Mathf.Approximately(
                        migratedData.CurrentStamina,
                        k_DefaultMaximumStamina))
                {
                    throw new InvalidOperationException(
                        "Legacy saves must receive the EP16 starting stat defaults.");
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

        private static void ValidateEventDrivenUI()
        {
            BindingFlags declaredInstanceMethods =
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            if (typeof(PlayerUIHUDManager).GetMethod("Update", declaredInstanceMethods) != null ||
                typeof(UIStatBar).GetMethod("Update", declaredInstanceMethods) != null)
            {
                throw new InvalidOperationException(
                    "Health UI must be driven by NetworkVariable events without Update polling.");
            }
        }

        private static void ValidateOwnerVariable(
            NetworkVariableBase networkVariable,
            string variableName)
        {
            if (networkVariable.ReadPerm != NetworkVariableReadPermission.Everyone ||
                networkVariable.WritePerm != NetworkVariableWritePermission.Owner)
            {
                throw new InvalidOperationException(
                    $"{variableName} must be readable by everyone and writable only by its Owner.");
            }
        }

        private static RectTransform GetOrCreateRectTransform(
            Transform parent,
            string objectName)
        {
            Transform existingTransform = parent.Find(objectName);
            if (existingTransform != null)
            {
                return existingTransform as RectTransform ??
                    throw new InvalidOperationException(
                        $"{objectName} must use a RectTransform.");
            }

            GameObject childObject = new GameObject(objectName, typeof(RectTransform));
            RectTransform rectTransform = childObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static Transform GetRequiredChild(Transform parent, string childName)
        {
            Transform child = parent?.Find(childName);
            return child != null
                ? child
                : throw new InvalidOperationException(
                    $"Could not find {childName} below {parent?.name ?? "null"}.");
        }

        private static T GetRequiredComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{gameObject.name} is missing {typeof(T).Name}.");
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void CopyRectLayout(
            RectTransform source,
            RectTransform destination)
        {
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.anchoredPosition = source.anchoredPosition;
            destination.localScale = Vector3.one;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SetUILayerRecursively(GameObject gameObject, int uiLayer)
        {
            gameObject.layer = uiLayer;
            foreach (Transform child in gameObject.transform)
            {
                SetUILayerRecursively(child.gameObject, uiLayer);
            }
        }

        private static void SetObjectReference(
            Component component,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateObjectReference(
            Component component,
            string propertyName,
            UnityEngine.Object expectedValue)
        {
            SerializedProperty property =
                GetRequiredProperty(new SerializedObject(component), propertyName);
            if (property.objectReferenceValue != expectedValue)
            {
                throw new InvalidOperationException(
                    $"{component.GetType().Name}.{propertyName} is not configured.");
            }
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"Could not find {serializedObject.targetObject.GetType().Name}." +
                    propertyName);
        }

        private static bool AreColorsApproximatelyEqual(Color first, Color second)
        {
            return Mathf.Approximately(first.r, second.r) &&
                Mathf.Approximately(first.g, second.g) &&
                Mathf.Approximately(first.b, second.b) &&
                Mathf.Approximately(first.a, second.a);
        }
    }
}
