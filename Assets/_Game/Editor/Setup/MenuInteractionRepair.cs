using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Repairs authored menu hierarchy, navigation, and command targets.</summary>
    public static class MenuInteractionRepair
    {
        [MenuItem("Tools/ZZ/Repair Character Menu Actions")]
        public static void Repair()
        {
            const string path = "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RepairRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            foreach (PlayerUIManager manager in Object.FindObjectsByType<PlayerUIManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                RepairRoot(manager.gameObject);
            }
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Tools/ZZ/Audit Archive Layout")]
        public static void AuditArchive()
        {
            var lines = new System.Collections.Generic.List<string>();
            foreach (RectTransform rect in Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string path = AnimationUtility.CalculateTransformPath(rect, null);
                if (path.Contains("Load Character Menu") || path.Contains("Save Game Menu"))
                {
                    lines.Add($"{path} size={rect.sizeDelta} pos={rect.anchoredPosition} " +
                        $"anchors={rect.anchorMin}-{rect.anchorMax} text={rect.GetComponent<TMP_Text>()?.text}");
                }
            }
            File.WriteAllLines(".utmp/archive-layout.txt", lines);
        }

        private static void RepairRoot(GameObject root)
        {
            Transform canvas = root.transform.Find("Player UI");
            Transform panel = canvas.Find("Character Menu/Menu Panel");
            PlayerUICharacterMenuManager character = root.GetComponent<PlayerUICharacterMenuManager>();
            string[] names = { "Equipment Button", "Upgrade Weapon Button", "Save Game Button",
                "Return Button", "Return To Main Menu Button", "Quit Game Button" };
            Button[] buttons = names.Select(name =>
                (panel.Find(name) ?? panel.Find("Command Column/" + name)).GetComponent<Button>()).ToArray();
            Transform column = panel.Find("Command Column");
            if (column == null)
            {
                column = new GameObject("Command Column", typeof(RectTransform), typeof(VerticalLayoutGroup)).transform;
                column.SetParent(panel, false);
            }
            var columnRect = (RectTransform)column;
            columnRect.anchorMin = new Vector2(0.12f, 0.20f);
            columnRect.anchorMax = new Vector2(0.88f, 0.82f);
            columnRect.offsetMin = Vector2.zero;
            columnRect.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                button.transform.SetParent(column, false);
                button.transform.SetSiblingIndex(index);
                LayoutElement element = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
                element.minHeight = 44;
                element.preferredHeight = 66;
                element.flexibleHeight = 1;
                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = buttons[(index + 5) % 6];
                navigation.selectOnDown = buttons[(index + 1) % 6];
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                button.navigation = navigation;
                EditorUtility.SetDirty(button);
            }
            Wire(buttons[4], character.ReturnToMainMenu);
            var characterData = new SerializedObject(character);
            characterData.FindProperty("m_initialButton").objectReferenceValue = buttons[0];
            characterData.ApplyModifiedPropertiesWithoutUndo();
            Wire(buttons[5], character.QuitGame);
            Wire(buttons[1], root.GetComponent<PlayerUIWeaponUpgradeManager>().OpenWeaponUpgradeMenu);
            Transform upgrade = root.transform.Find("Weapon Upgrade Menu");
            if (upgrade != null)
            {
                // The upgrade panel must share the gameplay Canvas and its GraphicRaycaster.
                upgrade.SetParent(canvas, false);
                var rect = (RectTransform)upgrade;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            upgrade = canvas.Find("Weapon Upgrade Menu");
            var upgradeData = new SerializedObject(root.GetComponent<PlayerUIWeaponUpgradeManager>());
            upgradeData.FindProperty("m_returnButton").objectReferenceValue = upgrade
                .Find("Upgrade Panel/Return Button").GetComponent<Button>();
            upgradeData.ApplyModifiedPropertiesWithoutUndo();
            Image popup = upgrade.Find("Confirm Upgrade Popup").GetComponent<Image>();
            Color popupColor = popup.color;
            popupColor.a = 1;
            popup.color = popupColor;
        }

        private static void Wire(Button button, UnityAction action)
        {
            while (button.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            }
            UnityEventTools.AddPersistentListener(button.onClick, action);
            EditorUtility.SetDirty(button);
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        }
    }
}
