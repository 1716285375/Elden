using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the authored assets required by EP142-144.</summary>
    public static class FrostbiteSystemSetup
    {
        private const string k_EffectsFolder = "Assets/Resources/Effects";
        private const string k_TakeFrostPath =
            k_EffectsFolder + "/Take Frost Buildup Effect.asset";
        private const string k_DegradeFrostPath =
            k_EffectsFolder + "/Degrade Frost Buildup Effect.asset";
        private const string k_StaminaModifierPath =
            k_EffectsFolder + "/Frostbite Stamina Regeneration Modifier.asset";
        private const string k_FrostbiteEffectPath =
            k_EffectsFolder + "/Frostbite Effect.asset";
        private const string k_FrostbiteVFXPath =
            k_EffectsFolder + "/Frostbite VFX.prefab";
        private const string k_FrozenMaterialPath =
            k_EffectsFolder + "/Frozen Material.mat";
        private const string k_BuildupBarPrefabPath =
            "Assets/Data/Prefabs/UI/Buildup Bar.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";

        private static readonly Color s_frostColor =
            new(0.25f, 0.72f, 1f, 1f);

        [MenuItem("Tools/Elden/Configure Frostbite System")]
        public static void ConfigureFrostbiteSystem()
        {
            BuildupEffect degradeFrost = ConfigureDegradeFrostEffect();
            ModifyStaminaRegenerationForATimeEffect staminaModifier =
                ConfigureStaminaModifierEffect();
            ConfigureTakeFrostEffect(degradeFrost);
            ConfigureFrostbiteEffect(staminaModifier);
            ConfigureFrostbiteVFX();
            ConfigureFrozenMaterial();
            ConfigurePlayerUI();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateFrostbiteSystem();
            Debug.Log(
                "[FrostbiteSystemSetup] Configured Frost buildup, timed " +
                "Frostbite, frozen presentation, and HUD bindings.");
        }

        [MenuItem("Tools/Elden/Validate Frostbite System")]
        public static void ValidateFrostbiteSystem()
        {
            TakeBuildupEffect takeFrost =
                LoadRequiredAsset<TakeBuildupEffect>(k_TakeFrostPath);
            BuildupEffect degradeFrost =
                LoadRequiredAsset<BuildupEffect>(k_DegradeFrostPath);
            ModifyStaminaRegenerationForATimeEffect staminaModifier =
                LoadRequiredAsset<ModifyStaminaRegenerationForATimeEffect>(
                    k_StaminaModifierPath);
            FrostbiteEffect frostbite =
                LoadRequiredAsset<FrostbiteEffect>(k_FrostbiteEffectPath);

            if (takeFrost.InstantEffectId != 5 ||
                takeFrost.BuildupType != Buildup.Frost ||
                !Mathf.Approximately(takeFrost.BuildupAmount, 25f) ||
                takeFrost.DegradeBuildupEffect != degradeFrost ||
                degradeFrost.TimedEffectID != 3 ||
                degradeFrost.BuildupType != Buildup.Frost ||
                !Mathf.Approximately(
                    degradeFrost.DefaultTimeLengthOnEffect,
                    60f) ||
                !Mathf.Approximately(
                    degradeFrost.BuildupAmountDegradation,
                    -1f))
            {
                throw new InvalidOperationException(
                    "Frost buildup and degradation assets are incomplete.");
            }

            if (staminaModifier.TimedEffectID != 4 ||
                !Mathf.Approximately(
                    staminaModifier.DefaultTimeLengthOnEffect,
                    60f) ||
                !Mathf.Approximately(staminaModifier.ModifierPercentage, -80f) ||
                frostbite.TimedEffectID != 5 ||
                !Mathf.Approximately(
                    frostbite.DefaultTimeLengthOnEffect,
                    60f) ||
                !Mathf.Approximately(frostbite.HPPercentageDamage, 10f) ||
                frostbite.StaminaRegenerationModifierEffect != staminaModifier)
            {
                throw new InvalidOperationException(
                    "Frostbite duration, damage, or Stamina modifier is invalid.");
            }

            GameObject frostbiteVFX =
                LoadRequiredAsset<GameObject>(k_FrostbiteVFXPath);
            Material frozenMaterial =
                LoadRequiredAsset<Material>(k_FrozenMaterialPath);
            if (frostbiteVFX.GetComponentInChildren<ParticleSystem>(true) == null ||
                frozenMaterial.shader == null ||
                Resources.Load<GameObject>("Effects/Frostbite VFX") !=
                    frostbiteVFX ||
                Resources.Load<Material>("Effects/Frozen Material") !=
                    frozenMaterial)
            {
                throw new InvalidOperationException(
                    "Frostbite VFX or frozen Material is unavailable through Resources.");
            }

            ValidatePlayerUI();
            Debug.Log(
                "[FrostbiteSystemValidation] EP142-144 authored assets are valid.");
        }

        private static BuildupEffect ConfigureDegradeFrostEffect()
        {
            BuildupEffect effect = LoadOrCreateAsset<BuildupEffect>(
                k_DegradeFrostPath);
            SerializedObject serializedEffect = new(effect);
            SetInteger(serializedEffect, "m_timedEffectID", 3);
            SetFloat(serializedEffect, "m_defaultTimeLengthOnEffect", 60f);
            GetRequiredProperty(serializedEffect, "m_buildupType").enumValueIndex =
                (int)Buildup.Frost;
            SetFloat(serializedEffect, "m_buildupAmountDegradation", -1f);
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
            return effect;
        }

        private static void ConfigureTakeFrostEffect(
            BuildupEffect degradeFrost)
        {
            TakeBuildupEffect effect = LoadOrCreateAsset<TakeBuildupEffect>(
                k_TakeFrostPath);
            SerializedObject serializedEffect = new(effect);
            SetInteger(serializedEffect, "m_instantEffectId", 5);
            GetRequiredProperty(serializedEffect, "m_buildupType").enumValueIndex =
                (int)Buildup.Frost;
            SetFloat(serializedEffect, "m_buildupAmount", 25f);
            GetRequiredProperty(serializedEffect, "m_degradeBuildupEffect")
                .objectReferenceValue = degradeFrost;
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
        }

        private static ModifyStaminaRegenerationForATimeEffect
            ConfigureStaminaModifierEffect()
        {
            ModifyStaminaRegenerationForATimeEffect effect =
                LoadOrCreateAsset<ModifyStaminaRegenerationForATimeEffect>(
                    k_StaminaModifierPath);
            SerializedObject serializedEffect = new(effect);
            SetInteger(serializedEffect, "m_timedEffectID", 4);
            SetFloat(serializedEffect, "m_defaultTimeLengthOnEffect", 60f);
            SetFloat(serializedEffect, "m_modifierPercentage", -80f);
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
            return effect;
        }

        private static void ConfigureFrostbiteEffect(
            ModifyStaminaRegenerationForATimeEffect staminaModifier)
        {
            FrostbiteEffect effect = LoadOrCreateAsset<FrostbiteEffect>(
                k_FrostbiteEffectPath);
            SerializedObject serializedEffect = new(effect);
            SetInteger(serializedEffect, "m_timedEffectID", 5);
            SetFloat(serializedEffect, "m_defaultTimeLengthOnEffect", 60f);
            SetFloat(serializedEffect, "m_hpPercentageDamage", 10f);
            GetRequiredProperty(
                serializedEffect,
                "m_staminaRegenerationModifierEffect").objectReferenceValue =
                    staminaModifier;
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
        }

        private static void ConfigureFrostbiteVFX()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_FrostbiteVFXPath);
            GameObject root = prefab != null
                ? PrefabUtility.LoadPrefabContents(k_FrostbiteVFXPath)
                : new GameObject("Frostbite VFX", typeof(ParticleSystem));

            try
            {
                root.name = "Frostbite VFX";
                ParticleSystem particles = root.GetComponent<ParticleSystem>() ??
                    root.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particles.main;
                main.duration = 2f;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.18f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.12f, 0.55f, 1f, 0.25f),
                    new Color(0.7f, 0.94f, 1f, 0.7f));
                main.maxParticles = 72;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = true;
                emission.rateOverTime = 18f;
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.68f;
                shape.radiusThickness = 1f;

                ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                    particles.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient fadeGradient = new();
                fadeGradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(s_frostColor, 0f),
                        new GradientColorKey(s_frostColor, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(0.65f, 0.2f),
                        new GradientAlphaKey(0f, 1f)
                    });
                colorOverLifetime.color = fadeGradient;

                ParticleSystemRenderer renderer = particles.GetComponent<
                    ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 11;
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, k_FrostbiteVFXPath);
            }
            finally
            {
                if (prefab != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void ConfigureFrozenMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ?? throw new InvalidOperationException(
                    "No Lit shader is available for the frozen Material.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                k_FrozenMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "Frozen Material"
                };
                AssetDatabase.CreateAsset(material, k_FrozenMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            SetMaterialColor(material, "_BaseColor", new Color(
                0.2f,
                0.62f,
                0.9f,
                1f));
            SetMaterialColor(material, "_Color", new Color(
                0.2f,
                0.62f,
                0.9f,
                1f));
            SetMaterialFloat(material, "_Metallic", 0.15f);
            SetMaterialFloat(material, "_Smoothness", 0.85f);
            SetMaterialColor(material, "_EmissionColor", new Color(
                0.03f,
                0.18f,
                0.3f,
                1f));
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
        }

        private static void ConfigurePlayerUI()
        {
            GameObject buildupBarPrefab = LoadRequiredAsset<GameObject>(
                k_BuildupBarPrefabPath);
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Transform popupOrganizer = FindDescendant(
                    root.transform,
                    "Popup Organizer") ?? throw new InvalidOperationException(
                        "Player UI is missing Popup Organizer.");
                Transform existing = FindDirectChild(
                    popupOrganizer,
                    "Frost Buildup Bar");
                GameObject instance = existing != null
                    ? existing.gameObject
                    : (GameObject)PrefabUtility.InstantiatePrefab(
                        buildupBarPrefab,
                        popupOrganizer);
                instance.name = "Frost Buildup Bar";
                instance.layer = 5;
                instance.transform.SetAsLastSibling();

                UIBuildupBar frostBar = instance.GetComponent<UIBuildupBar>() ??
                    throw new InvalidOperationException(
                        "Buildup Bar prefab is missing UIBuildupBar.");
                SerializedObject serializedBar = new(frostBar);
                GetRequiredProperty(serializedBar, "m_buildupType").enumValueIndex =
                    (int)Buildup.Frost;
                serializedBar.ApplyModifiedPropertiesWithoutUndo();

                Image fill = FindDescendant(instance.transform, "Fill")
                    ?.GetComponent<Image>();
                if (fill != null)
                {
                    fill.color = s_frostColor;
                }

                TMP_Text label = instance.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = "FROST";
                }

                Slider slider = instance.GetComponent<Slider>();
                if (slider != null)
                {
                    slider.value = 0f;
                }

                instance.SetActive(false);
                PlayerUIHUDManager hud = root.GetComponentInChildren<
                    PlayerUIHUDManager>(true) ?? throw new InvalidOperationException(
                        "Player UI is missing PlayerUIHUDManager.");
                UIBuildupBar[] bars = root.GetComponentsInChildren<UIBuildupBar>(
                    true);
                UIBuildupBar poisonBar = Array.Find(
                    bars,
                    bar => bar.BuildupType == Buildup.Poison);
                UIBuildupBar bleedBar = Array.Find(
                    bars,
                    bar => bar.BuildupType == Buildup.Bleed);
                if (poisonBar == null || bleedBar == null)
                {
                    throw new InvalidOperationException(
                        "Configure the Poison/Bleed status UI before Frostbite.");
                }

                SerializedObject serializedHUD = new(hud);
                SerializedProperty buildupBars = GetRequiredProperty(
                    serializedHUD,
                    "m_buildupBars");
                buildupBars.arraySize = 3;
                buildupBars.GetArrayElementAtIndex(0).objectReferenceValue =
                    poisonBar;
                buildupBars.GetArrayElementAtIndex(1).objectReferenceValue =
                    bleedBar;
                buildupBars.GetArrayElementAtIndex(2).objectReferenceValue =
                    frostBar;
                serializedHUD.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_PlayerUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidatePlayerUI()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Transform popupOrganizer = FindDescendant(
                    root.transform,
                    "Popup Organizer");
                UIBuildupBar[] bars = root.GetComponentsInChildren<UIBuildupBar>(
                    true);
                UIBuildupBar frostBar = Array.Find(
                    bars,
                    bar => bar.BuildupType == Buildup.Frost);
                PlayerUIHUDManager hud = root.GetComponentInChildren<
                    PlayerUIHUDManager>(true);
                if (popupOrganizer == null ||
                    frostBar == null ||
                    frostBar.transform.parent != popupOrganizer ||
                    frostBar.gameObject.activeSelf ||
                    hud == null)
                {
                    throw new InvalidOperationException(
                        "Player UI must contain one hidden Frost buildup bar.");
                }

                SerializedObject serializedHUD = new(hud);
                SerializedProperty buildupBars = GetRequiredProperty(
                    serializedHUD,
                    "m_buildupBars");
                if (buildupBars.arraySize != 3 ||
                    buildupBars.GetArrayElementAtIndex(2).objectReferenceValue !=
                        frostBar)
                {
                    throw new InvalidOperationException(
                        "Player UI HUD Frost buildup reference is incomplete.");
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

        private static Transform FindDirectChild(
            Transform parent,
            string childName)
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

        private static Transform FindDescendant(
            Transform parent,
            string childName)
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

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.name} is missing " +
                    $"{propertyName}.");
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

        private static void SetMaterialColor(
            Material material,
            string propertyName,
            Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetMaterialFloat(
            Material material,
            string propertyName,
            float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
