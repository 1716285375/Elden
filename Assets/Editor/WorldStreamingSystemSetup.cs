using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the EP119-123 additive world Scene scaffold.</summary>
    public static class WorldStreamingSystemSetup
    {
        private const string k_WorldScenePath =
            "Assets/Scenes/Scene_World_01.unity";
        private const string k_AreaSceneFolder =
            "Assets/Scenes/World Areas";
        private const string k_BakingSetPath =
            "Assets/Settings/World Streaming Probe Volume Baking Set.asset";
        private const string k_TriggerPrefabPath =
            "Assets/Data/Prefabs/World Streaming/Area Load Trigger.prefab";
        private const string k_ManagerName = "World Streaming Manager";
        private const string k_SpawnTriggerName = "Spawn Area Load Trigger";
        private const string k_ProbeVolumeName =
            "World Streaming Adaptive Probe Volume";
        private const string k_PerSceneDataName =
            "World Streaming Probe Volume Data";

        private static readonly string[] s_areaSceneIDs =
        {
            "Area_01_Sub_Area_00",
            "Area_01_Sub_Area_01",
            "Area_01_Sub_Area_02",
            "Area_01_Sub_Area_03",
            "Area_01_Sub_Area_04"
        };

        /// <summary>Creates all area Scenes, runtime roots, build entries, and APV authoring assets.</summary>
        [MenuItem("Tools/Elden/Configure World Streaming System")]
        public static void ConfigureWorldStreamingSystem()
        {
            try
            {
                EnsureAssetFolder(k_AreaSceneFolder);
                EnsureAreaScenes();
                ConfigureBuildSettings();
                ProbeVolumeBakingSet bakingSet = ConfigureBakingSet();
                ConfigureStreamingScenes(bakingSet);
                ConfigureTriggerPrefab();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                ValidateWorldStreamingSystem();
                Debug.Log(
                    "[WorldStreamingSystemSetup] Configured EP119-123 persistent " +
                    "world, five additive regions, Host queues, and APV authoring.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        /// <summary>Validates the additive Scene graph and its persistent runtime services.</summary>
        [MenuItem("Tools/Elden/Validate World Streaming System")]
        public static void ValidateWorldStreamingSystem()
        {
            ValidateSceneAssetsAndBuildSettings();
            ValidateBakingSet();
            ValidatePersistentWorldScene();
            ValidateTriggerPrefab();
            Debug.Log(
                "[WorldStreamingSystemValidation] EP119-123 world streaming " +
                "configuration is valid.");
        }

        private static void EnsureAreaScenes()
        {
            foreach (string sceneID in s_areaSceneIDs)
            {
                string scenePath = GetAreaScenePath(sceneID);
                if (File.Exists(scenePath))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
                new GameObject($"World Area - {sceneID}");
                EditorSceneManager.SaveScene(scene, scenePath);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ConfigureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes =
                EditorBuildSettings.scenes.ToList();
            foreach (string sceneID in s_areaSceneIDs)
            {
                string scenePath = GetAreaScenePath(sceneID);
                EditorBuildSettingsScene existing = scenes.FirstOrDefault(
                    scene => scene.path == scenePath);
                if (existing != null)
                {
                    existing.enabled = true;
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static ProbeVolumeBakingSet ConfigureBakingSet()
        {
            ProbeVolumeBakingSet bakingSet =
                AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(
                    k_BakingSetPath);
            if (bakingSet == null)
            {
                bakingSet = ScriptableObject.CreateInstance<ProbeVolumeBakingSet>();
                AssetDatabase.CreateAsset(bakingSet, k_BakingSetPath);
            }

            foreach (string scenePath in GetStreamingScenePaths())
            {
                string sceneGUID = AssetDatabase.AssetPathToGUID(scenePath);
                if (!bakingSet.sceneGUIDs.Contains(sceneGUID))
                {
                    bakingSet.TryAddScene(sceneGUID);
                }
            }

            SerializedObject serializedSet = new SerializedObject(bakingSet);
            SetBoolean(serializedSet, "singleSceneMode", false);
            SetBoolean(
                serializedSet,
                "settings.virtualOffsetSettings.useVirtualOffset",
                true);
            SetFloat(
                serializedSet,
                "settings.virtualOffsetSettings.searchMultiplier",
                0.2f);
            SetFloat(
                serializedSet,
                "settings.virtualOffsetSettings.outOfGeoOffset",
                0.01f);
            serializedSet.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bakingSet);
            return bakingSet;
        }

        private static void ConfigureStreamingScenes(
            ProbeVolumeBakingSet bakingSet)
        {
            List<Scene> scenes = new();
            try
            {
                foreach (string scenePath in GetStreamingScenePaths())
                {
                    Scene scene = SceneManager.GetSceneByPath(scenePath);
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        scene = EditorSceneManager.OpenScene(
                            scenePath,
                            OpenSceneMode.Additive);
                    }

                    scenes.Add(scene);
                    ConfigurePerSceneProbeData(scene, bakingSet, scenePath);
                }

                Scene worldScene = scenes.First(
                    scene => scene.path == k_WorldScenePath);
                ConfigurePersistentWorldRoot(worldScene);
                ConfigureProbeVolume(worldScene, scenes);
                foreach (Scene scene in scenes)
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                foreach (Scene scene in scenes.Where(scene =>
                    scene.IsValid() && scene.isLoaded))
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ConfigurePerSceneProbeData(
            Scene scene,
            ProbeVolumeBakingSet bakingSet,
            string scenePath)
        {
            GameObject dataObject = FindRoot(scene, k_PerSceneDataName);
            if (dataObject == null)
            {
                dataObject = new GameObject(k_PerSceneDataName);
                SceneManager.MoveGameObjectToScene(dataObject, scene);
            }

            dataObject.hideFlags = HideFlags.HideInHierarchy;
            ProbeVolumePerSceneData perSceneData =
                GetOrAddComponent<ProbeVolumePerSceneData>(dataObject);
            SerializedObject serializedData = new SerializedObject(perSceneData);
            SetObjectReference(
                serializedData,
                "serializedBakingSet",
                bakingSet);
            SetString(
                serializedData,
                "sceneGUID",
                AssetDatabase.AssetPathToGUID(scenePath));
            serializedData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(perSceneData);
        }

        private static void ConfigurePersistentWorldRoot(Scene worldScene)
        {
            GameObject managerObject = FindRoot(worldScene, k_ManagerName);
            if (managerObject == null)
            {
                managerObject = new GameObject(k_ManagerName);
                SceneManager.MoveGameObjectToScene(managerObject, worldScene);
            }

            GetOrAddComponent<NetworkObject>(managerObject);
            GetOrAddComponent<WorldSceneManager>(managerObject);
            GetOrAddComponent<WorldSceneSubSceneManager>(managerObject);
            ConfigureSpawnAreaTrigger(worldScene, managerObject.transform);
            EditorUtility.SetDirty(managerObject);
        }

        private static void ConfigureSpawnAreaTrigger(
            Scene worldScene,
            Transform parent)
        {
            Transform triggerTransform = parent.Find(k_SpawnTriggerName);
            GameObject triggerObject = triggerTransform != null
                ? triggerTransform.gameObject
                : new GameObject(k_SpawnTriggerName);
            triggerObject.transform.SetParent(parent, false);

            Transform spawnPoint = FindTransform(worldScene, "Player Spawn Point");
            triggerObject.transform.position = spawnPoint != null
                ? spawnPoint.position
                : Vector3.zero;
            BoxCollider boxCollider = GetOrAddComponent<BoxCollider>(triggerObject);
            boxCollider.isTrigger = true;
            boxCollider.center = new Vector3(0f, 3f, 0f);
            boxCollider.size = new Vector3(12f, 6f, 12f);
            EventTriggerLoadScene trigger =
                GetOrAddComponent<EventTriggerLoadScene>(triggerObject);
            SerializedObject serializedTrigger = new SerializedObject(trigger);
            SetInteger(
                serializedTrigger,
                "m_area",
                (int)WorldSceneLocation.Area01SubArea00);
            serializedTrigger.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(triggerObject);
        }

        private static void ConfigureProbeVolume(
            Scene worldScene,
            IReadOnlyCollection<Scene> streamingScenes)
        {
            GameObject probeObject = FindRoot(worldScene, k_ProbeVolumeName);
            if (probeObject == null)
            {
                probeObject = new GameObject(k_ProbeVolumeName);
                SceneManager.MoveGameObjectToScene(probeObject, worldScene);
            }

            ProbeVolume probeVolume = GetOrAddComponent<ProbeVolume>(probeObject);
            probeVolume.mode = ProbeVolume.Mode.Local;
            if (TryGetWorldBounds(streamingScenes, out Bounds worldBounds))
            {
                probeObject.transform.position = worldBounds.center;
                probeVolume.size = worldBounds.size + Vector3.one * 4f;
            }
            else
            {
                probeVolume.size = new Vector3(20f, 20f, 20f);
            }

            EditorUtility.SetDirty(probeVolume);
            EditorUtility.SetDirty(probeObject);
        }

        private static bool TryGetWorldBounds(
            IEnumerable<Scene> scenes,
            out Bounds worldBounds)
        {
            worldBounds = default;
            bool hasBounds = false;
            foreach (Scene scene in scenes)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Renderer renderer in
                        root.GetComponentsInChildren<Renderer>(true))
                    {
                        Encapsulate(renderer.bounds, ref worldBounds, ref hasBounds);
                    }

                    foreach (Terrain terrain in
                        root.GetComponentsInChildren<Terrain>(true))
                    {
                        Bounds terrainBounds = new Bounds(
                            terrain.transform.position +
                                terrain.terrainData.size * 0.5f,
                            terrain.terrainData.size);
                        Encapsulate(terrainBounds, ref worldBounds, ref hasBounds);
                    }
                }
            }

            return hasBounds;
        }

        private static void Encapsulate(
            Bounds bounds,
            ref Bounds worldBounds,
            ref bool hasBounds)
        {
            if (!hasBounds)
            {
                worldBounds = bounds;
                hasBounds = true;
                return;
            }

            worldBounds.Encapsulate(bounds);
        }

        private static void ConfigureTriggerPrefab()
        {
            EnsureAssetFolder(Path.GetDirectoryName(k_TriggerPrefabPath)
                ?.Replace('\\', '/'));
            GameObject triggerObject = new GameObject("Area Load Trigger");
            try
            {
                BoxCollider boxCollider = triggerObject.AddComponent<BoxCollider>();
                boxCollider.isTrigger = true;
                boxCollider.size = new Vector3(4f, 5f, 1f);
                triggerObject.AddComponent<EventTriggerLoadScene>();
                PrefabUtility.SaveAsPrefabAsset(
                    triggerObject,
                    k_TriggerPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(triggerObject);
            }
        }

        private static void ValidateSceneAssetsAndBuildSettings()
        {
            HashSet<string> enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToHashSet();
            foreach (string scenePath in GetStreamingScenePaths())
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null ||
                    !enabledScenes.Contains(scenePath))
                {
                    throw new InvalidOperationException(
                        $"Streaming Scene is missing or disabled: {scenePath}");
                }
            }
        }

        private static void ValidateBakingSet()
        {
            ProbeVolumeBakingSet bakingSet =
                AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(
                    k_BakingSetPath) ??
                throw new InvalidOperationException(
                    "The world streaming APV Baking Set is missing.");
            foreach (string scenePath in GetStreamingScenePaths())
            {
                if (!bakingSet.sceneGUIDs.Contains(
                    AssetDatabase.AssetPathToGUID(scenePath)))
                {
                    throw new InvalidOperationException(
                        $"APV Baking Set is missing {scenePath}.");
                }
            }

            SerializedObject serializedSet = new SerializedObject(bakingSet);
            if (serializedSet.FindProperty("singleSceneMode").boolValue ||
                !serializedSet.FindProperty(
                    "settings.virtualOffsetSettings.useVirtualOffset").boolValue)
            {
                throw new InvalidOperationException(
                    "APV multi-Scene or Virtual Offset settings are invalid.");
            }
        }

        private static void ValidatePersistentWorldScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_WorldScenePath);
            bool openedByValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedByValidation)
            {
                scene = EditorSceneManager.OpenScene(
                    k_WorldScenePath,
                    OpenSceneMode.Additive);
            }

            GameObject managerObject = FindRoot(scene, k_ManagerName);
            GameObject probeObject = FindRoot(scene, k_ProbeVolumeName);
            Transform triggerTransform = managerObject?.transform.Find(
                k_SpawnTriggerName);
            if (managerObject == null ||
                managerObject.GetComponent<NetworkObject>() == null ||
                managerObject.GetComponent<WorldSceneManager>() == null ||
                managerObject.GetComponent<WorldSceneSubSceneManager>() == null ||
                triggerTransform?.GetComponent<EventTriggerLoadScene>() == null ||
                probeObject?.GetComponent<ProbeVolume>()?.mode !=
                    ProbeVolume.Mode.Local)
            {
                throw new InvalidOperationException(
                    "Persistent world streaming services are incomplete.");
            }

            if (openedByValidation)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateTriggerPrefab()
        {
            GameObject triggerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_TriggerPrefabPath);
            BoxCollider collider = triggerPrefab?.GetComponent<BoxCollider>();
            if (triggerPrefab == null ||
                collider == null ||
                !collider.isTrigger ||
                triggerPrefab.GetComponent<EventTriggerLoadScene>() == null)
            {
                throw new InvalidOperationException(
                    "The reusable Area Load Trigger prefab is invalid.");
            }
        }

        private static IEnumerable<string> GetStreamingScenePaths()
        {
            yield return k_WorldScenePath;
            foreach (string sceneID in s_areaSceneIDs)
            {
                yield return GetAreaScenePath(sceneID);
            }
        }

        private static string GetAreaScenePath(string sceneID)
        {
            return $"{k_AreaSceneFolder}/{sceneID}.unity";
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == objectName);
        }

        private static Transform FindTransform(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform match = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) ||
                AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parentPath = Path.GetDirectoryName(folderPath)
                ?.Replace('\\', '/');
            EnsureAssetFolder(parentPath);
            AssetDatabase.CreateFolder(
                parentPath,
                Path.GetFileName(folderPath));
        }

        private static void SetBoolean(
            SerializedObject serializedObject,
            string propertyPath,
            bool value)
        {
            FindRequiredProperty(serializedObject, propertyPath).boolValue = value;
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyPath,
            float value)
        {
            FindRequiredProperty(serializedObject, propertyPath).floatValue = value;
        }

        private static void SetInteger(
            SerializedObject serializedObject,
            string propertyPath,
            int value)
        {
            FindRequiredProperty(serializedObject, propertyPath).intValue = value;
        }

        private static void SetString(
            SerializedObject serializedObject,
            string propertyPath,
            string value)
        {
            FindRequiredProperty(serializedObject, propertyPath).stringValue = value;
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyPath,
            UnityEngine.Object value)
        {
            FindRequiredProperty(serializedObject, propertyPath)
                .objectReferenceValue = value;
        }

        private static SerializedProperty FindRequiredProperty(
            SerializedObject serializedObject,
            string propertyPath)
        {
            return serializedObject.FindProperty(propertyPath) ??
                throw new InvalidOperationException(
                    $"Missing serialized property {propertyPath} on " +
                    $"{serializedObject.targetObject.name}.");
        }
    }
}
