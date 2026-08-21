using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    public static class CharacterEffectsSystemSetup
    {
        private const string k_PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_MainMenuScenePath = "Assets/Scenes/Scene_Main_Menu_01.unity";
        private const string k_InstantEffectsFolder = "Assets/Data/Effects/Instant Effects";
        private const string k_TakeStaminaDamageEffectPath =
            k_InstantEffectsFolder + "/Take Stamina Damage Effect.asset";
        private const string k_WorldManagerName = "World Character Effects Manager";
        private const string k_LoadingGroundName = "Character Loading Ground";
        private const float k_DefaultStaminaDamage = 25f;

        [MenuItem("Tools/Elden/Configure Character Effects")]
        public static void ConfigureCharacterEffects()
        {
            EnsureAssetFolder(k_InstantEffectsFolder);
            TakeStaminaDamageEffect staminaDamageEffect = ConfigureStaminaDamageEffect();
            ConfigurePlayerPrefab(staminaDamageEffect);
            ConfigureMainMenuScene(staminaDamageEffect);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateCharacterEffects();
            Debug.Log(
                "[CharacterEffectsSystemSetup] Configured the instant effect catalog, " +
                "Player manager, and loading ground.");
        }

        [MenuItem("Tools/Elden/Validate Character Effects")]
        public static void ValidateCharacterEffects()
        {
            TakeStaminaDamageEffect staminaDamageEffect =
                LoadRequiredAsset<TakeStaminaDamageEffect>(k_TakeStaminaDamageEffectPath);
            ValidateStaminaDamageEffect(staminaDamageEffect);
            ValidatePlayerPrefab(staminaDamageEffect);
            ValidateMainMenuScene(staminaDamageEffect);
            ValidateRuntimeCopyIsolation(staminaDamageEffect);
            ValidateInheritanceStructure();
            Debug.Log(
                "[CharacterEffectsValidation] Effect data, runtime isolation, catalog, " +
                "Player manager, and loading ground are valid.");
        }

        private static TakeStaminaDamageEffect ConfigureStaminaDamageEffect()
        {
            TakeStaminaDamageEffect effect =
                AssetDatabase.LoadAssetAtPath<TakeStaminaDamageEffect>(
                    k_TakeStaminaDamageEffectPath);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<TakeStaminaDamageEffect>();
                AssetDatabase.CreateAsset(effect, k_TakeStaminaDamageEffectPath);
            }

            SerializedObject serializedEffect = new SerializedObject(effect);
            SetFloat(serializedEffect, "m_staminaDamage", k_DefaultStaminaDamage);
            SetInteger(serializedEffect, "m_instantEffectId", 0);
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
            return effect;
        }

        private static void ConfigurePlayerPrefab(TakeStaminaDamageEffect staminaDamageEffect)
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                CharacterEffectsManager existingManager =
                    playerRoot.GetComponent<CharacterEffectsManager>();
                if (existingManager != null && existingManager is not PlayerEffectsManager)
                {
                    UnityEngine.Object.DestroyImmediate(existingManager);
                }

                PlayerEffectsManager effectsManager =
                    GetOrAddComponent<PlayerEffectsManager>(playerRoot);
                SetObjectReference(effectsManager, "m_effectToTest", staminaDamageEffect);
                EditorUtility.SetDirty(effectsManager);
                PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigureMainMenuScene(
            TakeStaminaDamageEffect staminaDamageEffect)
        {
            ExecuteWithMainMenuScene(scene =>
            {
                WorldCharacterEffectsManager effectsManager =
                    FindComponentInScene<WorldCharacterEffectsManager>(scene);
                if (effectsManager == null)
                {
                    GameObject managerObject = new GameObject(k_WorldManagerName);
                    SceneManager.MoveGameObjectToScene(managerObject, scene);
                    effectsManager = managerObject.AddComponent<WorldCharacterEffectsManager>();
                }

                effectsManager.gameObject.name = k_WorldManagerName;
                SerializedObject serializedManager = new SerializedObject(effectsManager);
                SerializedProperty instantEffects =
                    GetRequiredProperty(serializedManager, "m_instantEffects");
                instantEffects.arraySize = Mathf.Max(1, instantEffects.arraySize);
                instantEffects.GetArrayElementAtIndex(0).objectReferenceValue =
                    staminaDamageEffect;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(effectsManager);

                ConfigureLoadingGround(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            });
        }

        private static void ConfigureLoadingGround(Scene scene)
        {
            GameObject loadingGround = FindRootObject(scene, k_LoadingGroundName);
            if (loadingGround == null)
            {
                loadingGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
                loadingGround.name = k_LoadingGroundName;
                SceneManager.MoveGameObjectToScene(loadingGround, scene);
            }

            loadingGround.transform.SetPositionAndRotation(
                new Vector3(0f, -0.05f, 0f),
                Quaternion.identity);
            loadingGround.transform.localScale = new Vector3(20f, 1f, 20f);
            loadingGround.SetActive(true);

            MeshCollider meshCollider = GetOrAddComponent<MeshCollider>(loadingGround);
            MeshFilter meshFilter = loadingGround.GetComponent<MeshFilter>();
            if (meshCollider.sharedMesh == null && meshFilter != null)
            {
                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }

            meshCollider.enabled = true;
            MeshRenderer meshRenderer = loadingGround.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                UnityEngine.Object.DestroyImmediate(meshRenderer);
            }

            EditorUtility.SetDirty(loadingGround);
            EditorUtility.SetDirty(meshCollider);
        }

        private static void ValidateStaminaDamageEffect(
            TakeStaminaDamageEffect staminaDamageEffect)
        {
            if (staminaDamageEffect.InstantEffectId != 0 ||
                !Mathf.Approximately(
                    staminaDamageEffect.StaminaDamage,
                    k_DefaultStaminaDamage))
            {
                throw new InvalidOperationException(
                    "Take Stamina Damage Effect must use ID 0 and remove 25 Stamina.");
            }
        }

        private static void ValidatePlayerPrefab(
            TakeStaminaDamageEffect staminaDamageEffect)
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                PlayerEffectsManager effectsManager =
                    playerRoot.GetComponent<PlayerEffectsManager>();
                if (effectsManager == null ||
                    playerRoot.GetComponents<CharacterEffectsManager>().Length != 1)
                {
                    throw new InvalidOperationException(
                        "The Player prefab must contain exactly one PlayerEffectsManager.");
                }

                ValidateObjectReference(
                    effectsManager,
                    "m_effectToTest",
                    staminaDamageEffect);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateMainMenuScene(
            TakeStaminaDamageEffect staminaDamageEffect)
        {
            ExecuteWithMainMenuScene(scene =>
            {
                WorldCharacterEffectsManager effectsManager =
                    FindComponentInScene<WorldCharacterEffectsManager>(scene);
                if (effectsManager == null ||
                    effectsManager.InstantEffects.Count < 1 ||
                    effectsManager.InstantEffects[0] != staminaDamageEffect ||
                    !effectsManager.TryGetInstantEffect(0, out InstantCharacterEffect effect) ||
                    effect != staminaDamageEffect)
                {
                    throw new InvalidOperationException(
                        "The Main Menu effect catalog must contain the Stamina effect at ID 0.");
                }

                GameObject loadingGround = FindRootObject(scene, k_LoadingGroundName);
                MeshCollider meshCollider = loadingGround?.GetComponent<MeshCollider>();
                if (loadingGround == null ||
                    !loadingGround.activeSelf ||
                    meshCollider == null ||
                    !meshCollider.enabled ||
                    meshCollider.sharedMesh == null ||
                    loadingGround.GetComponent<MeshRenderer>() != null)
                {
                    throw new InvalidOperationException(
                        "The Main Menu needs an active, collider-only loading ground.");
                }
            });
        }

        private static void ValidateRuntimeCopyIsolation(
            TakeStaminaDamageEffect staminaDamageEffect)
        {
            InstantCharacterEffect runtimeEffect = staminaDamageEffect.CreateRuntimeInstance();

            try
            {
                SerializedObject serializedRuntimeEffect = new SerializedObject(runtimeEffect);
                SetFloat(serializedRuntimeEffect, "m_staminaDamage", 50f);
                serializedRuntimeEffect.ApplyModifiedPropertiesWithoutUndo();

                TakeStaminaDamageEffect runtimeStaminaEffect =
                    runtimeEffect as TakeStaminaDamageEffect;
                if (runtimeStaminaEffect == null ||
                    ReferenceEquals(runtimeStaminaEffect, staminaDamageEffect) ||
                    !Mathf.Approximately(runtimeStaminaEffect.StaminaDamage, 50f) ||
                    !Mathf.Approximately(
                        staminaDamageEffect.StaminaDamage,
                        k_DefaultStaminaDamage))
                {
                    throw new InvalidOperationException(
                        "Runtime effect changes must not modify the authored effect asset.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeEffect);
            }
        }

        private static void ValidateInheritanceStructure()
        {
            if (!typeof(CharacterEffectsManager).IsAssignableFrom(
                    typeof(PlayerEffectsManager)))
            {
                throw new InvalidOperationException(
                    "PlayerEffectsManager must inherit CharacterEffectsManager.");
            }
        }

        private static void ExecuteWithMainMenuScene(Action<Scene> action)
        {
            Scene scene = SceneManager.GetSceneByPath(k_MainMenuScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(
                    k_MainMenuScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                action(scene);
            }
            finally
            {
                if (!wasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                T component = rootObject.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static GameObject FindRootObject(Scene scene, string objectName)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == objectName)
                {
                    return rootObject;
                }
            }

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string currentPath = "Assets";
            string[] pathParts = folderPath.Split('/');
            for (int pathIndex = 1; pathIndex < pathParts.Length; pathIndex++)
            {
                string nextPath = $"{currentPath}/{pathParts[pathIndex]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, pathParts[pathIndex]);
                }

                currentPath = nextPath;
            }
        }

        private static void SetObjectReference(
            Component component,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateObjectReference(
            Component component,
            string propertyName,
            UnityEngine.Object expectedValue)
        {
            SerializedProperty property =
                GetRequiredProperty(new SerializedObject(component), propertyName);
            if (property.objectReferenceValue != expectedValue)
            {
                throw new InvalidOperationException(
                    $"{component.GetType().Name}.{propertyName} is not configured.");
            }
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
        }

        private static void SetInteger(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).intValue = value;
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"Could not find {serializedObject.targetObject.GetType().Name}." +
                    propertyName);
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }
    }
}
