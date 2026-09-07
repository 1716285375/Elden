using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Reuses the authored title-button style and existing heavy-weapon motion library.</summary>
    public static class GameplayPresentationRepair
    {
        private const string k_UIPath = "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_AnimationRoot = "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/";
        private const string k_OutputFolder = "Assets/_Game/Data/Animations/Broadsword";

        [MenuItem("Tools/ZZ/Apply Gameplay Button Style")]
        public static void ApplyButtonStyle()
        {
            TitleScreenManager title = UnityEngine.Object.FindFirstObjectByType<TitleScreenManager>(FindObjectsInactive.Include);
            if (title == null)
            {
                throw new InvalidOperationException("Open the main menu scene to read its authored button style.");
            }
            var titleData = new SerializedObject(title);
            var sourceButton = (Button)titleData.FindProperty("m_newGameButton").objectReferenceValue;
            var source = new SerializedObject(sourceButton.GetComponent<FrontendSelectableVisual>());
            TMP_Text sourceLabel = sourceButton.GetComponentInChildren<TMP_Text>(true);
            string[] paths = { k_UIPath, "Assets/_Game/Prefabs/UI/Equipment Slot.prefab",
                "Assets/_Game/Prefabs/UI/Equipment Inventory Slot.prefab" };
            int count = 0;
            foreach (string path in paths)
            {
                Backup(path);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (Button button in root.GetComponentsInChildren<Button>(true))
                    {
                        ApplyButton(button, source, sourceLabel);
                        count++;
                    }
                    PlayerUIManager manager = root.GetComponent<PlayerUIManager>();
                    if (manager != null)
                    {
                        RepairCharacterMenuLayout(root.transform);
                        var managerData = new SerializedObject(manager);
                        managerData.FindProperty("m_gameplayButtonStyle").objectReferenceValue = root
                            .GetComponentsInChildren<FrontendSelectableVisual>(true).First(visual => visual.name == "Return Button");
                        managerData.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"[Gameplay Presentation] Styled {count} gameplay buttons from the main menu.");
        }

        private static void RepairCharacterMenuLayout(Transform root)
        {
            Transform panel = root.Find("Player UI/Character Menu/Menu Panel");
            string[] names = { "Equipment Button", "Upgrade Weapon Button", "Save Game Button", "Return Button",
                "Return To Main Menu Button", "Quit Game Button" };
            Button[] buttons = names.Select(name => panel.Find(name).GetComponent<Button>()).ToArray();
            for (int index = 0; index < buttons.Length; index++)
            {
                var rect = (RectTransform)buttons[index].transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, index < 4 ? 170f - index * 100f : -230f);
                Navigation navigation = buttons[index].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = buttons[index == 0 ? 5 : index >= 4 ? 3 : index - 1];
                navigation.selectOnDown = buttons[index >= 4 ? 0 : index + 1];
                navigation.selectOnLeft = buttons[index == 5 ? 4 : index];
                navigation.selectOnRight = buttons[index == 4 ? 5 : index];
                buttons[index].navigation = navigation;
            }
        }

        private static void ApplyButton(Button button, SerializedObject source, TMP_Text sourceLabel)
        {
            FrontendSelectableVisual visual = button.GetComponent<FrontendSelectableVisual>();
            if (visual == null)
            {
                visual = button.gameObject.AddComponent<FrontendSelectableVisual>();
            }
            Transform existing = button.transform.Find("SelectionBackground");
            Image background;
            if (existing == null)
            {
                var backgroundObject = new GameObject("SelectionBackground", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                backgroundObject.layer = button.gameObject.layer;
                backgroundObject.transform.SetParent(button.transform, false);
                backgroundObject.transform.SetAsFirstSibling();
                backgroundObject.GetComponent<LayoutElement>().ignoreLayout = true;
                background = backgroundObject.GetComponent<Image>();
                background.rectTransform.anchorMin = Vector2.zero;
                background.rectTransform.anchorMax = Vector2.one;
                background.rectTransform.offsetMin = Vector2.zero;
                background.rectTransform.offsetMax = Vector2.zero;
            }
            else
            {
                background = existing.GetComponent<Image>();
            }
            background.raycastTarget = false;
            Transform labelTransform = button.transform.Find("Label");
            TMP_Text label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() :
                button.GetComponentInChildren<TMP_Text>(true);
            bool isItemSlot = button.transform.Find("Item Icon") != null ||
                button.GetComponent("UIEquipmentInventorySlot") != null;
            var data = new SerializedObject(visual);
            string[] styleProperties = { "m_normalBackgroundColor", "m_idleBackgroundColor", "m_disabledBackgroundColor",
                "m_normalTextColor", "m_selectedTextColor", "m_disabledTextColor", "m_idleBackgroundSprite",
                "m_selectedBackgroundSprite", "m_labelShiftX", "m_transitionDuration", "m_transitionEase",
                "m_disableButtonTransition" };
            foreach (string property in styleProperties)
            {
                data.CopyFromSerializedProperty(source.FindProperty(property));
            }
            data.FindProperty("m_selectable").objectReferenceValue = button;
            data.FindProperty("m_selectionBackground").objectReferenceValue = background;
            data.FindProperty("m_label").objectReferenceValue = label;
            if (isItemSlot)
            {
                // Equipment art keeps its own proportions; only the shared focus palette applies.
                data.FindProperty("m_idleBackgroundSprite").objectReferenceValue = null;
                data.FindProperty("m_selectedBackgroundSprite").objectReferenceValue = null;
                data.FindProperty("m_labelShiftX").floatValue = 0f;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            background.sprite = isItemSlot ? null : source.FindProperty("m_idleBackgroundSprite").objectReferenceValue as Sprite;
            background.type = Image.Type.Sliced;
            background.color = background.sprite != null ? Color.white : source.FindProperty("m_idleBackgroundColor").colorValue;
            button.transition = Selectable.Transition.None;
            if (label != null)
            {
                label.font = sourceLabel.font;
                label.color = source.FindProperty("m_normalTextColor").colorValue;
            }
        }

        [MenuItem("Tools/ZZ/Connect Broadsword Animation Library")]
        public static void ConnectBroadswordAnimations()
        {
            const string weaponPath = "Assets/_Game/Data/Items/Weapons/Melee Weapons/Broadsword.asset";
            Backup(weaponPath);
            EnsureFolder(k_OutputFolder);
            var runtime = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(k_AnimationRoot + "Runtime/Humanoid Runtime.controller");
            var imported = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(k_AnimationRoot + "Overrides/Overrides/Greatsword.overrideController");
            var importedPairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            imported.GetOverrides(importedPairs);
            string controllerPath = k_OutputFolder + "/Broadsword.overrideController";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
            if (controller == null)
            {
                controller = new AnimatorOverrideController(runtime);
                AssetDatabase.CreateAsset(controller, controllerPath);
            }
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (AnimationClip original in runtime.animationClips.Distinct())
            {
                string key = original.name.Replace("straight_sword_", "unarmed_")
                    .Replace("_release_full", "_full").Replace("_th_back_step_attack_02_", "_th_back_step_attack_01_");
                AnimationClip replacement = importedPairs.FirstOrDefault(pair => pair.Key.name == key).Value;
                if (replacement == null)
                {
                    continue;
                }
                string clipPath = $"{k_OutputFolder}/{original.name}.anim";
                if (AnimationUtility.GetAnimationEvents(original).Length == 0 &&
                    AnimationUtility.GetAnimationEvents(replacement).Length == 0)
                {
                    overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(original, replacement));
                    // Earlier generated copies in this dedicated output folder are redundant now.
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
                    {
                        AssetDatabase.DeleteAsset(clipPath);
                    }
                    continue;
                }
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = UnityEngine.Object.Instantiate(replacement);
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                else
                {
                    EditorUtility.CopySerialized(replacement, clip);
                }
                clip.name = "Broadsword " + original.name;
                // Keep runtime damage, stamina and cancel-window contracts on the new motion.
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(original);
                foreach (AnimationEvent animationEvent in events)
                {
                    animationEvent.time *= clip.length / Mathf.Max(original.length, 0.001f);
                }
                AnimationUtility.SetAnimationEvents(clip, events);
                overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(original, clip));
            }
            controller.ApplyOverrides(overrides);
            var weapon = new SerializedObject(AssetDatabase.LoadAssetAtPath<WeaponItem>(weaponPath));
            weapon.FindProperty("m_weaponAnimator").objectReferenceValue = controller;
            weapon.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Gameplay Presentation] Connected {overrides.Count} existing motions to Broadsword.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void Backup(string path)
        {
            string destination = Path.Combine(".utmp", "GameplayRepairBackup", path);
            if (!File.Exists(destination))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(path, destination);
            }
        }
    }
}
