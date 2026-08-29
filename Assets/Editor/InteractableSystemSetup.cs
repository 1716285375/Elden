using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP52 interaction framework.</summary>
    public static class InteractableSystemSetup
    {
        private const string k_PlayerControlsPath = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";
        private const string k_InteractableLayerName = "Interactable";
        private const string k_PlayerLayerName = "Player";
        private const string k_PopupOrganizerName = "Popup Organizer";
        private const string k_MessagePopupName = "Player Message Popup";

        private static readonly Color s_backgroundColor =
            new Color(0.015f, 0.01f, 0.008f, 0.82f);
        private static readonly Color s_borderColor =
            new Color(0.36f, 0.3f, 0.19f, 0.9f);
        private static readonly Color s_textColor =
            new Color(0.84f, 0.78f, 0.64f, 1f);

        [MenuItem("Tools/Elden/Configure Interactable System")]
        public static void ConfigureInteractableSystem()
        {
            ConfigureInteractableLayer();
            ConfigurePlayerPrefab();
            ConfigurePlayerUIPrefab();
            AssetDatabase.SaveAssets();
            ValidateInteractableSystem();
            Debug.Log(
                "[InteractableSystemSetup] Configured owner-side prompts, interaction " +
                "input, trigger collection, and Host eligibility.");
        }

        [MenuItem("Tools/Elden/Validate Interactable System")]
        public static void ValidateInteractableSystem()
        {
            ValidateInputActions();
            ValidatePlayerPrefab();
            ValidatePlayerUIPrefab();
            ValidateLayers();
            ValidateRuntimeArchitecture();
            Debug.Log(
                "[InteractableSystemValidation] Input, Player interaction list, popup, " +
                "trigger physics, and Host restrictions are valid.");
        }

        private static void ConfigureInteractableLayer()
        {
            int interactableLayer = EnsureLayer(k_InteractableLayerName);
            int playerLayer = GetRequiredLayer(k_PlayerLayerName);
            for (int layer = 0; layer < 32; layer++)
            {
                Physics.IgnoreLayerCollision(
                    interactableLayer,
                    layer,
                    layer != playerLayer);
            }
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                GetOrAddComponent<PlayerInteractionManager>(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, k_PlayerPrefabPath) == null)
                {
                    throw new InvalidOperationException("Could not save the Player prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigurePlayerUIPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerUIPrefabPath);
            try
            {
                PlayerUIPopUpManager popupManager =
                    GetRequiredComponent<PlayerUIPopUpManager>(root);
                Transform canvas = root.transform.Find("Player UI") ??
                    throw new InvalidOperationException(
                        "Player UI Manager is missing the Player UI Canvas.");
                TMP_FontAsset font = root.GetComponentsInChildren<TMP_Text>(true)
                    .Select(text => text.font)
                    .FirstOrDefault(candidate => candidate != null) ??
                    throw new InvalidOperationException(
                        "Player UI Manager is missing a TMP font.");

                Transform popupParent = canvas.Find(k_PopupOrganizerName) ?? canvas;
                RectTransform popup = GetOrCreateRectTransform(
                    popupParent,
                    k_MessagePopupName);
                ConfigureCenteredRect(
                    popup,
                    new Vector2(0f, 150f),
                    new Vector2(720f, 68f));
                Image background = GetOrAddComponent<Image>(popup.gameObject);
                background.color = s_backgroundColor;
                background.raycastTarget = false;
                Outline outline = GetOrAddComponent<Outline>(popup.gameObject);
                outline.effectColor = s_borderColor;
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                outline.useGraphicAlpha = true;

                TMP_Text inputPrompt = ConfigureText(
                    GetOrCreateRectTransform(popup, "Input Prompt"),
                    font,
                    "Y / R",
                    TextAlignmentOptions.Center,
                    25f);
                ConfigureAnchoredRect(
                    inputPrompt.rectTransform,
                    new Vector2(-270f, 0f),
                    new Vector2(130f, 54f));
                TMP_Text messageText = ConfigureText(
                    GetOrCreateRectTransform(popup, "Message Text"),
                    font,
                    "Interact",
                    TextAlignmentOptions.MidlineLeft,
                    27f);
                ConfigureAnchoredRect(
                    messageText.rectTransform,
                    new Vector2(65f, 0f),
                    new Vector2(510f, 54f));

                SetObjectReference(
                    popupManager,
                    "m_playerMessagePopup",
                    popup.gameObject);
                SetObjectReference(
                    popupManager,
                    "m_playerMessageText",
                    messageText);
                SetLayerRecursively(popup.gameObject, canvas.gameObject.layer);
                popup.gameObject.SetActive(false);
                EditorUtility.SetDirty(background);
                EditorUtility.SetDirty(outline);
                EditorUtility.SetDirty(popupManager);
                if (PrefabUtility.SaveAsPrefabAsset(root, k_PlayerUIPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the Player UI Manager prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static TMP_Text ConfigureText(
            RectTransform rectTransform,
            TMP_FontAsset font,
            string content,
            TextAlignmentOptions alignment,
            float fontSize)
        {
            TextMeshProUGUI text =
                GetOrAddComponent<TextMeshProUGUI>(rectTransform.gameObject);
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.SmallCaps;
            text.alignment = alignment;
            text.color = s_textColor;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            EditorUtility.SetDirty(text);
            return text;
        }

        private static void ValidateInputActions()
        {
            InputActionAsset controls =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(k_PlayerControlsPath) ??
                throw new InvalidOperationException("PlayerControls is missing.");
            InputAction interact = controls.FindActionMap("Player Movement", true)
                .FindAction("Interact", true);
            if (interact.type != InputActionType.Button ||
                !HasBinding(interact, "<Gamepad>/buttonNorth") ||
                !HasBinding(interact, "<Keyboard>/r"))
            {
                throw new InvalidOperationException(
                    "Interact must be a Button bound to Gamepad North and keyboard R.");
            }
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_PlayerPrefabPath) ??
                throw new InvalidOperationException("Player prefab is missing.");
            if (player.GetComponent<PlayerInteractionManager>() == null ||
                player.GetComponent<PlayerManager>() == null)
            {
                throw new InvalidOperationException(
                    "Player prefab is missing its interaction manager.");
            }
        }

        private static void ValidatePlayerUIPrefab()
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_PlayerUIPrefabPath) ??
                throw new InvalidOperationException(
                    "Player UI Manager prefab is missing.");
            PlayerUIPopUpManager popupManager =
                GetRequiredComponent<PlayerUIPopUpManager>(root);
            Transform popup = root.transform.Find(
                $"Player UI/{k_PopupOrganizerName}/{k_MessagePopupName}") ??
                root.transform.Find($"Player UI/{k_MessagePopupName}");
            TMP_Text messageText = popup?.Find("Message Text")
                ?.GetComponent<TMP_Text>();
            if (popup == null ||
                popup.gameObject.activeSelf ||
                popup.GetComponent<Image>() == null ||
                popup.Find("Input Prompt") == null ||
                messageText == null)
            {
                throw new InvalidOperationException(
                    "Player UI is missing its disabled interaction message popup.");
            }

            ValidateObjectReference(
                popupManager,
                "m_playerMessagePopup",
                popup.gameObject);
            ValidateObjectReference(
                popupManager,
                "m_playerMessageText",
                messageText);
        }

        private static void ValidateLayers()
        {
            int interactableLayer = GetRequiredLayer(k_InteractableLayerName);
            int playerLayer = GetRequiredLayer(k_PlayerLayerName);
            for (int layer = 0; layer < 32; layer++)
            {
                bool shouldIgnore = layer != playerLayer;
                if (Physics.GetIgnoreLayerCollision(interactableLayer, layer) !=
                    shouldIgnore)
                {
                    throw new InvalidOperationException(
                        "Interactable must collide only with the Player layer.");
                }
            }
        }

        private static void ValidateRuntimeArchitecture()
        {
            BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;
            if (!typeof(Unity.Netcode.NetworkBehaviour).IsAssignableFrom(
                    typeof(Interactable)) ||
                typeof(Interactable).GetMethod("Interact", publicInstance) == null ||
                typeof(PlayerInteractionManager).GetMethod(
                    "CheckForInteractable",
                    publicInstance) == null ||
                typeof(PlayerInteractionManager).GetMethod(
                    "RefreshInteractionList",
                    publicInstance) == null ||
                typeof(PlayerInteractionManager).GetMethod(
                    "HandleInteractionInput",
                    publicInstance) == null ||
                typeof(PlayerUIPopUpManager).GetMethod(
                    "SendPlayerMessagePopup",
                    publicInstance) == null ||
                typeof(PlayerUIPopUpManager).GetMethod(
                    "CloseAllPopUpWindows",
                    publicInstance) == null)
            {
                throw new InvalidOperationException(
                    "The interaction runtime contract is incomplete.");
            }
        }

        private static int EnsureLayer(string layerName)
        {
            int existingLayer = LayerMask.NameToLayer(layerName);
            if (existingLayer >= 0)
            {
                return existingLayer;
            }

            UnityEngine.Object tagManager = AssetDatabase.LoadAllAssetsAtPath(
                    "ProjectSettings/TagManager.asset")
                .FirstOrDefault() ??
                throw new InvalidOperationException("TagManager.asset is unavailable.");
            SerializedObject serializedTagManager = new SerializedObject(tagManager);
            SerializedProperty layers = serializedTagManager.FindProperty("layers") ??
                throw new InvalidOperationException("TagManager layers are unavailable.");
            for (int layer = 8; layer < layers.arraySize; layer++)
            {
                SerializedProperty layerProperty = layers.GetArrayElementAtIndex(layer);
                if (!string.IsNullOrEmpty(layerProperty.stringValue))
                {
                    continue;
                }

                layerProperty.stringValue = layerName;
                serializedTagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return layer;
            }

            throw new InvalidOperationException("No User Layer is available for Interactable.");
        }

        private static int GetRequiredLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0
                ? layer
                : throw new InvalidOperationException(
                    $"Required layer is missing: {layerName}.");
        }

        private static bool HasBinding(InputAction action, string bindingPath)
        {
            return action.bindings.Any(binding =>
                string.Equals(
                    binding.path,
                    bindingPath,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static void ConfigureCenteredRect(
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

        private static void ConfigureAnchoredRect(
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
            RectTransform rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
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

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{target.GetType().Name} is missing {propertyName}.");
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object expectedValue)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            if (serializedObject.FindProperty(propertyName)?.objectReferenceValue !=
                expectedValue)
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
    }
}
