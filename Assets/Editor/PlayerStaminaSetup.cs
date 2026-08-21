using System;
using System.Reflection;
using UnityEditor;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    public static class PlayerStaminaSetup
    {
        private const string k_PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_PlayerUIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";

        private static readonly Color s_staminaBackgroundColor = new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color s_staminaFillColor = new Color(0.12f, 0.75f, 0.2f, 1f);

        [MenuItem("Tools/Elden/Configure Player Stamina")]
        public static void ConfigurePlayerStamina()
        {
            ConfigurePlayerPrefab();
            ConfigurePlayerUIManagerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidatePlayerStamina();
            Debug.Log("[PlayerStaminaSetup] Configured networked Stamina, Stats, and owner HUD.");
        }

        [MenuItem("Tools/Elden/Validate Player Stamina")]
        public static void ValidatePlayerStamina()
        {
            ValidatePlayerPrefab();
            ValidatePlayerUIManagerPrefab();
            ValidateStaminaRules();
            Debug.Log("[PlayerStaminaValidation] Stamina data, rules, consumption, and HUD are valid.");
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                PlayerStatsManager statsManager = GetOrAddComponent<PlayerStatsManager>(playerRoot);
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
            GameObject managerRoot = PrefabUtility.LoadPrefabContents(k_PlayerUIManagerPrefabPath);

            try
            {
                PlayerUIManager playerUIManager = GetOrAddComponent<PlayerUIManager>(managerRoot);
                RectTransform playerUI = GetOrCreateRectTransform(managerRoot.transform, "Player UI");
                ConfigureCanvas(playerUI.gameObject);

                RectTransform hud = GetOrCreateRectTransform(playerUI, "HUD");
                StretchToParent(hud);
                PlayerUIHUDManager playerHUDManager = GetOrAddComponent<PlayerUIHUDManager>(hud.gameObject);
                hud.gameObject.SetActive(false);

                RectTransform statusBars = GetOrCreateRectTransform(hud, "Status Bars");
                ConfigureStatusBars(statusBars);

                RectTransform staminaBar = GetOrCreateRectTransform(statusBars, "Stamina Bar");
                UIStatBar statBar = ConfigureStaminaBar(staminaBar);

                SetObjectReference(playerHUDManager, "m_staminaBar", statBar);
                SetObjectReference(playerUIManager, "m_playerUIHUDManager", playerHUDManager);
                SetUILayerRecursively(playerUI.gameObject);

                EditorUtility.SetDirty(playerUIManager);
                EditorUtility.SetDirty(playerHUDManager);
                EditorUtility.SetDirty(statBar);
                PrefabUtility.SaveAsPrefabAsset(managerRoot, k_PlayerUIManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(managerRoot);
            }
        }

        private static void ConfigureCanvas(GameObject playerUI)
        {
            RectTransform rectTransform = playerUI.GetComponent<RectTransform>();
            StretchToParent(rectTransform);

            Canvas canvas = GetOrAddComponent<Canvas>(playerUI);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler canvasScaler = GetOrAddComponent<CanvasScaler>(playerUI);
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 0.5f;

            GetOrAddComponent<GraphicRaycaster>(playerUI);
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(canvasScaler);
        }

        private static void ConfigureStatusBars(RectTransform statusBars)
        {
            statusBars.anchorMin = new Vector2(0f, 1f);
            statusBars.anchorMax = new Vector2(0f, 1f);
            statusBars.pivot = new Vector2(0f, 1f);
            statusBars.anchoredPosition = new Vector2(50f, -50f);
            statusBars.sizeDelta = new Vector2(400f, 200f);

            VerticalLayoutGroup layoutGroup = GetOrAddComponent<VerticalLayoutGroup>(statusBars.gameObject);
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.spacing = 10f;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
            EditorUtility.SetDirty(layoutGroup);
        }

        private static UIStatBar ConfigureStaminaBar(RectTransform staminaBar)
        {
            staminaBar.sizeDelta = new Vector2(300f, 24f);
            LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(staminaBar.gameObject);
            layoutElement.preferredWidth = 300f;
            layoutElement.preferredHeight = 24f;

            RectTransform background = GetOrCreateRectTransform(staminaBar, "Background");
            StretchToParent(background);
            Image backgroundImage = GetOrAddComponent<Image>(background.gameObject);
            backgroundImage.color = s_staminaBackgroundColor;
            backgroundImage.raycastTarget = false;

            RectTransform fillArea = GetOrCreateRectTransform(staminaBar, "Fill Area");
            StretchToParent(fillArea);
            fillArea.offsetMin = new Vector2(2f, 2f);
            fillArea.offsetMax = new Vector2(-2f, -2f);

            RectTransform fill = GetOrCreateRectTransform(fillArea, "Fill");
            StretchToParent(fill);
            Image fillImage = GetOrAddComponent<Image>(fill.gameObject);
            fillImage.color = s_staminaFillColor;
            fillImage.raycastTarget = false;

            Slider slider = GetOrAddComponent<Slider>(staminaBar.gameObject);
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 100f;
            slider.wholeNumbers = false;
            slider.fillRect = fill;
            slider.handleRect = null;
            slider.targetGraphic = null;

            UIStatBar statBar = GetOrAddComponent<UIStatBar>(staminaBar.gameObject);
            SetObjectReference(statBar, "m_slider", slider);

            EditorUtility.SetDirty(layoutElement);
            EditorUtility.SetDirty(backgroundImage);
            EditorUtility.SetDirty(fillImage);
            EditorUtility.SetDirty(slider);
            EditorUtility.SetDirty(statBar);
            return statBar;
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                PlayerManager playerManager = playerRoot.GetComponent<PlayerManager>();
                PlayerStatsManager statsManager = playerRoot.GetComponent<PlayerStatsManager>();
                PlayerLocomotionManager locomotionManager =
                    playerRoot.GetComponent<PlayerLocomotionManager>();
                CharacterNetworkManager networkManager =
                    playerRoot.GetComponent<CharacterNetworkManager>();
                if (playerManager == null ||
                    statsManager == null ||
                    locomotionManager == null ||
                    networkManager == null ||
                    playerRoot.GetComponents<CharacterStatsManager>().Length != 1)
                {
                    throw new InvalidOperationException(
                        "The Player prefab must contain one PlayerStatsManager and the existing managers.");
                }

                ValidateOwnerVariable(networkManager.Endurance, "Endurance");
                ValidateOwnerVariable(networkManager.CurrentStamina, "CurrentStamina");
                ValidateOwnerVariable(networkManager.MaxStamina, "MaxStamina");
                if (networkManager.Endurance.Value != 10 ||
                    !Mathf.Approximately(
                        statsManager.CalculateStaminaBasedOnEnduranceLevel(10),
                        100f))
                {
                    throw new InvalidOperationException(
                        "Endurance 10 must produce a maximum Stamina value of 100.");
                }

                ValidatePositiveSerializedFloat(statsManager, "m_staminaRegenerationDelay");
                ValidatePositiveSerializedFloat(statsManager, "m_staminaRegenerationTickInterval");
                ValidatePositiveSerializedFloat(statsManager, "m_staminaRegenerationAmount");
                ValidatePositiveSerializedFloat(locomotionManager, "m_sprintingStaminaCost");
                ValidatePositiveSerializedFloat(locomotionManager, "m_dodgeStaminaCost");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidatePlayerUIManagerPrefab()
        {
            GameObject managerRoot = PrefabUtility.LoadPrefabContents(k_PlayerUIManagerPrefabPath);

            try
            {
                PlayerUIManager playerUIManager = managerRoot.GetComponent<PlayerUIManager>();
                Transform playerUI = managerRoot.transform.Find("Player UI");
                Transform hud = playerUI?.Find("HUD");
                Transform statusBars = hud?.Find("Status Bars");
                Transform staminaBar = statusBars?.Find("Stamina Bar");
                PlayerUIHUDManager playerHUDManager = hud?.GetComponent<PlayerUIHUDManager>();
                UIStatBar statBar = staminaBar?.GetComponent<UIStatBar>();
                Slider slider = staminaBar?.GetComponent<Slider>();
                if (playerUIManager == null ||
                    playerUI == null ||
                    playerUI.GetComponent<Canvas>() == null ||
                    playerUI.GetComponent<CanvasScaler>() == null ||
                    playerHUDManager == null ||
                    hud.gameObject.activeSelf ||
                    statusBars?.GetComponent<VerticalLayoutGroup>() == null ||
                    statBar == null ||
                    slider == null)
                {
                    throw new InvalidOperationException(
                        "The Player UI prefab is missing the required HUD or Stamina Bar hierarchy.");
                }

                Canvas canvas = playerUI.GetComponent<Canvas>();
                CanvasScaler canvasScaler = playerUI.GetComponent<CanvasScaler>();
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay ||
                    canvasScaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                    canvasScaler.referenceResolution != new Vector2(1920f, 1080f) ||
                    slider.transition != Selectable.Transition.None ||
                    slider.interactable ||
                    slider.handleRect != null ||
                    slider.fillRect == null)
                {
                    throw new InvalidOperationException(
                        "The Stamina Canvas or Slider settings do not match the EP8 HUD contract.");
                }

                ValidateObjectReference(playerUIManager, "m_playerUIHUDManager", playerHUDManager);
                ValidateObjectReference(playerHUDManager, "m_staminaBar", statBar);
                ValidateObjectReference(statBar, "m_slider", slider);
                ValidateStaminaBarColors(staminaBar);
                ValidateEventDrivenUI();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(managerRoot);
            }
        }

        private static void ValidateStaminaRules()
        {
            MethodInfo shouldResetTimer = typeof(CharacterStatsManager).GetMethod(
                "ShouldResetStaminaRegenerationTimer",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo calculateStaminaAfterConsumption = typeof(CharacterStatsManager).GetMethod(
                "CalculateStaminaAfterConsumption",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo calculateRegenerationTicks = typeof(CharacterStatsManager).GetMethod(
                "CalculateElapsedStaminaRegenerationTicks",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (shouldResetTimer == null ||
                calculateStaminaAfterConsumption == null ||
                calculateRegenerationTicks == null ||
                !IsInvocationTrue(shouldResetTimer, 100f, 75f) ||
                IsInvocationTrue(shouldResetTimer, 50f, 52f) ||
                IsInvocationTrue(shouldResetTimer, 50f, 50f) ||
                !Mathf.Approximately(
                    InvokeFloat(calculateStaminaAfterConsumption, 100f, 100f, 25f),
                    75f) ||
                !Mathf.Approximately(
                    InvokeFloat(calculateStaminaAfterConsumption, 10f, 100f, 25f),
                    0f) ||
                InvokeInt(calculateRegenerationTicks, 0.05f, 0.1f) != 0 ||
                InvokeInt(calculateRegenerationTicks, 0.35f, 0.1f) != 3)
            {
                throw new InvalidOperationException(
                    "Stamina consumption must clamp to zero and exclusively reset the recovery delay.");
            }
        }

        private static void ValidateStaminaBarColors(Transform staminaBar)
        {
            Image background = staminaBar.Find("Background")?.GetComponent<Image>();
            Image fill = staminaBar.Find("Fill Area/Fill")?.GetComponent<Image>();
            if (background == null ||
                fill == null ||
                !AreColorsApproximatelyEqual(background.color, s_staminaBackgroundColor) ||
                !AreColorsApproximatelyEqual(fill.color, s_staminaFillColor))
            {
                throw new InvalidOperationException(
                    "The Stamina Bar needs the configured black background and green fill.");
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
                    "Stamina UI must be driven by NetworkVariable events instead of Update polling.");
            }
        }

        private static void ValidateOwnerVariable(NetworkVariableBase networkVariable, string variableName)
        {
            if (networkVariable.ReadPerm != NetworkVariableReadPermission.Everyone ||
                networkVariable.WritePerm != NetworkVariableWritePermission.Owner)
            {
                throw new InvalidOperationException(
                    $"{variableName} must be readable by everyone and writable only by its Owner.");
            }
        }

        private static void ValidatePositiveSerializedFloat(Component component, string propertyName)
        {
            SerializedProperty property = new SerializedObject(component).FindProperty(propertyName);
            if (property == null || property.floatValue <= 0f)
            {
                throw new InvalidOperationException(
                    $"{component.GetType().Name}.{propertyName} must be configured above zero.");
            }
        }

        private static void SetObjectReference(
            Component component,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Could not find {component.GetType().Name}.{propertyName}.");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateObjectReference(
            Component component,
            string propertyName,
            UnityEngine.Object expectedValue)
        {
            SerializedProperty property = new SerializedObject(component).FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != expectedValue)
            {
                throw new InvalidOperationException(
                    $"{component.GetType().Name}.{propertyName} is not configured correctly.");
            }
        }

        private static RectTransform GetOrCreateRectTransform(Transform parent, string objectName)
        {
            Transform existingChild = parent.Find(objectName);
            if (existingChild != null)
            {
                RectTransform existingRectTransform = existingChild.GetComponent<RectTransform>();
                if (existingRectTransform == null)
                {
                    throw new InvalidOperationException(
                        $"Existing UI object {objectName} does not use a RectTransform.");
                }

                return existingRectTransform;
            }

            GameObject child = new GameObject(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static void SetUILayerRecursively(GameObject rootObject)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0)
            {
                return;
            }

            foreach (Transform child in rootObject.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = uiLayer;
            }
        }

        private static bool IsInvocationTrue(
            MethodInfo method,
            float previousValue,
            float currentValue)
        {
            object result = method.Invoke(null, new object[] { previousValue, currentValue });
            return result is bool booleanResult && booleanResult;
        }

        private static float InvokeFloat(
            MethodInfo method,
            float currentValue,
            float maximumValue,
            float cost)
        {
            object result = method.Invoke(
                null,
                new object[] { currentValue, maximumValue, cost });
            return result is float floatResult ? floatResult : float.NaN;
        }

        private static int InvokeInt(
            MethodInfo method,
            float timer,
            float interval)
        {
            object result = method.Invoke(null, new object[] { timer, interval });
            return result is int integerResult ? integerResult : -1;
        }

        private static bool AreColorsApproximatelyEqual(Color first, Color second)
        {
            return Mathf.Approximately(first.r, second.r) &&
                Mathf.Approximately(first.g, second.g) &&
                Mathf.Approximately(first.b, second.b) &&
                Mathf.Approximately(first.a, second.a);
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}
