using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the data-driven EP149-150 world locations.</summary>
    public static class LargeWorldSceneSetup
    {
        private const string k_WorldLocationFolder =
            "Assets/Resources/World Locations";
        private const string k_AreaSceneFolder = "Assets/Scenes/World Areas";
        private const string k_TriggerFolder =
            "Assets/Data/Prefabs/World Streaming/World Location Triggers";
        private const string k_BakingSetPath =
            "Assets/Settings/World Streaming Probe Volume Baking Set.asset";
        private const string k_TagManagerPath =
            "ProjectSettings/TagManager.asset";
        private const string k_EventTriggerLayerName = "Event Trigger";

        private static readonly WorldSceneLocation[] s_locations =
        {
            WorldSceneLocation.Area01SubArea00,
            WorldSceneLocation.Area01SubArea01,
            WorldSceneLocation.Area01SubArea02,
            WorldSceneLocation.Area01SubArea03,
            WorldSceneLocation.Area01SubArea04
        };

        private static readonly string[] s_sliceSuffixes =
        {
            "Props",
            "Effects",
            "Spawners"
        };

        [MenuItem("Tools/Elden/Configure EP149-150 Large World Scenes")]
        public static void ConfigureLargeWorldScenes()
        {
            EnsureFolder(k_WorldLocationFolder);
            EnsureFolder(k_AreaSceneFolder);
            EnsureFolder(k_TriggerFolder);
            EnsureSliceScenes();
            WorldLocationSceneSet[] locationSets =
                ConfigureWorldLocationAssets();
            ConfigureBuildSettings();
            ConfigureBakingSet();
            int eventTriggerLayer = EnsureEventTriggerLayer();
            ConfigureTriggerPrefabs(locationSets, eventTriggerLayer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateLargeWorldScenes();
            Debug.Log(
                "[LargeWorldSceneSetup] Configured five data-driven locations, " +
                "twenty additive Scene slices, and convex ProBuilder Triggers.");
        }

        [MenuItem("Tools/Elden/Validate EP149-150 Large World Scenes")]
        public static void ValidateLargeWorldScenes()
        {
            WorldLocationSceneSet[] locationSets = LoadLocationSets();
            if (locationSets.Length != s_locations.Length)
            {
                throw new InvalidOperationException(
                    "Every legacy world location needs one Scene Set asset.");
            }

            HashSet<string> enabledScenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToHashSet();
            ProbeVolumeBakingSet bakingSet =
                AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(
                    k_BakingSetPath) ??
                throw new InvalidOperationException(
                    "World streaming APV Baking Set is missing.");

            foreach (WorldLocationSceneSet locationSet in locationSets)
            {
                if (locationSet.ScenesRequiredForThisLocation.Count != 4)
                {
                    throw new InvalidOperationException(
                        $"{locationSet.name} must own Structure, Props, " +
                        "Effects, and Spawners Scene slices.");
                }

                foreach (string sceneID in
                    locationSet.ScenesRequiredForThisLocation)
                {
                    string scenePath = GetScenePath(sceneID);
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) ==
                            null ||
                        !enabledScenePaths.Contains(scenePath) ||
                        !bakingSet.sceneGUIDs.Contains(
                            AssetDatabase.AssetPathToGUID(scenePath)))
                    {
                        throw new InvalidOperationException(
                            $"Streaming Scene is not fully configured: " +
                            $"{scenePath}");
                    }
                }

                ValidateTriggerPrefab(locationSet);
            }

            Debug.Log(
                "[LargeWorldSceneValidation] EP149-150 data assets, Build " +
                "Profile Scenes, APV membership, and Trigger prefabs are valid.");
        }

        private static WorldLocationSceneSet[] ConfigureWorldLocationAssets()
        {
            WorldLocationSceneSet[] locationSets = new WorldLocationSceneSet[
                s_locations.Length];
            for (int locationIndex = 0;
                locationIndex < s_locations.Length;
                locationIndex++)
            {
                string locationID = GetBaseSceneID(locationIndex);
                string assetPath = GetLocationAssetPath(locationID);
                WorldLocationSceneSet locationSet =
                    AssetDatabase.LoadAssetAtPath<WorldLocationSceneSet>(
                        assetPath);
                if (locationSet == null)
                {
                    locationSet = ScriptableObject.CreateInstance<
                        WorldLocationSceneSet>();
                    locationSet.name = locationID;
                    AssetDatabase.CreateAsset(locationSet, assetPath);
                }

                locationSets[locationIndex] = locationSet;
            }

            for (int locationIndex = 0;
                locationIndex < locationSets.Length;
                locationIndex++)
            {
                WorldLocationSceneSet locationSet = locationSets[locationIndex];
                string locationID = GetBaseSceneID(locationIndex);
                SerializedObject serializedSet = new(locationSet);
                GetRequiredProperty(serializedSet, "m_locationID").stringValue =
                    locationID;
                GetRequiredProperty(serializedSet, "m_legacyLocation").intValue =
                    (int)s_locations[locationIndex];

                SerializedProperty scenes = GetRequiredProperty(
                    serializedSet,
                    "m_scenesRequiredForThisLocation");
                string[] sceneIDs = GetOwnedSceneIDs(locationIndex).ToArray();
                scenes.arraySize = sceneIDs.Length;
                for (int sceneIndex = 0;
                    sceneIndex < sceneIDs.Length;
                    sceneIndex++)
                {
                    scenes.GetArrayElementAtIndex(sceneIndex).stringValue =
                        sceneIDs[sceneIndex];
                }

                List<WorldLocationSceneSet> requiredLocations = new();
                if (locationIndex > 0)
                {
                    requiredLocations.Add(locationSets[locationIndex - 1]);
                }

                if (locationIndex < locationSets.Length - 1)
                {
                    requiredLocations.Add(locationSets[locationIndex + 1]);
                }

                SerializedProperty required = GetRequiredProperty(
                    serializedSet,
                    "m_requiredLocations");
                required.arraySize = requiredLocations.Count;
                for (int requiredIndex = 0;
                    requiredIndex < requiredLocations.Count;
                    requiredIndex++)
                {
                    required.GetArrayElementAtIndex(requiredIndex)
                        .objectReferenceValue = requiredLocations[requiredIndex];
                }

                serializedSet.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(locationSet);
            }

            return locationSets;
        }

        private static void EnsureSliceScenes()
        {
            Scene originalActiveScene = SceneManager.GetActiveScene();
            try
            {
                for (int locationIndex = 0;
                    locationIndex < s_locations.Length;
                    locationIndex++)
                {
                    foreach (string sceneID in GetOwnedSceneIDs(locationIndex)
                        .Skip(1))
                    {
                        string scenePath = GetScenePath(sceneID);
                        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) !=
                            null)
                        {
                            continue;
                        }

                        Scene scene = EditorSceneManager.NewScene(
                            NewSceneSetup.EmptyScene,
                            NewSceneMode.Additive);
                        GameObject sliceRoot = new($"World Slice - {sceneID}");
                        SceneManager.MoveGameObjectToScene(sliceRoot, scene);
                        EditorSceneManager.SaveScene(scene, scenePath);
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
            finally
            {
                if (originalActiveScene.IsValid() &&
                    originalActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(originalActiveScene);
                }
            }
        }

        private static void ConfigureBuildSettings()
        {
            List<EditorBuildSettingsScene> buildScenes =
                EditorBuildSettings.scenes.ToList();
            foreach (string scenePath in GetAllAreaScenePaths())
            {
                EditorBuildSettingsScene existing = buildScenes.FirstOrDefault(
                    scene => scene.path == scenePath);
                if (existing != null)
                {
                    existing.enabled = true;
                }
                else
                {
                    buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
                }
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        private static void ConfigureBakingSet()
        {
            ProbeVolumeBakingSet bakingSet =
                AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(
                    k_BakingSetPath) ??
                throw new InvalidOperationException(
                    "Configure EP119-123 world streaming before EP149-150.");
            foreach (string scenePath in GetAllAreaScenePaths())
            {
                string sceneGUID = AssetDatabase.AssetPathToGUID(scenePath);
                if (!bakingSet.sceneGUIDs.Contains(sceneGUID))
                {
                    bakingSet.TryAddScene(sceneGUID);
                }
            }

            EditorUtility.SetDirty(bakingSet);
        }

        private static int EnsureEventTriggerLayer()
        {
            UnityEngine.Object tagManager = AssetDatabase.LoadAllAssetsAtPath(
                    k_TagManagerPath)
                .FirstOrDefault() ??
                throw new InvalidOperationException("TagManager asset is missing.");
            SerializedObject serializedTagManager = new(tagManager);
            SerializedProperty layers = GetRequiredProperty(
                serializedTagManager,
                "layers");
            for (int layerIndex = 8;
                layerIndex < layers.arraySize;
                layerIndex++)
            {
                if (layers.GetArrayElementAtIndex(layerIndex).stringValue ==
                    k_EventTriggerLayerName)
                {
                    return layerIndex;
                }
            }

            for (int layerIndex = 8;
                layerIndex < layers.arraySize;
                layerIndex++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(
                    layerIndex);
                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    continue;
                }

                layer.stringValue = k_EventTriggerLayerName;
                serializedTagManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tagManager);
                return layerIndex;
            }

            throw new InvalidOperationException(
                "No free user layer is available for Event Trigger.");
        }

        private static void ConfigureTriggerPrefabs(
            IReadOnlyList<WorldLocationSceneSet> locationSets,
            int eventTriggerLayer)
        {
            foreach (WorldLocationSceneSet locationSet in locationSets)
            {
                GameObject triggerObject = new(
                    $"{locationSet.LocationID} Trigger",
                    typeof(ProBuilderMesh),
                    typeof(PolyShape),
                    typeof(MeshCollider),
                    typeof(EventTriggerLoadScene));
                try
                {
                    triggerObject.layer = eventTriggerLayer;
                    PolyShape polyShape = triggerObject.GetComponent<PolyShape>();
                    polyShape.SetControlPoints(new[]
                    {
                        new Vector3(-4f, 0f, -3f),
                        new Vector3(2.5f, 0f, -3f),
                        new Vector3(4f, 0f, -1f),
                        new Vector3(3f, 0f, 3f),
                        new Vector3(-3f, 0f, 3f),
                        new Vector3(-4f, 0f, 1f)
                    });
                    polyShape.extrude = 5f;
                    polyShape.flipNormals = false;
                    ActionResult result = polyShape.CreateShapeFromPolygon();
                    if (result.status == ActionResult.Status.Failure)
                    {
                        throw new InvalidOperationException(result.notification);
                    }

                    MeshCollider collider = triggerObject.GetComponent<
                        MeshCollider>();
                    collider.sharedMesh = triggerObject.GetComponent<MeshFilter>()
                        .sharedMesh;
                    collider.convex = true;
                    collider.isTrigger = true;
                    MeshRenderer renderer = triggerObject.GetComponent<
                        MeshRenderer>();
                    renderer.enabled = false;

                    EventTriggerLoadScene trigger = triggerObject.GetComponent<
                        EventTriggerLoadScene>();
                    SerializedObject serializedTrigger = new(trigger);
                    GetRequiredProperty(
                        serializedTrigger,
                        "m_worldLocation").objectReferenceValue = locationSet;
                    GetRequiredProperty(
                        serializedTrigger,
                        "m_area").intValue = (int)locationSet.LegacyLocation;
                    serializedTrigger.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(
                        triggerObject,
                        GetTriggerPrefabPath(locationSet));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(triggerObject);
                }
            }
        }

        private static void ValidateTriggerPrefab(
            WorldLocationSceneSet locationSet)
        {
            GameObject triggerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                GetTriggerPrefabPath(locationSet));
            MeshCollider collider = triggerPrefab?.GetComponent<MeshCollider>();
            EventTriggerLoadScene trigger = triggerPrefab?.GetComponent<
                EventTriggerLoadScene>();
            if (triggerPrefab == null ||
                triggerPrefab.GetComponent<ProBuilderMesh>() == null ||
                triggerPrefab.GetComponent<PolyShape>() == null ||
                collider == null ||
                !collider.convex ||
                !collider.isTrigger ||
                trigger == null ||
                trigger.WorldLocation != locationSet ||
                triggerPrefab.GetComponent<MeshRenderer>()?.enabled != false ||
                LayerMask.LayerToName(triggerPrefab.layer) !=
                    k_EventTriggerLayerName)
            {
                throw new InvalidOperationException(
                    $"World location Trigger is invalid: {locationSet.name}");
            }
        }

        private static WorldLocationSceneSet[] LoadLocationSets()
        {
            return s_locations
                .Select((location, index) =>
                    AssetDatabase.LoadAssetAtPath<WorldLocationSceneSet>(
                        GetLocationAssetPath(GetBaseSceneID(index))))
                .Where(location => location != null)
                .ToArray();
        }

        private static IEnumerable<string> GetOwnedSceneIDs(int locationIndex)
        {
            string baseSceneID = GetBaseSceneID(locationIndex);
            yield return baseSceneID;
            foreach (string sliceSuffix in s_sliceSuffixes)
            {
                yield return $"{baseSceneID}_{sliceSuffix}";
            }
        }

        private static IEnumerable<string> GetAllAreaScenePaths()
        {
            for (int locationIndex = 0;
                locationIndex < s_locations.Length;
                locationIndex++)
            {
                foreach (string sceneID in GetOwnedSceneIDs(locationIndex))
                {
                    yield return GetScenePath(sceneID);
                }
            }
        }

        private static string GetBaseSceneID(int locationIndex)
        {
            return $"Area_01_Sub_Area_{locationIndex:00}";
        }

        private static string GetScenePath(string sceneID)
        {
            return $"{k_AreaSceneFolder}/{sceneID}.unity";
        }

        private static string GetLocationAssetPath(string locationID)
        {
            return $"{k_WorldLocationFolder}/{locationID}.asset";
        }

        private static string GetTriggerPrefabPath(
            WorldLocationSceneSet locationSet)
        {
            return $"{k_TriggerFolder}/{locationSet.LocationID} Trigger.prefab";
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parentPath = Path.GetDirectoryName(folderPath)
                ?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parentPath))
            {
                EnsureFolder(parentPath);
                AssetDatabase.CreateFolder(
                    parentPath,
                    Path.GetFileName(folderPath));
            }
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
    }
}
