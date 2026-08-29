using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP92 persistent loading overlay.</summary>
    public static class LoadingScreenSystemSetup
    {
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        [MenuItem("Tools/Elden/Configure Loading Screen System")]
        public static void ConfigureLoadingScreenSystem()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Transform playerUI = FindDescendant(root.transform, "Player UI") ??
                    throw new InvalidOperationException(
                        "Player UI prefab requires the Player UI Canvas root.");
                PlayerUIManager playerUIManager =
                    root.GetComponent<PlayerUIManager>() ??
                    throw new InvalidOperationException(
                        "Player UI prefab requires PlayerUIManager.");

                RectTransform managerTransform = GetOrCreateRectTransform(
                    playerUI,
                    "Loading Screen Manager");
                StretchToParent(managerTransform);
                PlayerUILoadingScreenManager loadingScreenManager =
                    GetOrAddComponent<PlayerUILoadingScreenManager>(
                        managerTransform.gameObject);

                RectTransform loadingScreen = GetOrCreateRectTransform(
                    managerTransform,
                    "Loading Screen");
                StretchToParent(loadingScreen);
                CanvasGroup canvasGroup = GetOrAddComponent<CanvasGroup>(
                    loadingScreen.gameObject);
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                RectTransform background = GetOrCreateRectTransform(
                    loadingScreen,
                    "Background");
                StretchToParent(background);
                Image backgroundImage = GetOrAddComponent<Image>(
                    background.gameObject);
                backgroundImage.color = new Color(0.008f, 0.007f, 0.006f, 1f);
                backgroundImage.raycastTarget = false;

                RectTransform loadingIcon = GetOrCreateRectTransform(
                    loadingScreen,
                    "Loading Icon");
                ConfigureLoadingIcon(loadingIcon);
                Image iconImage = GetOrAddComponent<Image>(
                    loadingIcon.gameObject);
                iconImage.color = new Color(0.82f, 0.68f, 0.32f, 1f);
                iconImage.raycastTarget = false;
                GetOrAddComponent<FadeLoadingScreenIcon>(loadingIcon.gameObject);

                SetObjectReference(
                    loadingScreenManager,
                    "m_loadingScreen",
                    loadingScreen.gameObject);
                SetObjectReference(
                    loadingScreenManager,
                    "m_loadingScreenCanvasGroup",
                    canvasGroup);
                SetObjectReference(
                    playerUIManager,
                    "m_playerUILoadingScreenManager",
                    loadingScreenManager);

                managerTransform.SetAsLastSibling();
                managerTransform.gameObject.SetActive(true);
                loadingScreen.gameObject.SetActive(false);
                EditorUtility.SetDirty(loadingScreenManager);
                EditorUtility.SetDirty(playerUIManager);
                PrefabUtility.SaveAsPrefabAsset(root, k_PlayerUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            ValidateLoadingScreenSystem();
            Debug.Log(
                "[LoadingScreenSystemSetup] Configured EP92 immediate loading " +
                "overlay, unscaled fade-out, and breathing icon.");
        }

        [MenuItem("Tools/Elden/Validate Loading Screen System")]
        public static void ValidateLoadingScreenSystem()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                PlayerUIManager playerUIManager =
                    root.GetComponent<PlayerUIManager>();
                PlayerUILoadingScreenManager manager =
                    root.GetComponentInChildren<PlayerUILoadingScreenManager>(
                        true);
                if (playerUIManager == null || manager == null)
                {
                    throw new InvalidOperationException(
                        "Player UI requires PlayerUILoadingScreenManager.");
                }

                SerializedObject serializedUI = new SerializedObject(
                    playerUIManager);
                SerializedObject serializedManager = new SerializedObject(manager);
                GameObject loadingScreen = serializedManager.FindProperty(
                    "m_loadingScreen").objectReferenceValue as GameObject;
                CanvasGroup canvasGroup = serializedManager.FindProperty(
                    "m_loadingScreenCanvasGroup")
                    .objectReferenceValue as CanvasGroup;
                Image background = loadingScreen?.transform.Find("Background")
                    ?.GetComponent<Image>();
                FadeLoadingScreenIcon icon = loadingScreen
                    ?.GetComponentInChildren<FadeLoadingScreenIcon>(true);
                if (serializedUI.FindProperty("m_playerUILoadingScreenManager")
                        .objectReferenceValue != manager ||
                    loadingScreen == null ||
                    loadingScreen.activeSelf ||
                    canvasGroup == null ||
                    background == null ||
                    background.raycastTarget ||
                    icon == null)
                {
                    throw new InvalidOperationException(
                        "Loading overlay references or presentation are invalid.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log(
                "[LoadingScreenSystemValidation] Loading overlay structure, " +
                "CanvasGroup, manager reference, and icon animation are valid.");
        }

        private static void ConfigureLoadingIcon(RectTransform icon)
        {
            icon.anchorMin = new Vector2(1f, 0f);
            icon.anchorMax = new Vector2(1f, 0f);
            icon.pivot = new Vector2(1f, 0f);
            icon.anchoredPosition = new Vector2(-76f, 64f);
            icon.sizeDelta = new Vector2(54f, 54f);
            icon.localScale = Vector3.one;
            icon.localRotation = Quaternion.Euler(0f, 0f, 45f);
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

            GameObject gameObject = new GameObject(
                objectName,
                typeof(RectTransform));
            RectTransform rectTransform =
                gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static Transform FindDescendant(
            Transform parent,
            string objectName)
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

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }

        private static void SetObjectReference(
            Component component,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(
                propertyName) ?? throw new InvalidOperationException(
                $"{component.GetType().Name} is missing {propertyName}.");
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
