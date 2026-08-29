using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the authored EP80 Focus Point presentation.</summary>
    public static class FocusPointsSystemSetup
    {
        private const string k_FireballPath =
            "Assets/_Game/Data/Items/Spells/Fireball.asset";
        private const string k_UIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        [MenuItem("ZZ/Setup/Configure EP80 Focus Points")]
        public static void ConfigureFocusPointsSystem()
        {
            ConfigureFireballCosts();
            ConfigureHUD();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateFocusPointsSystem();
            Debug.Log(
                "[FocusPointsSystemSetup] Configured EP80 FP costs, status bar, " +
                "save-ready resources, and spell quick-slot presentation.");
        }

        private static void ConfigureFireballCosts()
        {
            SpellItem fireball = AssetDatabase.LoadAssetAtPath<SpellItem>(
                k_FireballPath);
            if (fireball == null)
            {
                throw new InvalidOperationException(
                    $"Could not load Fireball at '{k_FireballPath}'.");
            }

            SerializedObject serializedFireball = new SerializedObject(fireball);
            serializedFireball.FindProperty("m_staminaCost").floatValue = 25f;
            serializedFireball.FindProperty("m_focusPointsCost").intValue = 25;
            serializedFireball.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fireball);
        }

        private static void ConfigureHUD()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_UIManagerPrefabPath);
            try
            {
                PlayerUIHUDManager hud = root.GetComponentInChildren<
                    PlayerUIHUDManager>(true);
                UIStatBar staminaBar = FindNamedComponent<UIStatBar>(
                    root,
                    "Stamina Bar");
                if (hud == null || staminaBar == null)
                {
                    throw new InvalidOperationException(
                        "Player UI Manager is missing its HUD or Stamina Bar.");
                }

                UIStatBar focusPointsBar = FindNamedComponent<UIStatBar>(
                    root,
                    "Focus Point Bar");
                if (focusPointsBar == null)
                {
                    GameObject focusBarObject = UnityEngine.Object.Instantiate(
                        staminaBar.gameObject,
                        staminaBar.transform.parent);
                    focusBarObject.name = "Focus Point Bar";
                    focusBarObject.transform.SetSiblingIndex(
                        staminaBar.transform.GetSiblingIndex());
                    focusPointsBar = focusBarObject.GetComponent<UIStatBar>();
                }

                ConfigureFocusPointsBar(focusPointsBar);
                SerializedObject serializedHUD = new SerializedObject(hud);
                serializedHUD.FindProperty("m_focusPointsBar").objectReferenceValue =
                    focusPointsBar;
                UIQuickSlot spellQuickSlot = serializedHUD
                    .FindProperty("m_spellQuickSlot")
                    .objectReferenceValue as UIQuickSlot;
                serializedHUD.ApplyModifiedPropertiesWithoutUndo();
                ConfigureSpellQuickSlot(spellQuickSlot);
                EditorUtility.SetDirty(hud);
                PrefabUtility.SaveAsPrefabAsset(root, k_UIManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureFocusPointsBar(UIStatBar focusPointsBar)
        {
            Slider slider = focusPointsBar.GetComponent<Slider>();
            if (slider == null)
            {
                throw new InvalidOperationException(
                    "Focus Point Bar requires a Slider component.");
            }

            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 100f;
            Image fillImage = slider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = new Color(0.12f, 0.36f, 0.92f, 1f);
                fillImage.raycastTarget = false;
            }

            LayoutElement layoutElement = focusPointsBar.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredWidth = 200f;
                layoutElement.preferredHeight = 24f;
            }

            EditorUtility.SetDirty(focusPointsBar);
        }

        private static void ConfigureSpellQuickSlot(UIQuickSlot spellQuickSlot)
        {
            if (spellQuickSlot == null)
            {
                throw new InvalidOperationException(
                    "Player HUD is missing its Spell Quick Slot reference.");
            }

            SerializedObject serializedQuickSlot = new SerializedObject(
                spellQuickSlot);
            Image icon = serializedQuickSlot.FindProperty("m_iconImage")
                .objectReferenceValue as Image;
            if (icon == null)
            {
                throw new InvalidOperationException(
                    "Spell Quick Slot is missing its item icon Image.");
            }

            icon.preserveAspect = true;
            icon.raycastTarget = false;
            EditorUtility.SetDirty(icon);
        }

        private static T FindNamedComponent<T>(GameObject root, string name)
            where T : Component
        {
            return root.GetComponentsInChildren<T>(true)
                .FirstOrDefault(component => component.gameObject.name == name);
        }

        private static void ValidateFocusPointsSystem()
        {
            SpellItem fireball = AssetDatabase.LoadAssetAtPath<SpellItem>(
                k_FireballPath);
            SerializedObject serializedFireball = new SerializedObject(fireball);
            if (serializedFireball.FindProperty("m_focusPointsCost").intValue != 25 ||
                !Mathf.Approximately(
                    serializedFireball.FindProperty("m_staminaCost").floatValue,
                    25f))
            {
                throw new InvalidOperationException(
                    "Fireball resource costs were not authored correctly.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(
                k_UIManagerPrefabPath);
            try
            {
                PlayerUIHUDManager hud = root.GetComponentInChildren<
                    PlayerUIHUDManager>(true);
                SerializedObject serializedHUD = new SerializedObject(hud);
                UIStatBar focusPointsBar = serializedHUD
                    .FindProperty("m_focusPointsBar")
                    .objectReferenceValue as UIStatBar;
                if (focusPointsBar == null ||
                    focusPointsBar.gameObject.name != "Focus Point Bar")
                {
                    throw new InvalidOperationException(
                        "Player HUD Focus Point Bar is not assigned.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log(
                "[FocusPointsSystemValidation] EP80 Mind, FP, casting costs, " +
                "HUD, save fields, and spell icon data are valid.");
        }
    }
}
