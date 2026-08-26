using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Builds and validates the EP102 Rune rewards and HUD presentation.</summary>
    public static class RuneSystemSetup
    {
        private const string k_PlayerPrefabPath =
            "Assets/Data/Prefabs/Player.prefab";
        private const string k_UndeadPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";
        private const string k_PlayerUIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";

        private static readonly Color s_backgroundColor =
            new Color(0.015f, 0.012f, 0.01f, 0.82f);
        private static readonly Color s_countColor =
            new Color(0.92f, 0.9f, 0.82f, 1f);
        private static readonly Color s_pendingColor =
            new Color(0.88f, 0.72f, 0.32f, 1f);

        [MenuItem("Tools/Elden/Configure Rune System")]
        public static void ConfigureRuneSystem()
        {
            ConfigureCharacterPrefab(
                k_PlayerPrefabPath,
                CharacterGroup.TeamOne,
                0);
            ConfigureCharacterPrefab(
                k_UndeadPrefabPath,
                CharacterGroup.TeamTwo,
                50);
            ConfigureCharacterPrefab(
                k_BossPrefabPath,
                CharacterGroup.TeamTwo,
                5000);
            ConfigureRuneHUD();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateRuneSystem();
            Debug.Log(
                "[RuneSystemSetup] Configured factions, Rune rewards, and the pending Rune HUD.");
        }

        [MenuItem("Tools/Elden/Validate Rune System")]
        public static void ValidateRuneSystem()
        {
            ValidateCharacterPrefab(
                k_PlayerPrefabPath,
                CharacterGroup.TeamOne,
                0);
            ValidateCharacterPrefab(
                k_UndeadPrefabPath,
                CharacterGroup.TeamTwo,
                50);
            ValidateCharacterPrefab(
                k_BossPrefabPath,
                CharacterGroup.TeamTwo,
                5000);
            ValidateRuneHUD();
            Debug.Log(
                "[RuneSystemValidation] Rune data, rewards, factions, and HUD are valid.");
        }

        private static void ConfigureCharacterPrefab(
            string prefabPath,
            CharacterGroup characterGroup,
            int runesDroppedOnDeath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                CharacterManager character = root.GetComponent<CharacterManager>();
                CharacterStatsManager stats = root.GetComponent<CharacterStatsManager>();
                if (character == null || stats == null)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} requires Character and Stats managers.");
                }

                SetEnum(character, "m_characterGroup", (int)characterGroup);
                SetInteger(stats, "m_runesDroppedOnDeath", runesDroppedOnDeath);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureRuneHUD()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIManagerPrefabPath);

            try
            {
                Transform hud = root.transform.Find("Player UI/HUD");
                PlayerUIHUDManager hudManager =
                    hud?.GetComponent<PlayerUIHUDManager>();
                if (hud == null || hudManager == null)
                {
                    throw new InvalidOperationException(
                        "Player UI Manager prefab requires Player UI/HUD.");
                }

                TMP_FontAsset font = root.GetComponentsInChildren<TMP_Text>(true)
                    .Select(text => text.font)
                    .FirstOrDefault(candidate => candidate != null);
                RectTransform runesHUD = GetOrCreateRectTransform(hud, "Runes HUD");
                runesHUD.anchorMin = new Vector2(1f, 0f);
                runesHUD.anchorMax = new Vector2(1f, 0f);
                runesHUD.pivot = new Vector2(1f, 0f);
                runesHUD.anchoredPosition = new Vector2(-44f, 42f);
                runesHUD.sizeDelta = new Vector2(310f, 94f);
                CanvasGroup canvasGroup = GetOrAddComponent<CanvasGroup>(
                    runesHUD.gameObject);
                EditorUtility.SetDirty(runesHUD.gameObject);
                EditorUtility.SetDirty(canvasGroup);

                RectTransform background = GetOrCreateRectTransform(
                    runesHUD,
                    "Background");
                StretchToParent(background);
                Image backgroundImage = GetOrAddComponent<Image>(
                    background.gameObject);
                backgroundImage.color = s_backgroundColor;
                backgroundImage.raycastTarget = false;

                TMP_Text countText = ConfigureText(
                    GetOrCreateRectTransform(runesHUD, "Runes Count Text"),
                    font,
                    "0",
                    34f,
                    s_countColor);
                countText.rectTransform.anchorMin = Vector2.zero;
                countText.rectTransform.anchorMax = Vector2.one;
                countText.rectTransform.offsetMin = new Vector2(28f, 8f);
                countText.rectTransform.offsetMax = new Vector2(-24f, -35f);

                TMP_Text pendingText = ConfigureText(
                    GetOrCreateRectTransform(runesHUD, "Runes To Add Text"),
                    font,
                    string.Empty,
                    24f,
                    s_pendingColor);
                pendingText.rectTransform.anchorMin = Vector2.zero;
                pendingText.rectTransform.anchorMax = Vector2.one;
                pendingText.rectTransform.offsetMin = new Vector2(28f, 48f);
                pendingText.rectTransform.offsetMax = new Vector2(-24f, -8f);
                pendingText.gameObject.SetActive(false);

                SetObjectReference(hudManager, "m_runesCountText", countText);
                SetObjectReference(hudManager, "m_runesToAddText", pendingText);
                AddCanvasGroup(hudManager, canvasGroup);
                SetUILayerRecursively(runesHUD.gameObject);
                EditorUtility.SetDirty(hudManager);
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    k_PlayerUIManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static TMP_Text ConfigureText(
            RectTransform rectTransform,
            TMP_FontAsset font,
            string value,
            float fontSize,
            Color color)
        {
            TextMeshProUGUI text = GetOrAddComponent<TextMeshProUGUI>(
                rectTransform.gameObject);
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            EditorUtility.SetDirty(text);
            return text;
        }

        private static void ValidateCharacterPrefab(
            string prefabPath,
            CharacterGroup expectedGroup,
            int expectedReward)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                CharacterManager character = root.GetComponent<CharacterManager>();
                CharacterStatsManager stats = root.GetComponent<CharacterStatsManager>();
                if (character == null ||
                    stats == null ||
                    character.CharacterGroup != expectedGroup ||
                    stats.RunesDroppedOnDeath != expectedReward)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} has invalid Rune reward or faction data.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateRuneHUD()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIManagerPrefabPath);

            try
            {
                Transform hud = root.transform.Find("Player UI/HUD");
                Transform runesHUD = hud?.Find("Runes HUD");
                PlayerUIHUDManager hudManager =
                    hud?.GetComponent<PlayerUIHUDManager>();
                TMP_Text countText = runesHUD?.Find("Runes Count Text")
                    ?.GetComponent<TMP_Text>();
                TMP_Text pendingText = runesHUD?.Find("Runes To Add Text")
                    ?.GetComponent<TMP_Text>();
                SerializedObject serializedHUD = new SerializedObject(hudManager);
                if (runesHUD == null ||
                    countText == null ||
                    pendingText == null ||
                    pendingText.gameObject.activeSelf ||
                    runesHUD.GetComponent<CanvasGroup>() == null ||
                    serializedHUD.FindProperty("m_runesCountText")
                        .objectReferenceValue != countText ||
                    serializedHUD.FindProperty("m_runesToAddText")
                        .objectReferenceValue != pendingText)
                {
                    throw new InvalidOperationException(
                        "Player UI Manager prefab has an invalid Rune HUD.");
                }

                RectTransform rectTransform = (RectTransform)runesHUD;
                if (rectTransform.anchorMin != new Vector2(1f, 0f) ||
                    rectTransform.anchorMax != new Vector2(1f, 0f))
                {
                    throw new InvalidOperationException(
                        "Rune HUD must remain anchored to the bottom-right corner.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(
            UnityEngine.Object target,
            string propertyName,
            int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).enumValueIndex = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInteger(
            UnityEngine.Object target,
            string propertyName,
            int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddCanvasGroup(
            PlayerUIHUDManager hudManager,
            CanvasGroup canvasGroup)
        {
            SerializedObject serializedObject = new SerializedObject(hudManager);
            SerializedProperty groups = serializedObject.FindProperty(
                "m_hudCanvasGroups");
            for (int index = 0; index < groups.arraySize; index++)
            {
                SerializedProperty group = groups.GetArrayElementAtIndex(index);
                if (group.objectReferenceValue == canvasGroup)
                {
                    return;
                }

                if (group.objectReferenceValue == null)
                {
                    group.objectReferenceValue = canvasGroup;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }

            groups.arraySize++;
            groups.GetArrayElementAtIndex(groups.arraySize - 1)
                .objectReferenceValue = canvasGroup;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetUILayerRecursively(GameObject root)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            root.layer = uiLayer;
            foreach (Transform child in root.transform)
            {
                SetUILayerRecursively(child.gameObject);
            }
        }
    }
}
