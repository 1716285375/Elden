using System;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP42 network AI spawner architecture.</summary>
    public static class AICharacterSpawnerSystemSetup
    {
        private const int k_ExpectedSpawnerCount = 3;
        private const int k_PersistenceValidationBossID = 42001;
        private const string k_AICharacterPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_WorldAIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/World AI Manager.prefab";
        private const string k_WorldScenePath =
            WorldScenePathLayout.MasterScenePath;

        [MenuItem("Tools/Elden/Configure AI Character Spawners")]
        public static void ConfigureAICharacterSpawners()
        {
            ConfigureManagerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateAICharacterSpawners();
            Debug.Log(
                "[AICharacterSpawnerSystemSetup] Converted AI spawn points to " +
                "server-authoritative character spawners.");
        }

        [MenuItem("Tools/Elden/Validate AI Character Spawners")]
        public static void ValidateAICharacterSpawners()
        {
            ValidateRuntimeContracts();
            ValidateBossPersistence();
            ValidateManagerPrefab();
            ValidateWorldScene();
            Debug.Log(
                "[AICharacterSpawnerSystemValidation] Spawner registration, " +
                "network prefabs, and boss persistence are valid.");
        }

        private static void ConfigureManagerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_WorldAIManagerPrefabPath);
            try
            {
                if (root.GetComponent<WorldAIManager>() == null)
                {
                    root.AddComponent<WorldAIManager>();
                }

                GameObject aiCharacterPrefab = LoadRequiredAsset<GameObject>(
                    k_AICharacterPrefabPath);
                Transform[] spawnTransforms = root.GetComponentsInChildren<Transform>(
                        true)
                    .Where(transform =>
                        transform.parent == root.transform &&
                        transform.name.StartsWith(
                            "AI Spawn Point",
                            StringComparison.Ordinal))
                    .OrderBy(transform => transform.GetSiblingIndex())
                    .ToArray();
                if (spawnTransforms.Length != k_ExpectedSpawnerCount)
                {
                    throw new InvalidOperationException(
                        "The World AI Manager must keep its three authored spawn points.");
                }

                foreach (Transform spawnTransform in spawnTransforms)
                {
                    AISpawnPoint legacySpawnPoint =
                        spawnTransform.GetComponent<AISpawnPoint>();
                    if (legacySpawnPoint != null)
                    {
                        UnityEngine.Object.DestroyImmediate(
                            legacySpawnPoint,
                            true);
                    }

                    AICharacterSpawner characterSpawner =
                        spawnTransform.GetComponent<AICharacterSpawner>() ??
                        spawnTransform.gameObject.AddComponent<AICharacterSpawner>();
                    SerializedObject serializedSpawner = new SerializedObject(
                        characterSpawner);
                    GetRequiredProperty(
                        serializedSpawner,
                        "m_characterGameObject").objectReferenceValue =
                        aiCharacterPrefab;
                    GetRequiredProperty(serializedSpawner, "m_bossID").intValue = 0;
                    serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(characterSpawner);
                }

                if (PrefabUtility.SaveAsPrefabAsset(
                        root,
                        k_WorldAIManagerPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        $"Could not save {k_WorldAIManagerPrefabPath}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateRuntimeContracts()
        {
            BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;
            MethodInfo spawnMethod = typeof(AICharacterSpawner).GetMethod(
                nameof(AICharacterSpawner.AttemptToSpawnCharacter),
                publicInstance);
            MethodInfo registerMethod = typeof(WorldAIManager).GetMethod(
                nameof(WorldAIManager.RegisterSpawner),
                publicInstance);
            MethodInfo setOriginMethod = typeof(AICharacterManager).GetMethod(
                nameof(AICharacterManager.SetOriginSpawner),
                publicInstance);
            MethodInfo recordBossMethod = typeof(WorldSaveGameManager).GetMethod(
                nameof(WorldSaveGameManager.RecordBossProgress),
                publicInstance);
            if (spawnMethod == null ||
                spawnMethod.ReturnType != typeof(AICharacterManager) ||
                registerMethod == null ||
                setOriginMethod == null ||
                recordBossMethod == null)
            {
                throw new InvalidOperationException(
                    "The EP42 runtime spawner contracts are incomplete.");
            }

            GameObject aiCharacterPrefab = LoadRequiredAsset<GameObject>(
                k_AICharacterPrefabPath);
            if (aiCharacterPrefab.GetComponent<NetworkObject>() == null ||
                aiCharacterPrefab.GetComponent<AICharacterManager>() == null)
            {
                throw new InvalidOperationException(
                    "AI spawners require a NetworkObject AI prefab.");
            }
        }

        private static void ValidateBossPersistence()
        {
            CharacterSaveData saveData = new CharacterSaveData();
            if (saveData.GetBossProgress(k_PersistenceValidationBossID) !=
                    BossProgressState.Dormant ||
                !saveData.SetBossProgress(
                    k_PersistenceValidationBossID,
                    BossProgressState.Awakened) ||
                saveData.SetBossProgress(
                    k_PersistenceValidationBossID,
                    BossProgressState.Dormant) ||
                !saveData.SetBossProgress(
                    k_PersistenceValidationBossID,
                    BossProgressState.Defeated) ||
                !saveData.IsBossDefeated(k_PersistenceValidationBossID))
            {
                throw new InvalidOperationException(
                    "Boss progress must advance monotonically through three states.");
            }

            string json = JsonUtility.ToJson(saveData);
            CharacterSaveData restoredData =
                JsonUtility.FromJson<CharacterSaveData>(json);
            if (restoredData == null ||
                !restoredData.IsBossDefeated(k_PersistenceValidationBossID))
            {
                throw new InvalidOperationException(
                    "Boss progress must survive a JSON save round trip.");
            }
        }

        private static void ValidateManagerPrefab()
        {
            GameObject managerPrefab = LoadRequiredAsset<GameObject>(
                k_WorldAIManagerPrefabPath);
            if (managerPrefab.GetComponent<WorldAIManager>() == null)
            {
                throw new InvalidOperationException(
                    "The World AI Manager prefab is missing its manager component.");
            }

            GameObject aiCharacterPrefab = LoadRequiredAsset<GameObject>(
                k_AICharacterPrefabPath);
            AICharacterSpawner[] spawners = managerPrefab
                .GetComponentsInChildren<AICharacterSpawner>(true)
                .Where(spawner => !spawner.IsBoss)
                .ToArray();
            if (spawners.Length != k_ExpectedSpawnerCount ||
                managerPrefab.GetComponentsInChildren<AISpawnPoint>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    "Every legacy AI spawn point must be converted to a character spawner.");
            }

            foreach (AICharacterSpawner spawner in spawners)
            {
                SerializedObject serializedSpawner = new SerializedObject(spawner);
                if (GetRequiredProperty(
                        serializedSpawner,
                        "m_characterGameObject").objectReferenceValue !=
                        aiCharacterPrefab ||
                    spawner.BossID != 0)
                {
                    throw new InvalidOperationException(
                        $"{spawner.name} has invalid normal-enemy spawn data.");
                }
            }
        }

        private static void ValidateWorldScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_WorldScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(
                    k_WorldScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                WorldAIManager manager = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<WorldAIManager>())
                    .FirstOrDefault(candidate => candidate != null);
                int normalSpawnerCount = manager != null
                    ? manager.GetComponentsInChildren<AICharacterSpawner>(true)
                        .Count(spawner => !spawner.IsBoss)
                    : 0;
                if (manager == null || normalSpawnerCount != k_ExpectedSpawnerCount)
                {
                    throw new InvalidOperationException(
                        "The World Scene must expose three normal AI spawners.");
                }
            }
            finally
            {
                if (openedForValidation)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} is missing " +
                    $"serialized property {propertyName}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }
    }
}
