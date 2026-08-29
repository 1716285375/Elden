using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    public static class DamageSystemSetup
    {
        private const string k_PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_MainMenuScenePath = WorldScenePathLayout.MainMenuScenePath;
        private const string k_WorldScenePath = WorldScenePathLayout.MasterScenePath;
        private const string k_TagManagerPath = "ProjectSettings/TagManager.asset";
        private const string k_PhysicsManagerPath = "ProjectSettings/DynamicsManager.asset";
        private const string k_InstantEffectsFolder = "Assets/_Game/Data/Effects/Instant Effects";
        private const string k_TakeStaminaDamageEffectPath =
            k_InstantEffectsFolder + "/Take Stamina Damage Effect.asset";
        private const string k_TakeDamageEffectPath =
            k_InstantEffectsFolder + "/Take Damage Effect.asset";
        private const string k_DamageLayerName = "Damage Collider";
        private const string k_CharacterLayerName = "Player";
        private const string k_DamageableCharacterLayerName = "Damageable Character";
        private const string k_WallOfPainName = "Wall Of Pain";
        private const int k_TakeDamageEffectId = 1;
        private const float k_WallPhysicalDamage = 10f;

        private static readonly Vector3 s_wallPosition = new Vector3(0f, 1.5f, 5f);
        private static readonly Vector3 s_wallScale = new Vector3(6f, 3f, 0.5f);

        [MenuItem("Tools/Elden/Configure Damage System")]
        public static void ConfigureDamageSystem()
        {
            EnsureAssetFolder(k_InstantEffectsFolder);
            int damageLayer = EnsureLayer(k_DamageLayerName);
            int characterLayer = GetRequiredLayer(k_CharacterLayerName);
            int damageableCharacterLayer = GetRequiredLayer(
                k_DamageableCharacterLayerName);
            ConfigureLayerCollisionMatrix(
                damageLayer,
                characterLayer,
                damageableCharacterLayer);
            TakeDamageEffect takeDamageEffect = ConfigureTakeDamageEffect();
            ConfigurePlayerPrefab();
            ConfigureMainMenuScene(takeDamageEffect);
            ConfigureWorldScene(damageLayer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateDamageSystem();
            Debug.Log(
                "[DamageSystemSetup] Configured damage data, owner state, collision layers, " +
                "effect catalog, and Wall Of Pain.");
        }

        [MenuItem("Tools/Elden/Validate Damage System")]
        public static void ValidateDamageSystem()
        {
            TakeDamageEffect takeDamageEffect =
                LoadRequiredAsset<TakeDamageEffect>(k_TakeDamageEffectPath);
            ValidateDamageCalculation(takeDamageEffect);
            ValidatePlayerPrefab();
            ValidateMainMenuScene(takeDamageEffect);
            ValidateLayerCollisionMatrix();
            ValidateWorldScene();
            ValidateDamageColliderContract();
            Debug.Log(
                "[DamageSystemValidation] Damage calculation, owner state, runtime effect, " +
                "collision filtering, catalog, and Wall Of Pain are valid.");
        }

        private static TakeDamageEffect ConfigureTakeDamageEffect()
        {
            TakeDamageEffect effect =
                AssetDatabase.LoadAssetAtPath<TakeDamageEffect>(k_TakeDamageEffectPath);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<TakeDamageEffect>();
                AssetDatabase.CreateAsset(effect, k_TakeDamageEffectPath);
            }

            SerializedObject serializedEffect = new SerializedObject(effect);
            GetRequiredProperty(serializedEffect, "m_instantEffectId").intValue =
                k_TakeDamageEffectId;
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
            return effect;
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                CharacterNetworkManager networkManager =
                    GetRequiredComponent<CharacterNetworkManager>(playerRoot);
                EditorUtility.SetDirty(networkManager);
                PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigureMainMenuScene(TakeDamageEffect takeDamageEffect)
        {
            ExecuteWithScene(k_MainMenuScenePath, scene =>
            {
                WorldCharacterEffectsManager effectsManager =
                    FindComponentInScene<WorldCharacterEffectsManager>(scene);
                if (effectsManager == null)
                {
                    throw new InvalidOperationException(
                        "The Main Menu scene needs a WorldCharacterEffectsManager.");
                }

                TakeStaminaDamageEffect staminaEffect =
                    LoadRequiredAsset<TakeStaminaDamageEffect>(
                        k_TakeStaminaDamageEffectPath);
                SerializedObject serializedManager = new SerializedObject(effectsManager);
                SerializedProperty instantEffects =
                    GetRequiredProperty(serializedManager, "m_instantEffects");
                instantEffects.arraySize = Mathf.Max(
                    k_TakeDamageEffectId + 1,
                    instantEffects.arraySize);
                instantEffects.GetArrayElementAtIndex(0).objectReferenceValue =
                    staminaEffect;
                instantEffects.GetArrayElementAtIndex(k_TakeDamageEffectId)
                    .objectReferenceValue = takeDamageEffect;
                GetRequiredProperty(serializedManager, "m_takeDamageEffect")
                    .objectReferenceValue = takeDamageEffect;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(effectsManager);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            });
        }

        private static void ConfigureWorldScene(int damageLayer)
        {
            ExecuteWithScene(k_WorldScenePath, scene =>
            {
                GameObject wallOfPain = FindRootObject(scene, k_WallOfPainName);
                if (wallOfPain == null)
                {
                    wallOfPain = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wallOfPain.name = k_WallOfPainName;
                    SceneManager.MoveGameObjectToScene(wallOfPain, scene);
                }

                wallOfPain.layer = damageLayer;
                wallOfPain.transform.SetPositionAndRotation(
                    s_wallPosition,
                    Quaternion.identity);
                wallOfPain.transform.localScale = s_wallScale;

                BoxCollider boxCollider = GetOrAddComponent<BoxCollider>(wallOfPain);
                boxCollider.enabled = true;
                boxCollider.isTrigger = true;
                DamageCollider damageCollider = GetOrAddComponent<DamageCollider>(wallOfPain);
                SetFloat(damageCollider, "m_physicalDamage", k_WallPhysicalDamage);
                SetFloat(damageCollider, "m_magicDamage", 0f);
                SetFloat(damageCollider, "m_fireDamage", 0f);
                SetFloat(damageCollider, "m_lightningDamage", 0f);
                SetFloat(damageCollider, "m_holyDamage", 0f);
                SetFloat(damageCollider, "m_poiseDamage", 0f);

                EditorUtility.SetDirty(wallOfPain);
                EditorUtility.SetDirty(boxCollider);
                EditorUtility.SetDirty(damageCollider);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            });
        }

        private static int EnsureLayer(string layerName)
        {
            int existingLayer = LayerMask.NameToLayer(layerName);
            if (existingLayer >= 0)
            {
                return existingLayer;
            }

            UnityEngine.Object tagManager = LoadRequiredSettingsAsset(k_TagManagerPath);
            SerializedObject serializedTagManager = new SerializedObject(tagManager);
            SerializedProperty layers = GetRequiredProperty(serializedTagManager, "layers");
            for (int layerIndex = 8; layerIndex < layers.arraySize; layerIndex++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(layerIndex);
                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    continue;
                }

                layer.stringValue = layerName;
                serializedTagManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tagManager);
                AssetDatabase.SaveAssets();
                return layerIndex;
            }

            throw new InvalidOperationException(
                $"No empty user layer is available for '{layerName}'.");
        }

        private static void ConfigureLayerCollisionMatrix(
            int damageLayer,
            int characterLayer,
            int damageableCharacterLayer)
        {
            for (int layerIndex = 0; layerIndex < 32; layerIndex++)
            {
                Physics.IgnoreLayerCollision(
                    damageLayer,
                    layerIndex,
                    layerIndex != characterLayer &&
                    layerIndex != damageableCharacterLayer);
            }

            UnityEngine.Object physicsManager =
                LoadRequiredSettingsAsset(k_PhysicsManagerPath);
            EditorUtility.SetDirty(physicsManager);
        }

        private static void ValidateDamageCalculation(TakeDamageEffect template)
        {
            TakeDamageEffect combinedDamage = template.CreateRuntimeDamageEffect(
                null,
                1.2f,
                2.3f,
                3.4f,
                4.5f,
                5.6f,
                Vector3.one,
                7f);
            TakeDamageEffect minimumDamage = template.CreateRuntimeDamageEffect(
                null,
                0f,
                0f,
                0f,
                0f,
                0f,
                Vector3.zero,
                0f);

            try
            {
                if (template.InstantEffectId != k_TakeDamageEffectId ||
                    combinedDamage.CalculateDamage() != 17 ||
                    minimumDamage.CalculateDamage() != 1 ||
                    combinedDamage.ContactPoint != Vector3.one ||
                    !Mathf.Approximately(combinedDamage.PoiseDamage, 7f) ||
                    template.FinalDamageDealt != 0)
                {
                    throw new InvalidOperationException(
                        "Damage must combine all types, round once, enforce one, " +
                        "and preserve the authored template.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(combinedDamage);
                UnityEngine.Object.DestroyImmediate(minimumDamage);
            }
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                CharacterNetworkManager networkManager =
                    GetRequiredComponent<CharacterNetworkManager>(playerRoot);
                GetRequiredComponent<CharacterEffectsManager>(playerRoot);
                if (networkManager.IsDead.ReadPerm !=
                        NetworkVariableReadPermission.Everyone ||
                    networkManager.IsDead.WritePerm !=
                        NetworkVariableWritePermission.Owner ||
                    networkManager.IsDead.Value)
                {
                    throw new InvalidOperationException(
                        "Player death state must be owner-written, universally read, " +
                        "and integrated with Character Effects.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateMainMenuScene(TakeDamageEffect takeDamageEffect)
        {
            ExecuteWithScene(k_MainMenuScenePath, scene =>
            {
                WorldCharacterEffectsManager effectsManager =
                    FindComponentInScene<WorldCharacterEffectsManager>(scene);
                if (effectsManager == null ||
                    effectsManager.InstantEffects.Count <= k_TakeDamageEffectId ||
                    effectsManager.InstantEffects[k_TakeDamageEffectId] !=
                        takeDamageEffect ||
                    effectsManager.TakeDamageEffect != takeDamageEffect ||
                    !effectsManager.TryGetInstantEffect(
                        k_TakeDamageEffectId,
                        out InstantCharacterEffect catalogEffect) ||
                    catalogEffect != takeDamageEffect)
                {
                    throw new InvalidOperationException(
                        "The world effect catalog must expose TakeDamageEffect at ID 1.");
                }
            });
        }

        private static void ValidateLayerCollisionMatrix()
        {
            int damageLayer = GetRequiredLayer(k_DamageLayerName);
            int characterLayer = GetRequiredLayer(k_CharacterLayerName);
            int damageableCharacterLayer = GetRequiredLayer(
                k_DamageableCharacterLayerName);
            for (int layerIndex = 0; layerIndex < 32; layerIndex++)
            {
                bool isCollisionEnabled =
                    !Physics.GetIgnoreLayerCollision(damageLayer, layerIndex);
                bool shouldCollide = layerIndex == characterLayer ||
                    layerIndex == damageableCharacterLayer;
                if (isCollisionEnabled != shouldCollide)
                {
                    throw new InvalidOperationException(
                        "Damage Collider must collide exclusively with Player and " +
                        "Damageable Character layers. " +
                        $"Unexpected layer {layerIndex} ({LayerMask.LayerToName(layerIndex)}), " +
                        $"collision enabled: {isCollisionEnabled}.");
                }
            }
        }

        private static void ValidateWorldScene()
        {
            ExecuteWithScene(k_WorldScenePath, scene =>
            {
                GameObject wallOfPain = FindRootObject(scene, k_WallOfPainName);
                BoxCollider boxCollider = wallOfPain?.GetComponent<BoxCollider>();
                DamageCollider damageCollider = wallOfPain?.GetComponent<DamageCollider>();
                if (wallOfPain == null ||
                    wallOfPain.layer != GetRequiredLayer(k_DamageLayerName) ||
                    wallOfPain.transform.position != s_wallPosition ||
                    wallOfPain.transform.localScale != s_wallScale ||
                    boxCollider == null ||
                    !boxCollider.enabled ||
                    !boxCollider.isTrigger ||
                    damageCollider == null ||
                    !Mathf.Approximately(
                        GetFloat(damageCollider, "m_physicalDamage"),
                        k_WallPhysicalDamage))
                {
                    throw new InvalidOperationException(
                        "Wall Of Pain must be a visible 10 Physical Damage trigger.");
                }
            });
        }

        private static void ValidateDamageColliderContract()
        {
            const BindingFlags instanceMethods =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;
            MethodInfo damageMethod = typeof(DamageCollider).GetMethod(
                "Damage",
                instanceMethods);
            if (damageMethod == null ||
                !damageMethod.IsVirtual ||
                typeof(DamageCollider).GetMethod("OpenDamageCollider", instanceMethods) == null ||
                typeof(DamageCollider).GetMethod("CloseDamageCollider", instanceMethods) == null ||
                typeof(DamageCollider).GetMethod("Update", instanceMethods) != null)
            {
                throw new InvalidOperationException(
                    "DamageCollider needs a virtual damage hook, explicit hit-window API, " +
                    "and no Update polling.");
            }
        }

        private static int GetRequiredLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0
                ? layer
                : throw new InvalidOperationException(
                    $"Could not find the required '{layerName}' layer.");
        }

        private static void ExecuteWithScene(string scenePath, Action<Scene> action)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
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

        private static UnityEngine.Object LoadRequiredSettingsAsset(string assetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            return assets.Length > 0
                ? assets[0]
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static T GetRequiredComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{gameObject.name} needs a {typeof(T).Name}.");
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void SetFloat(
            Component component,
            string propertyName,
            float value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static float GetFloat(Component component, string propertyName)
        {
            return GetRequiredProperty(
                new SerializedObject(component),
                propertyName).floatValue;
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
    }
}
