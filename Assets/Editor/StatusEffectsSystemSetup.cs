using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the authored assets required by EP137-138.</summary>
    public static class StatusEffectsSystemSetup
    {
        private const string k_EffectsFolder = "Assets/Resources/Effects";
        private const string k_TakePoisonPath =
            k_EffectsFolder + "/Take Poison Buildup Effect.asset";
        private const string k_TakeBleedPath =
            k_EffectsFolder + "/Take Bleed Buildup Effect.asset";
        private const string k_DegradePoisonPath =
            k_EffectsFolder + "/Degrade Poison Buildup Effect.asset";
        private const string k_DegradeBleedPath =
            k_EffectsFolder + "/Degrade Bleed Buildup Effect.asset";
        private const string k_BuildupBarPrefabPath =
            "Assets/_Game/Prefabs/UI/Buildup Bar.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        private static readonly Color s_backgroundColor =
            new(0.025f, 0.025f, 0.02f, 0.94f);
        private static readonly Color s_borderColor =
            new(0.52f, 0.42f, 0.24f, 0.95f);
        private static readonly Color s_textColor =
            new(0.93f, 0.88f, 0.72f, 1f);
        private static readonly Color s_poisonColor =
            new(0.34f, 0.62f, 0.2f, 1f);
        private static readonly Color s_bleedColor =
            new(0.66f, 0.08f, 0.1f, 1f);

        [MenuItem("Tools/Elden/Configure Status Effects System")]
        public static void ConfigureStatusEffectsSystem()
        {
            BuildupEffect degradePoison = ConfigureBuildupEffect(
                k_DegradePoisonPath,
                Buildup.Poison,
                0);
            BuildupEffect degradeBleed = ConfigureBuildupEffect(
                k_DegradeBleedPath,
                Buildup.Bleed,
                1);
            ConfigureTakeBuildupEffect(
                k_TakePoisonPath,
                Buildup.Poison,
                25f,
                3,
                degradePoison);
            ConfigureTakeBuildupEffect(
                k_TakeBleedPath,
                Buildup.Bleed,
                25f,
                4,
                degradeBleed);

            GameObject buildupBarPrefab = ConfigureBuildupBarPrefab();
            ConfigurePlayerUI(buildupBarPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateStatusEffectsSystem();
            Debug.Log(
                "[StatusEffectsSystemSetup] Configured Poison/Bleed buildup, " +
                "decay effects, reusable UI, and HUD bindings.");
        }

        [MenuItem("Tools/Elden/Validate Status Effects System")]
        public static void ValidateStatusEffectsSystem()
        {
            TakeBuildupEffect takePoison =
                LoadRequiredAsset<TakeBuildupEffect>(k_TakePoisonPath);
            TakeBuildupEffect takeBleed =
                LoadRequiredAsset<TakeBuildupEffect>(k_TakeBleedPath);
            BuildupEffect degradePoison =
                LoadRequiredAsset<BuildupEffect>(k_DegradePoisonPath);
            BuildupEffect degradeBleed =
                LoadRequiredAsset<BuildupEffect>(k_DegradeBleedPath);

            if (takePoison.BuildupType != Buildup.Poison ||
                takeBleed.BuildupType != Buildup.Bleed ||
                takePoison.DegradeBuildupEffect != degradePoison ||
                takeBleed.DegradeBuildupEffect != degradeBleed ||
                degradePoison.BuildupType != Buildup.Poison ||
                degradeBleed.BuildupType != Buildup.Bleed ||
                degradePoison.TimedEffectID == degradeBleed.TimedEffectID)
            {
                throw new InvalidOperationException(
                    "Buildup effect assets are incomplete or have duplicate IDs.");
            }

            GameObject barPrefab = LoadRequiredAsset<GameObject>(
                k_BuildupBarPrefabPath);
            if (barPrefab.GetComponent<UIBuildupBar>() == null ||
                barPrefab.GetComponent<Slider>() == null ||
                barPrefab.activeSelf)
            {
                throw new InvalidOperationException(
                    "The reusable buildup bar must contain its controller and start hidden.");
            }

            ValidatePlayerUI();
            Debug.Log(
                "[StatusEffectsSystemValidation] EP137-138 authored assets are valid.");
        }

        private static BuildupEffect ConfigureBuildupEffect(
            string assetPath,
            Buildup buildupType,
            int timedEffectID)
        {
            BuildupEffect effect = LoadOrCreateAsset<BuildupEffect>(assetPath);
            SerializedObject serializedEffect = new(effect);
            SetInteger(serializedEffect, "m_timedEffectID", timedEffectID);
            SetFloat(serializedEffect, "m_defaultTimeLengthOnEffect", 60f);
            GetRequiredProperty(serializedEffect, "m_buildupType").enumValueIndex =
                (int)buildupType;
            SetFloat(serializedEffect, "m_buildupAmountDegradation", -1f);
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
            return effect;
        }

        private static TakeBuildupEffect ConfigureTakeBuildupEffect(
            string assetPath,
            Buildup buildupType,
            float buildupAmount,
            int instantEffectID,
            BuildupEffect degradeEffect)
        {
            TakeBuildupEffect effect =
                LoadOrCreateAsset<TakeBuildupEffect>(assetPath);
            SerializedObject serializedEffect = new(effect);
            SetInteger(serializedEffect, "m_instantEffectId", instantEffectID);
            GetRequiredProperty(serializedEffect, "m_buildupType").enumValueIndex =
                (int)buildupType;
            SetFloat(serializedEffect, "m_buildupAmount", buildupAmount);
            GetRequiredProperty(serializedEffect, "m_degradeBuildupEffect")
                .objectReferenceValue = degradeEffect;
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
            return effect;
        }

        private static GameObject ConfigureBuildupBarPrefab()
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BuildupBarPrefabPath);
            GameObject root = prefabRoot != null
                ? PrefabUtility.LoadPrefabContents(k_BuildupBarPrefabPath)
                : CreateBuildupBarHierarchy();

            try
            {
                ConfigureBuildupBarHierarchy(root);
                return PrefabUtility.SaveAsPrefabAsset(
                    root,
                    k_BuildupBarPrefabPath);
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static GameObject CreateBuildupBarHierarchy()
        {
            GameObject root = new(
                "Buildup Bar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(Slider),
                typeof(LayoutElement),
                typeof(UIBuildupBar));
            root.layer = 5;

            GameObject fillArea = new("Fill Area", typeof(RectTransform));
            fillArea.layer = 5;
            fillArea.transform.SetParent(root.transform, false);
            GameObject fill = new(
                "Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            fill.layer = 5;
            fill.transform.SetParent(fillArea.transform, false);

            GameObject label = new(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            label.layer = 5;
            label.transform.SetParent(root.transform, false);
            return root;
        }

        private static void ConfigureBuildupBarHierarchy(GameObject root)
        {
            root.name = "Buildup Bar";
            root.layer = 5;
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(420f, 32f);

            Image background = root.GetComponent<Image>();
            background.color = s_backgroundColor;
            background.raycastTarget = false;
            Outline outline = root.GetComponent<Outline>();
            outline.effectColor = s_borderColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            RectTransform fillArea = RequireChild(root.transform, "Fill Area")
                .GetComponent<RectTransform>();
            Stretch(fillArea, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            RectTransform fill = RequireChild(fillArea, "Fill")
                .GetComponent<RectTransform>();
            Stretch(fill, Vector2.zero, Vector2.zero);
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = Color.white;
            fillImage.raycastTarget = false;

            Slider slider = root.GetComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 0f;
            slider.fillRect = fill;
            slider.targetGraphic = background;
            slider.direction = Slider.Direction.LeftToRight;

            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = 420f;
            layout.preferredHeight = 32f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            TextMeshProUGUI label = RequireChild(root.transform, "Label")
                .GetComponent<TextMeshProUGUI>();
            RectTransform labelRect = label.rectTransform;
            Stretch(labelRect, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            label.text = "STATUS";
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 18f;
            label.fontStyle = FontStyles.SmallCaps;
            label.color = s_textColor;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;

            UIBuildupBar buildupBar = root.GetComponent<UIBuildupBar>();
            SerializedObject serializedBar = new(buildupBar);
            GetRequiredProperty(serializedBar, "m_slider").objectReferenceValue =
                slider;
            GetRequiredProperty(serializedBar, "m_rectTransform")
                .objectReferenceValue = rootRect;
            GetRequiredProperty(serializedBar, "m_buildupType").enumValueIndex =
                (int)Buildup.Poison;
            serializedBar.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
        }

        private static void ConfigurePlayerUI(GameObject buildupBarPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Transform popupOrganizer = FindDescendant(
                    root.transform,
                    "Popup Organizer") ?? throw new InvalidOperationException(
                    "Player UI is missing Popup Organizer.");
                UIBuildupBar poisonBar = ConfigureBuildupBarInstance(
                    popupOrganizer,
                    buildupBarPrefab,
                    "Poison Buildup Bar",
                    "POISON",
                    Buildup.Poison,
                    s_poisonColor);
                UIBuildupBar bleedBar = ConfigureBuildupBarInstance(
                    popupOrganizer,
                    buildupBarPrefab,
                    "Bleed Buildup Bar",
                    "BLEED",
                    Buildup.Bleed,
                    s_bleedColor);

                PlayerUIHUDManager hud = root.GetComponentInChildren<
                    PlayerUIHUDManager>(true) ?? throw new InvalidOperationException(
                    "Player UI is missing PlayerUIHUDManager.");
                SerializedObject serializedHUD = new(hud);
                SerializedProperty bars = GetRequiredProperty(
                    serializedHUD,
                    "m_buildupBars");
                bars.arraySize = Mathf.Max(2, bars.arraySize);
                bars.GetArrayElementAtIndex(0).objectReferenceValue = poisonBar;
                bars.GetArrayElementAtIndex(1).objectReferenceValue = bleedBar;
                serializedHUD.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, k_PlayerUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static UIBuildupBar ConfigureBuildupBarInstance(
            Transform parent,
            GameObject buildupBarPrefab,
            string instanceName,
            string labelText,
            Buildup buildupType,
            Color fillColor)
        {
            Transform existing = FindDirectChild(parent, instanceName);
            GameObject instance = existing != null
                ? existing.gameObject
                : (GameObject)PrefabUtility.InstantiatePrefab(
                    buildupBarPrefab,
                    parent);
            instance.name = instanceName;
            instance.layer = 5;
            instance.transform.SetAsLastSibling();

            UIBuildupBar bar = instance.GetComponent<UIBuildupBar>();
            SerializedObject serializedBar = new(bar);
            GetRequiredProperty(serializedBar, "m_buildupType").enumValueIndex =
                (int)buildupType;
            serializedBar.ApplyModifiedPropertiesWithoutUndo();

            Transform fill = FindDescendant(instance.transform, "Fill");
            Image fillImage = fill?.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = fillColor;
            }

            TextMeshProUGUI label = instance.GetComponentInChildren<
                TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = labelText;
            }

            Slider slider = instance.GetComponent<Slider>();
            slider.value = 0f;
            instance.SetActive(false);
            return bar;
        }

        private static void ValidatePlayerUI()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                PlayerUIHUDManager hud = root.GetComponentInChildren<
                    PlayerUIHUDManager>(true);
                UIBuildupBar[] bars = root.GetComponentsInChildren<
                    UIBuildupBar>(true);
                bool hasPoison = Array.Exists(
                    bars,
                    bar => bar.BuildupType == Buildup.Poison &&
                        !bar.gameObject.activeSelf);
                bool hasBleed = Array.Exists(
                    bars,
                    bar => bar.BuildupType == Buildup.Bleed &&
                        !bar.gameObject.activeSelf);
                if (hud == null || bars.Length < 2 || !hasPoison || !hasBleed)
                {
                    throw new InvalidOperationException(
                        "Player UI must contain hidden Poison and Bleed buildup bars.");
                }

                SerializedObject serializedHUD = new(hud);
                if (GetRequiredProperty(serializedHUD, "m_buildupBars").arraySize < 2)
                {
                    throw new InvalidOperationException(
                        "Player UI HUD buildup references are incomplete.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T LoadOrCreateAsset<T>(string assetPath)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private static Transform RequireChild(Transform parent, string childName)
        {
            return FindDirectChild(parent, childName) ??
                throw new InvalidOperationException(
                    $"{parent.name} is missing child {childName}.");
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
            {
                Transform child = parent.GetChild(childIndex);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindDescendant(Transform parent, string childName)
        {
            if (parent.name == childName)
            {
                return parent;
            }

            for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
            {
                Transform result = FindDescendant(
                    parent.GetChild(childIndex),
                    childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void Stretch(
            RectTransform rectTransform,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.name} is missing {propertyName}.");
        }

        private static void SetInteger(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).intValue = value;
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
        }
    }
}
