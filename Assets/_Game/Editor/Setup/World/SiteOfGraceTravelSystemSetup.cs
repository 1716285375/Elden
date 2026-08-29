using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP91 Site of Grace travel flow.</summary>
    public static class SiteOfGraceTravelSystemSetup
    {
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_WorldScenePath =
            WorldScenePathLayout.MasterScenePath;

        private static readonly Color s_overlayColor =
            new Color(0.015f, 0.012f, 0.009f, 0.9f);
        private static readonly Color s_panelColor =
            new Color(0.055f, 0.045f, 0.032f, 0.98f);
        private static readonly Color s_buttonColor =
            new Color(0.12f, 0.095f, 0.055f, 0.96f);
        private static readonly Color s_goldColor =
            new Color(0.82f, 0.68f, 0.32f, 1f);

        [MenuItem("Tools/Elden/Configure Site of Grace Travel System")]
        public static void ConfigureSiteOfGraceTravelSystem()
        {
            SiteDefinition[] sites = ConfigureWorldScene();
            ConfigurePlayerUIPrefab(sites);
            AssetDatabase.SaveAssets();
            ValidateSiteOfGraceTravelSystem();
            Debug.Log(
                $"[SiteOfGraceTravelSystemSetup] Configured EP91 with " +
                $"{sites.Length} independent fast-travel destination(s).");
        }

        [MenuItem("Tools/Elden/Validate Site of Grace Travel System")]
        public static void ValidateSiteOfGraceTravelSystem()
        {
            SiteDefinition[] sites = ValidateWorldScene();
            ValidatePlayerUIPrefab(sites);
            Debug.Log(
                "[SiteOfGraceTravelSystemValidation] World registration, " +
                "separate teleport points, modal UI, and location mappings are valid.");
        }

        private static SiteDefinition[] ConfigureWorldScene()
        {
            Scene scene = OpenWorldScene();
            SiteOfGraceInteractable[] sites = FindSites(scene);
            ValidateSiteIDs(sites);

            WorldObjectManager worldObjectManager = FindComponentsInScene<
                WorldObjectManager>(scene).FirstOrDefault();
            if (worldObjectManager == null)
            {
                GameObject managerObject = new GameObject("World Object Manager");
                SceneManager.MoveGameObjectToScene(managerObject, scene);
                worldObjectManager = managerObject.AddComponent<WorldObjectManager>();
            }

            worldObjectManager.gameObject.name = "World Object Manager";
            foreach (SiteOfGraceInteractable site in sites)
            {
                SerializedObject serializedSite = new SerializedObject(site);
                SerializedProperty teleportProperty = serializedSite.FindProperty(
                    "m_teleportTransform");
                Transform teleportTransform =
                    teleportProperty.objectReferenceValue as Transform;
                if (teleportTransform == null)
                {
                    Transform existing = site.transform.Find("Teleport Point");
                    if (existing == null)
                    {
                        GameObject teleportPoint = new GameObject("Teleport Point");
                        teleportPoint.transform.SetParent(site.transform, false);
                        existing = teleportPoint.transform;
                        existing.localPosition = new Vector3(0f, 0f, 2f);
                        existing.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    }

                    teleportProperty.objectReferenceValue = existing;
                    serializedSite.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(site);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return CreateDefinitions(sites);
        }

        private static void ConfigurePlayerUIPrefab(SiteDefinition[] sites)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Transform playerUI = FindDescendant(root.transform, "Player UI") ??
                    throw new InvalidOperationException(
                        "Player UI prefab requires the Player UI Canvas root.");
                TMP_FontAsset font = root
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Select(text => text.font)
                    .FirstOrDefault(candidate => candidate != null) ??
                    TMP_Settings.defaultFontAsset;
                Button styleButton = root
                    .GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.name == "Save Game Button") ??
                    root.GetComponentInChildren<Button>(true);

                PlayerUIManager playerUIManager =
                    GetOrAddComponent<PlayerUIManager>(root);
                PlayerUISiteOfGraceManager siteManager =
                    GetOrAddComponent<PlayerUISiteOfGraceManager>(root);
                PlayerUITeleportLocationManager teleportManager =
                    GetOrAddComponent<PlayerUITeleportLocationManager>(root);

                RectTransform siteMenu = ConfigureMenuRoot(
                    playerUI,
                    "Site Of Grace Menu");
                RectTransform sitePanel = ConfigurePanel(
                    siteMenu,
                    new Vector2(700f, 480f));
                ConfigureHeading(
                    sitePanel,
                    "Title",
                    "SITE OF GRACE",
                    new Vector2(0f, 155f),
                    font);
                ConfigureTextObject(
                    sitePanel,
                    "Message",
                    "REST AND PREPARE FOR THE JOURNEY",
                    new Vector2(0f, 82f),
                    new Vector2(600f, 48f),
                    22f,
                    font);
                Button travelButton = ConfigureButton(
                    sitePanel,
                    "Travel Button",
                    "TRAVEL",
                    new Vector2(0f, -12f),
                    font,
                    styleButton);
                Button siteReturnButton = ConfigureButton(
                    sitePanel,
                    "Return Button",
                    "RETURN",
                    new Vector2(0f, -102f),
                    font,
                    styleButton);
                ConfigureButtonEvent(
                    travelButton,
                    siteManager.OpenTeleportLocationMenu);
                ConfigureButtonEvent(
                    siteReturnButton,
                    playerUIManager.CloseAllMenuWindows);
                SetVerticalNavigation(travelButton, siteReturnButton);

                RectTransform teleportMenu = ConfigureMenuRoot(
                    playerUI,
                    "Teleport Location Menu");
                RectTransform teleportPanel = ConfigurePanel(
                    teleportMenu,
                    new Vector2(820f, 880f));
                ConfigureHeading(
                    teleportPanel,
                    "Title",
                    "FAST TRAVEL",
                    new Vector2(0f, 360f),
                    font);
                ConfigureTextObject(
                    teleportPanel,
                    "Message",
                    "SELECT AN UNLOCKED SITE OF GRACE",
                    new Vector2(0f, 302f),
                    new Vector2(700f, 40f),
                    20f,
                    font);

                Transform locationList = GetOrCreateRectTransform(
                    teleportPanel,
                    "Locations");
                ConfigureCenteredRect(
                    (RectTransform)locationList,
                    new Vector2(0f, 30f),
                    new Vector2(680f, 500f));
                Button[] locationButtons = ConfigureLocationButtons(
                    locationList,
                    sites,
                    teleportManager,
                    font,
                    styleButton);
                Button teleportReturnButton = ConfigureButton(
                    teleportPanel,
                    "Return Button",
                    "RETURN",
                    new Vector2(0f, -352f),
                    font,
                    styleButton);
                ConfigureButtonEvent(
                    teleportReturnButton,
                    siteManager.OpenSiteOfGraceMenu);
                SetListNavigation(locationButtons, teleportReturnButton);

                SetObjectReference(
                    siteManager,
                    "m_siteOfGraceMenu",
                    siteMenu.gameObject);
                SetObjectReference(siteManager, "m_travelButton", travelButton);
                SetObjectReference(
                    siteManager,
                    "m_returnButton",
                    siteReturnButton);
                SetObjectReference(
                    teleportManager,
                    "m_teleportLocationMenu",
                    teleportMenu.gameObject);
                SetObjectArray(
                    teleportManager,
                    "m_teleportLocationButtons",
                    locationButtons);
                SetIntArray(
                    teleportManager,
                    "m_siteOfGraceIDs",
                    sites.Select(site => site.ID).ToArray());
                SetObjectReference(
                    teleportManager,
                    "m_returnButton",
                    teleportReturnButton);
                SetObjectReference(
                    playerUIManager,
                    "m_playerUISiteOfGraceManager",
                    siteManager);
                SetObjectReference(
                    playerUIManager,
                    "m_playerUITeleportLocationManager",
                    teleportManager);

                siteMenu.gameObject.SetActive(false);
                teleportMenu.gameObject.SetActive(false);
                EditorUtility.SetDirty(siteManager);
                EditorUtility.SetDirty(teleportManager);
                EditorUtility.SetDirty(playerUIManager);
                PrefabUtility.SaveAsPrefabAsset(root, k_PlayerUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SiteDefinition[] ValidateWorldScene()
        {
            Scene scene = OpenWorldScene();
            SiteOfGraceInteractable[] sites = FindSites(scene);
            ValidateSiteIDs(sites);
            if (FindComponentsInScene<WorldObjectManager>(scene).Count != 1)
            {
                throw new InvalidOperationException(
                    "World Scene requires exactly one WorldObjectManager.");
            }

            foreach (SiteOfGraceInteractable site in sites)
            {
                if (site.TeleportTransform == null ||
                    site.TeleportTransform == site.transform ||
                    !site.TeleportTransform.IsChildOf(site.transform))
                {
                    throw new InvalidOperationException(
                        $"Site {site.SiteOfGraceID} requires a separate child " +
                        "Teleport Point.");
                }
            }

            return CreateDefinitions(sites);
        }

        private static void ValidatePlayerUIPrefab(SiteDefinition[] sites)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                PlayerUIManager playerUIManager =
                    root.GetComponent<PlayerUIManager>();
                PlayerUISiteOfGraceManager siteManager =
                    root.GetComponent<PlayerUISiteOfGraceManager>();
                PlayerUITeleportLocationManager teleportManager =
                    root.GetComponent<PlayerUITeleportLocationManager>();
                if (playerUIManager == null ||
                    siteManager == null ||
                    teleportManager == null)
                {
                    throw new InvalidOperationException(
                        "Player UI prefab requires both EP91 menu managers.");
                }

                SerializedObject serializedUI = new SerializedObject(
                    playerUIManager);
                SerializedObject serializedSite = new SerializedObject(siteManager);
                SerializedObject serializedTeleport = new SerializedObject(
                    teleportManager);
                SerializedProperty buttons = serializedTeleport.FindProperty(
                    "m_teleportLocationButtons");
                SerializedProperty ids = serializedTeleport.FindProperty(
                    "m_siteOfGraceIDs");
                if (serializedUI.FindProperty("m_playerUISiteOfGraceManager")
                        .objectReferenceValue != siteManager ||
                    serializedUI.FindProperty(
                            "m_playerUITeleportLocationManager")
                        .objectReferenceValue != teleportManager ||
                    serializedSite.FindProperty("m_siteOfGraceMenu")
                        .objectReferenceValue == null ||
                    serializedTeleport.FindProperty("m_teleportLocationMenu")
                        .objectReferenceValue == null ||
                    buttons.arraySize != sites.Length ||
                    ids.arraySize != sites.Length)
                {
                    throw new InvalidOperationException(
                        "Player UI EP91 references do not match the World Scene.");
                }

                for (int index = 0; index < sites.Length; index++)
                {
                    Button button = buttons.GetArrayElementAtIndex(index)
                        .objectReferenceValue as Button;
                    if (ids.GetArrayElementAtIndex(index).intValue !=
                            sites[index].ID ||
                        button == null ||
                        !HasPersistentListener(
                            button,
                            teleportManager,
                            nameof(PlayerUITeleportLocationManager
                                .TeleportToSiteOfGrace)))
                    {
                        throw new InvalidOperationException(
                            $"Travel button {index} is not mapped to Site " +
                            $"{sites[index].ID}.");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Button[] ConfigureLocationButtons(
            Transform parent,
            SiteDefinition[] sites,
            PlayerUITeleportLocationManager manager,
            TMP_FontAsset font,
            Button styleButton)
        {
            HashSet<string> expectedNames = sites
                .Select(site => $"Location {site.ID:00}")
                .ToHashSet();
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                if (child.name.StartsWith("Location ", StringComparison.Ordinal) &&
                    !expectedNames.Contains(child.name))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            Button[] buttons = new Button[sites.Length];
            float topY = 205f;
            for (int index = 0; index < sites.Length; index++)
            {
                SiteDefinition site = sites[index];
                Button button = ConfigureButton(
                    parent,
                    $"Location {site.ID:00}",
                    site.Label,
                    new Vector2(0f, topY - index * 70f),
                    font,
                    styleButton,
                    new Vector2(620f, 58f));
                ConfigureIntButtonEvent(
                    button,
                    manager.TeleportToSiteOfGrace,
                    site.ID);
                buttons[index] = button;
            }

            return buttons;
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

        private static RectTransform ConfigurePanel(
            RectTransform menu,
            Vector2 size)
        {
            RectTransform panel = GetOrCreateRectTransform(menu, "Panel");
            ConfigureCenteredRect(panel, Vector2.zero, size);
            Image panelImage = GetOrAddComponent<Image>(panel.gameObject);
            panelImage.color = s_panelColor;
            panelImage.raycastTarget = true;
            return panel;
        }

        private static void ConfigureHeading(
            Transform parent,
            string objectName,
            string text,
            Vector2 position,
            TMP_FontAsset font)
        {
            TMP_Text heading = ConfigureTextObject(
                parent,
                objectName,
                text,
                position,
                new Vector2(680f, 72f),
                38f,
                font);
            heading.color = s_goldColor;
            heading.fontStyle = FontStyles.SmallCaps;
        }

        private static TMP_Text ConfigureTextObject(
            Transform parent,
            string objectName,
            string text,
            Vector2 position,
            Vector2 size,
            float fontSize,
            TMP_FontAsset font)
        {
            RectTransform textRect = GetOrCreateRectTransform(
                parent,
                objectName);
            ConfigureCenteredRect(textRect, position, size);
            TextMeshProUGUI textComponent =
                GetOrAddComponent<TextMeshProUGUI>(textRect.gameObject);
            textComponent.text = text;
            textComponent.font = font;
            textComponent.fontSize = fontSize;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.color = new Color(0.88f, 0.84f, 0.72f, 1f);
            textComponent.raycastTarget = false;
            return textComponent;
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
            TextMeshProUGUI text =
                GetOrAddComponent<TextMeshProUGUI>(labelRect.gameObject);
            text.text = label;
            text.font = font;
            text.fontSize = 26f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.92f, 0.87f, 0.73f, 1f);
            text.raycastTarget = false;
            return button;
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
                destination.color = s_buttonColor;
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
            }
            else
            {
                ColorBlock colors = destination.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.9f, 0.58f, 1f);
                colors.selectedColor = s_goldColor;
                colors.pressedColor = new Color(0.7f, 0.52f, 0.2f, 1f);
                colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
                destination.colors = colors;
            }
        }

        private static void ConfigureButtonEvent(
            Button button,
            UnityAction action)
        {
            ClearPersistentListeners(button);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            EditorUtility.SetDirty(button);
        }

        private static void ConfigureIntButtonEvent(
            Button button,
            UnityAction<int> action,
            int value)
        {
            ClearPersistentListeners(button);
            UnityEventTools.AddIntPersistentListener(button.onClick, action, value);
            EditorUtility.SetDirty(button);
        }

        private static void ClearPersistentListeners(Button button)
        {
            for (int index = button.onClick.GetPersistentEventCount() - 1;
                index >= 0;
                index--)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, index);
            }
        }

        private static bool HasPersistentListener(
            Button button,
            UnityEngine.Object target,
            string methodName)
        {
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

        private static void SetVerticalNavigation(Button top, Button bottom)
        {
            top.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = bottom,
                selectOnDown = bottom
            };
            bottom.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = top,
                selectOnDown = top
            };
        }

        private static void SetListNavigation(
            Button[] buttons,
            Button returnButton)
        {
            if (buttons.Length == 0)
            {
                returnButton.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = returnButton,
                    selectOnDown = returnButton
                };
                return;
            }

            for (int index = 0; index < buttons.Length; index++)
            {
                buttons[index].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = index == 0
                        ? returnButton
                        : buttons[index - 1],
                    selectOnDown = index == buttons.Length - 1
                        ? returnButton
                        : buttons[index + 1]
                };
            }

            returnButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[^1],
                selectOnDown = buttons[0]
            };
        }

        private static Scene OpenWorldScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_WorldScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                return scene;
            }

            return EditorSceneManager.OpenScene(
                k_WorldScenePath,
                OpenSceneMode.Single);
        }

        private static SiteOfGraceInteractable[] FindSites(Scene scene)
        {
            SiteOfGraceInteractable[] sites = FindComponentsInScene<
                SiteOfGraceInteractable>(scene)
                .OrderBy(site => site.SiteOfGraceID)
                .ToArray();
            if (sites.Length == 0)
            {
                throw new InvalidOperationException(
                    "World Scene requires at least one SiteOfGraceInteractable.");
            }

            return sites;
        }

        private static List<T> FindComponentsInScene<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToList();
        }

        private static void ValidateSiteIDs(SiteOfGraceInteractable[] sites)
        {
            if (sites.Any(site => site.SiteOfGraceID <= 0) ||
                sites.Select(site => site.SiteOfGraceID).Distinct().Count() !=
                    sites.Length)
            {
                throw new InvalidOperationException(
                    "Every Site of Grace requires a unique positive save ID.");
            }
        }

        private static SiteDefinition[] CreateDefinitions(
            SiteOfGraceInteractable[] sites)
        {
            return sites.Select(site => new SiteDefinition(
                    site.SiteOfGraceID,
                    BuildLocationLabel(site)))
                .ToArray();
        }

        private static string BuildLocationLabel(SiteOfGraceInteractable site)
        {
            string authoredName = site.gameObject.name
                .Replace("Site Of Grace", string.Empty)
                .Replace("Site of Grace", string.Empty)
                .Trim(' ', '-', '_');
            return string.IsNullOrWhiteSpace(authoredName)
                ? $"SITE OF GRACE {site.SiteOfGraceID:00}"
                : authoredName.ToUpperInvariant();
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
            rectTransform.localScale = Vector3.one;
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
            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
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

        private static void SetObjectArray<T>(
            Component component,
            string propertyName,
            T[] values) where T : UnityEngine.Object
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(
                propertyName) ?? throw new InvalidOperationException(
                $"{component.GetType().Name} is missing {propertyName}.");
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIntArray(
            Component component,
            string propertyName,
            int[] values)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(
                propertyName) ?? throw new InvalidOperationException(
                $"{component.GetType().Name} is missing {propertyName}.");
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).intValue = values[index];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private readonly struct SiteDefinition
        {
            public SiteDefinition(int id, string label)
            {
                ID = id;
                Label = label;
            }

            public int ID { get; }
            public string Label { get; }
        }
    }
}
