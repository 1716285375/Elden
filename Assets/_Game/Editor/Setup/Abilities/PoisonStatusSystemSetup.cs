using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the authored assets required by EP139-140.</summary>
    public static class PoisonStatusSystemSetup
    {
        private const string k_PoisonedEffectPath =
            "Assets/Resources/Effects/Poisoned Effect.asset";
        private const string k_PoisonedVFXPath =
            "Assets/Resources/Effects/Poisoned VFX.prefab";
        private const string k_StatusWarningPath =
            "Assets/_Game/Prefabs/UI/Status Effect Warning.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        private static readonly Color s_poisonColor =
            new(0.34f, 0.62f, 0.2f, 1f);
        private static readonly Color s_backgroundColor =
            new(0.025f, 0.025f, 0.02f, 0.94f);
        private static readonly Color s_borderColor =
            new(0.52f, 0.42f, 0.24f, 0.95f);

        [MenuItem("Tools/Elden/Configure Poison Status System")]
        public static void ConfigurePoisonStatusSystem()
        {
            ConfigurePoisonedEffect();
            ConfigurePoisonedVFX();
            UIStatusEffectWarning warningPrefab = ConfigureStatusWarning();
            ConfigurePlayerUI(warningPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidatePoisonStatusSystem();
            Debug.Log(
                "[PoisonStatusSystemSetup] Configured Poison damage, VFX, " +
                "Health colors, and owner warning UI.");
        }

        [MenuItem("Tools/Elden/Validate Poison Status System")]
        public static void ValidatePoisonStatusSystem()
        {
            PoisonedEffect poisonedEffect = LoadRequiredAsset<PoisonedEffect>(
                k_PoisonedEffectPath);
            if (poisonedEffect.TimedEffectID != 2 ||
                !Mathf.Approximately(
                    poisonedEffect.DefaultTimeLengthOnEffect,
                    120f) ||
                !Mathf.Approximately(poisonedEffect.PoisonDamage, 10f))
            {
                throw new InvalidOperationException(
                    "The Poisoned Effect must use ID 2, last 120 seconds, " +
                    "and deal 10 damage per tick.");
            }

            GameObject poisonedVFX = LoadRequiredAsset<GameObject>(
                k_PoisonedVFXPath);
            if (poisonedVFX.GetComponent<ParticleSystem>() == null)
            {
                throw new InvalidOperationException(
                    "Poisoned VFX must contain a ParticleSystem.");
            }

            GameObject warning = LoadRequiredAsset<GameObject>(
                k_StatusWarningPath);
            if (warning.activeSelf ||
                warning.GetComponent<UIStatusEffectWarning>() == null ||
                warning.GetComponent<CanvasGroup>() == null)
            {
                throw new InvalidOperationException(
                    "Status warning prefab must contain its controller and start hidden.");
            }

            ValidatePlayerUI();
            Debug.Log(
                "[PoisonStatusSystemValidation] EP139-140 authored assets are valid.");
        }

        private static void ConfigurePoisonedEffect()
        {
            PoisonedEffect effect = AssetDatabase.LoadAssetAtPath<PoisonedEffect>(
                k_PoisonedEffectPath);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<PoisonedEffect>();
                AssetDatabase.CreateAsset(effect, k_PoisonedEffectPath);
            }

            SerializedObject serializedEffect = new(effect);
            GetRequiredProperty(serializedEffect, "m_timedEffectID").intValue = 2;
            GetRequiredProperty(
                serializedEffect,
                "m_defaultTimeLengthOnEffect").floatValue = 120f;
            GetRequiredProperty(serializedEffect, "m_poisonDamage").floatValue =
                10f;
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
        }

        private static void ConfigurePoisonedVFX()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_PoisonedVFXPath);
            GameObject root = prefab != null
                ? PrefabUtility.LoadPrefabContents(k_PoisonedVFXPath)
                : new GameObject("Poisoned VFX", typeof(ParticleSystem));

            try
            {
                root.name = "Poisoned VFX";
                ParticleSystem particles = root.GetComponent<ParticleSystem>() ??
                    root.AddComponent<ParticleSystem>();
                ConfigureParticles(particles);
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, k_PoisonedVFXPath);
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

        private static void ConfigureParticles(ParticleSystem particles)
        {
            ParticleSystem.MainModule main = particles.main;
            main.duration = 2f;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.18f, 0.48f, 0.08f, 0.2f),
                new Color(0.52f, 0.8f, 0.18f, 0.55f));
            main.maxParticles = 64;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 14f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.65f;
            shape.radiusThickness = 1f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fadeGradient = new();
            fadeGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(s_poisonColor, 0f),
                    new GradientColorKey(s_poisonColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.55f, 0.25f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fadeGradient;

            ParticleSystemRenderer renderer = particles.GetComponent<
                ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 10;
        }

        private static UIStatusEffectWarning ConfigureStatusWarning()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_StatusWarningPath);
            GameObject root = prefab != null
                ? PrefabUtility.LoadPrefabContents(k_StatusWarningPath)
                : CreateStatusWarningHierarchy();

            try
            {
                ConfigureStatusWarningHierarchy(root);
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    k_StatusWarningPath);
                return savedPrefab.GetComponent<UIStatusEffectWarning>();
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

        private static GameObject CreateStatusWarningHierarchy()
        {
            GameObject root = new(
                "Status Effect Warning",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(CanvasGroup),
                typeof(LayoutElement),
                typeof(UIStatusEffectWarning));
            root.layer = 5;

            GameObject label = new(
                "Status Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            label.layer = 5;
            label.transform.SetParent(root.transform, false);
            return root;
        }

        private static void ConfigureStatusWarningHierarchy(GameObject root)
        {
            root.name = "Status Effect Warning";
            root.layer = 5;
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(420f, 56f);

            Image background = root.GetComponent<Image>();
            background.color = s_backgroundColor;
            background.raycastTarget = false;
            Outline outline = root.GetComponent<Outline>();
            outline.effectColor = s_borderColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = 420f;
            layout.preferredHeight = 56f;

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            TextMeshProUGUI statusText = root.GetComponentInChildren<
                TextMeshProUGUI>(true);
            Stretch(statusText.rectTransform, new Vector2(12f, 4f),
                new Vector2(-12f, -4f));
            statusText.text = "POISONED";
            statusText.font = TMP_Settings.defaultFontAsset;
            statusText.fontSize = 30f;
            statusText.fontStyle = FontStyles.SmallCaps;
            statusText.color = s_poisonColor;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.raycastTarget = false;

            UIStatusEffectWarning warning =
                root.GetComponent<UIStatusEffectWarning>();
            SerializedObject serializedWarning = new(warning);
            GetRequiredProperty(serializedWarning, "m_canvasGroup")
                .objectReferenceValue = canvasGroup;
            GetRequiredProperty(serializedWarning, "m_statusText")
                .objectReferenceValue = statusText;
            GetRequiredProperty(serializedWarning, "m_stayDuration").floatValue =
                2f;
            GetRequiredProperty(serializedWarning, "m_fadeDuration").floatValue =
                1f;
            serializedWarning.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
        }

        private static void ConfigurePlayerUI(UIStatusEffectWarning warningPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Transform organizer = FindDescendant(
                    root.transform,
                    "Popup Organizer") ?? throw new InvalidOperationException(
                    "Player UI is missing Popup Organizer.");
                PlayerUIPopUpManager popupManager =
                    root.GetComponentInChildren<PlayerUIPopUpManager>(true) ??
                    throw new InvalidOperationException(
                        "Player UI is missing PlayerUIPopUpManager.");

                SerializedObject serializedPopup = new(popupManager);
                GetRequiredProperty(serializedPopup, "m_popupOrganizer")
                    .objectReferenceValue = organizer;
                GetRequiredProperty(serializedPopup, "m_statusEffectWarningPrefab")
                    .objectReferenceValue = warningPrefab;
                serializedPopup.ApplyModifiedPropertiesWithoutUndo();
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
                PlayerUIPopUpManager popupManager =
                    root.GetComponentInChildren<PlayerUIPopUpManager>(true);
                if (popupManager == null)
                {
                    throw new InvalidOperationException(
                        "Player UI is missing PlayerUIPopUpManager.");
                }

                SerializedObject serializedPopup = new(popupManager);
                if (GetRequiredProperty(serializedPopup, "m_popupOrganizer")
                        .objectReferenceValue == null ||
                    GetRequiredProperty(
                        serializedPopup,
                        "m_statusEffectWarningPrefab").objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        "Player UI status warning references are incomplete.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
    }
}
