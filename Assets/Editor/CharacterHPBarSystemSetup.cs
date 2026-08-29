using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP51 world-space character Health bars.</summary>
    public static class CharacterHPBarSystemSetup
    {
        private const string k_PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_AICharacterPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";
        private const string k_PlayerUIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_CharacterUIName = "Character UI";
        private const string k_HPBarName = "HP Bar";

        private static readonly Color s_backgroundColor =
            new Color(0.015f, 0.01f, 0.008f, 0.9f);
        private static readonly Color s_healthColor =
            new Color(0.68f, 0.035f, 0.025f, 1f);

        [MenuItem("Tools/Elden/Configure Character HP Bar System")]
        public static void ConfigureCharacterHPBarSystem()
        {
            TMP_FontAsset font = LoadUIFont();
            ConfigureCharacterPrefab(k_PlayerPrefabPath, 2.15f, true, font);
            ConfigureCharacterPrefab(k_AICharacterPrefabPath, 2.2f, true, font);
            ConfigureCharacterPrefab(k_BossPrefabPath, 2.6f, false, font);
            AssetDatabase.SaveAssets();
            ValidateCharacterHPBarSystem();
            Debug.Log(
                "[CharacterHPBarSystemSetup] Configured event-driven world-space " +
                "Health bars, accumulated change text, hiding, and billboarding.");
        }

        [MenuItem("Tools/Elden/Validate Character HP Bar System")]
        public static void ValidateCharacterHPBarSystem()
        {
            ValidateHPBarInheritance();
            ValidateCharacterPrefab(k_PlayerPrefabPath, true, true);
            ValidateCharacterPrefab(k_AICharacterPrefabPath, true, true);
            ValidateCharacterPrefab(k_BossPrefabPath, false, true);
            ValidateRuntimeArchitecture();
            Debug.Log(
                "[CharacterHPBarSystemValidation] Player and AI subscriptions, Boss opt-out, " +
                "world Canvas, delayed hiding, change text, and billboard flow are valid.");
        }

        private static void ConfigureCharacterPrefab(
            string prefabPath,
            float verticalOffset,
            bool hasFloatingHPBar,
            TMP_FontAsset font)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                CharacterManager character =
                    root.GetComponent<CharacterManager>() ??
                    throw new InvalidOperationException(
                        $"{prefabPath} is missing CharacterManager.");
                SerializedObject serializedCharacter = new SerializedObject(character);
                SerializedProperty hasBar = serializedCharacter.FindProperty(
                    "m_hasFloatingHPBar") ??
                    throw new InvalidOperationException(
                        "CharacterManager is missing m_hasFloatingHPBar.");
                hasBar.boolValue = hasFloatingHPBar;
                serializedCharacter.ApplyModifiedPropertiesWithoutUndo();

                RectTransform characterUI = GetOrCreateRectTransform(
                    root.transform,
                    k_CharacterUIName);
                ConfigureWorldCanvas(characterUI, verticalOffset);
                CharacterHPBar hpBar = ConfigureHPBar(characterUI, font);
                CharacterUIManager uiManager =
                    GetOrAddComponent<CharacterUIManager>(characterUI.gameObject);
                SerializedObject serializedUI = new SerializedObject(uiManager);
                SetObjectReference(serializedUI, "m_character", character);
                SetObjectReference(
                    serializedUI,
                    "m_characterUICanvas",
                    characterUI.GetComponent<Canvas>());
                SetObjectReference(serializedUI, "m_characterHPBar", hpBar);
                serializedUI.ApplyModifiedPropertiesWithoutUndo();
                SetLayerRecursively(
                    characterUI.gameObject,
                    LayerMask.NameToLayer("UI"));
                hpBar.gameObject.SetActive(false);

                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                {
                    throw new InvalidOperationException(
                        $"Could not save character prefab {prefabPath}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureWorldCanvas(
            RectTransform characterUI,
            float verticalOffset)
        {
            characterUI.localPosition = new Vector3(0f, verticalOffset, 0f);
            characterUI.localRotation = Quaternion.identity;
            characterUI.localScale = Vector3.one * 0.01f;
            characterUI.sizeDelta = new Vector2(240f, 55f);
            Canvas canvas = GetOrAddComponent<Canvas>(characterUI.gameObject);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            CanvasScaler scaler =
                GetOrAddComponent<CanvasScaler>(characterUI.gameObject);
            scaler.dynamicPixelsPerUnit = 100f;
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(scaler);
        }

        private static CharacterHPBar ConfigureHPBar(
            RectTransform characterUI,
            TMP_FontAsset font)
        {
            RectTransform hpBarRect = GetOrCreateRectTransform(
                characterUI,
                k_HPBarName);
            hpBarRect.anchorMin = new Vector2(0.5f, 0.5f);
            hpBarRect.anchorMax = new Vector2(0.5f, 0.5f);
            hpBarRect.pivot = new Vector2(0.5f, 0.5f);
            hpBarRect.anchoredPosition = new Vector2(0f, -12f);
            hpBarRect.sizeDelta = new Vector2(220f, 18f);

            RectTransform background = GetOrCreateRectTransform(
                hpBarRect,
                "Background");
            StretchToParent(background);
            Image backgroundImage = GetOrAddComponent<Image>(background.gameObject);
            backgroundImage.color = s_backgroundColor;
            backgroundImage.raycastTarget = false;

            RectTransform fillArea = GetOrCreateRectTransform(
                hpBarRect,
                "Fill Area");
            StretchToParent(fillArea);
            fillArea.offsetMin = new Vector2(2f, 2f);
            fillArea.offsetMax = new Vector2(-2f, -2f);
            RectTransform fill = GetOrCreateRectTransform(fillArea, "Fill");
            StretchToParent(fill);
            Image fillImage = GetOrAddComponent<Image>(fill.gameObject);
            fillImage.color = s_healthColor;
            fillImage.raycastTarget = false;

            Slider slider = GetOrAddComponent<Slider>(hpBarRect.gameObject);
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill;
            slider.handleRect = null;
            slider.targetGraphic = null;
            slider.minValue = 0f;
            slider.maxValue = 150f;
            slider.value = 150f;

            RectTransform textRect = GetOrCreateRectTransform(
                hpBarRect,
                "Health Change");
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0f, 30f);
            textRect.sizeDelta = new Vector2(180f, 30f);
            TextMeshProUGUI changeText =
                GetOrAddComponent<TextMeshProUGUI>(textRect.gameObject);
            changeText.font = font;
            changeText.text = "-0";
            changeText.fontSize = 24f;
            changeText.fontStyle = FontStyles.Bold;
            changeText.alignment = TextAlignmentOptions.Center;
            changeText.color = Color.white;
            changeText.raycastTarget = false;

            CharacterHPBar hpBar =
                GetOrAddComponent<CharacterHPBar>(hpBarRect.gameObject);
            SerializedObject serializedBar = new SerializedObject(hpBar);
            SetObjectReference(serializedBar, "m_slider", slider);
            SetObjectReference(serializedBar, "m_rectTransform", hpBarRect);
            SetBoolean(serializedBar, "m_shouldScaleBarLengthWithStats", false);
            SetFloat(serializedBar, "m_defaultTimeBeforeBarHides", 3f);
            SetObjectReference(serializedBar, "m_healthChangeText", changeText);
            serializedBar.ApplyModifiedPropertiesWithoutUndo();
            return hpBar;
        }

        private static TMP_FontAsset LoadUIFont()
        {
            GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_PlayerUIManagerPrefabPath);
            TMP_FontAsset font = uiPrefab != null
                ? uiPrefab.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .Select(text => text.font)
                    .FirstOrDefault(candidate => candidate != null)
                : null;
            return font ??
                throw new InvalidOperationException(
                    "A UI font is required for floating Health change text.");
        }

        private static void ValidateHPBarInheritance()
        {
            if (!typeof(CharacterHPBar).IsSubclassOf(typeof(UIStatBar)))
            {
                throw new InvalidOperationException(
                    "CharacterHPBar must inherit UIStatBar.");
            }
        }

        private static void ValidateCharacterPrefab(
            string prefabPath,
            bool expectedFloatingHPBar,
            bool expectUI)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) ??
                throw new InvalidOperationException(
                    $"Required character prefab is missing: {prefabPath}");
            CharacterManager character = prefab.GetComponent<CharacterManager>() ??
                throw new InvalidOperationException(
                    $"{prefabPath} is missing CharacterManager.");
            if (character.HasFloatingHPBar != expectedFloatingHPBar)
            {
                throw new InvalidOperationException(
                    $"{prefabPath} has an invalid floating-HP-bar policy.");
            }

            CharacterUIManager uiManager =
                prefab.GetComponentInChildren<CharacterUIManager>(true);
            CharacterHPBar hpBar =
                prefab.GetComponentInChildren<CharacterHPBar>(true);
            Canvas canvas = prefab.GetComponentsInChildren<Canvas>(true)
                .FirstOrDefault(candidate => candidate.name == k_CharacterUIName);
            if (expectUI &&
                (uiManager == null ||
                    hpBar == null ||
                    canvas == null ||
                    canvas.renderMode != RenderMode.WorldSpace))
            {
                throw new InvalidOperationException(
                    $"{prefabPath} is missing its world-space character UI.");
            }

            if (hpBar != null)
            {
                SerializedObject serializedBar = new SerializedObject(hpBar);
                SerializedProperty hideDelay = serializedBar.FindProperty(
                    "m_defaultTimeBeforeBarHides");
                SerializedProperty changeText = serializedBar.FindProperty(
                    "m_healthChangeText");
                if (hideDelay == null ||
                    !Mathf.Approximately(hideDelay.floatValue, 3f) ||
                    changeText?.objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} has invalid HP bar feedback settings.");
                }
            }
        }

        private static void ValidateRuntimeArchitecture()
        {
            BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;
            BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            if (typeof(CharacterUIManager).GetMethod(
                    "BindNetworkHealth",
                    publicInstance) == null ||
                typeof(CharacterUIManager).GetMethod(
                    "UnbindNetworkHealth",
                    publicInstance) == null ||
                typeof(CharacterUIManager).GetMethod(
                    "OnHPChanged",
                    publicInstance) == null ||
                typeof(CharacterUIManager).GetMethod(
                    "OnEnable",
                    privateInstance) == null ||
                typeof(CharacterUIManager).GetMethod(
                    "LateUpdate",
                    privateInstance) == null)
            {
                throw new InvalidOperationException(
                    "Character UI requires network binding and billboard lifecycle methods.");
            }
        }

        private static RectTransform GetOrCreateRectTransform(
            Transform parent,
            string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing as RectTransform ??
                    throw new InvalidOperationException(
                        $"{childName} must use a RectTransform.");
            }

            GameObject child = new GameObject(childName, typeof(RectTransform));
            RectTransform rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            if (layer >= 0)
            {
                gameObject.layer = layer;
            }

            foreach (Transform child in gameObject.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue = value;
        }

        private static void SetBoolean(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            GetRequiredProperty(serializedObject, propertyName).boolValue = value;
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} is missing " +
                    $"serialized property {propertyName}.");
        }
    }
}
