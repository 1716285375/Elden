using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class LargeWorldStreamingSystemTests
    {
        private const string k_LocationFolder =
            "Assets/Resources/World Locations";
        private const string k_SceneFolder = "Assets/Scenes/World Areas";
        private const string k_TriggerFolder =
            "Assets/Data/Prefabs/World Streaming/World Location Triggers";

        [Test]
        public void SceneSetsOwnFourPhysicalSlicesAndDirectNeighbours()
        {
            UnityEngine.Object[] locations = LoadLocationAssets();
            Assert.That(locations.Length, Is.EqualTo(5));
            for (int locationIndex = 0;
                locationIndex < locations.Length;
                locationIndex++)
            {
                UnityEngine.Object location = locations[locationIndex];
                IReadOnlyList<string> ownedScenes = GetProperty<
                    IReadOnlyList<string>>(
                        location,
                        "ScenesRequiredForThisLocation");
                IReadOnlyList<UnityEngine.Object> requiredLocations =
                    ((IEnumerable)GetProperty(location, "RequiredLocations"))
                    .Cast<UnityEngine.Object>()
                    .ToArray();

                Assert.That(ownedScenes.Count, Is.EqualTo(4));
                Assert.That(ownedScenes[0],
                    Is.EqualTo($"Area_01_Sub_Area_{locationIndex:00}"));
                Assert.That(ownedScenes, Does.Contain(ownedScenes[0] + "_Props"));
                Assert.That(ownedScenes, Does.Contain(ownedScenes[0] + "_Effects"));
                Assert.That(ownedScenes,
                    Does.Contain(ownedScenes[0] + "_Spawners"));
                int expectedNeighbours = locationIndex == 0 ||
                    locationIndex == locations.Length - 1
                    ? 1
                    : 2;
                Assert.That(requiredLocations.Count,
                    Is.EqualTo(expectedNeighbours));
            }
        }

        [Test]
        public void SceneSetBuildsStableDeduplicatedRequiredSceneIDs()
        {
            foreach (UnityEngine.Object location in LoadLocationAssets())
            {
                MethodInfo getRequiredScenes = location.GetType().GetMethod(
                    "GetRequiredSceneIDsForWorldLocation",
                    BindingFlags.Public | BindingFlags.Instance);
                string[] requiredScenes = ((IEnumerable<string>)getRequiredScenes
                    .Invoke(location, null)).ToArray();
                IReadOnlyList<string> ownedScenes = GetProperty<
                    IReadOnlyList<string>>(
                        location,
                        "ScenesRequiredForThisLocation");

                Assert.That(requiredScenes.Length,
                    Is.EqualTo(requiredScenes.Distinct().Count()));
                Assert.That(requiredScenes.Take(ownedScenes.Count),
                    Is.EqualTo(ownedScenes));
                Assert.That(requiredScenes, Has.None.Null.Or.Empty);
            }
        }

        [Test]
        public void PlayerLocationTrackingIsDictionaryDriven()
        {
            string managerSource = ReadSource(
                "Assets/Script/World Managers/WorldSceneSubSceneManager.cs");
            string playerSource = ReadSource(
                "Assets/Script/Character/Player/PlayerManager.cs");

            Assert.That(managerSource, Does.Contain(
                "Dictionary<\n            WorldLocationSceneSet,\n" +
                "            List<PlayerManager>> m_playersInLocation"));
            Assert.That(managerSource,
                Does.Contain("Resources.LoadAll<WorldLocationSceneSet>"));
            Assert.That(managerSource,
                Does.Contain("players.RemoveAll(player => player == null)"));
            Assert.That(managerSource, Does.Not.Contain("m_area00Players"));
            Assert.That(managerSource, Does.Not.Contain("location switch"));
            Assert.That(playerSource, Does.Contain("AreaCurrentlyIn"));
            Assert.That(playerSource, Does.Contain("SetAreaCurrentlyIn"));
        }

        [Test]
        public void SceneQueueRetriesNetcodeBusyStateAndThrottlesOperations()
        {
            string source = ReadSource(
                "Assets/Script/World Managers/WorldSceneManager.cs");

            Assert.That(source, Does.Contain(
                "SceneEventProgressStatus.SceneEventInProgress"));
            Assert.That(source, Does.Contain("WaitForQueueInterval("));
            Assert.That(source, Does.Contain("m_loadOperationInterval = 0.1f"));
            Assert.That(source, Does.Contain("m_unloadOperationInterval = 0.5f"));
            Assert.That(source, Does.Contain("LoadingScreenIsActive()"));
            Assert.That(source, Does.Contain("IsLoadingScreenActive == true"));
            Assert.That(
                source.IndexOf("m_queuedSceneIDs.Count > 0",
                    StringComparison.Ordinal),
                Is.LessThan(source.IndexOf(
                    "string unloadSceneID",
                    StringComparison.Ordinal)));
        }

        [Test]
        public void EveryPhysicalSliceIsEnabledInBuildSettings()
        {
            HashSet<string> enabledScenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToHashSet();
            foreach (UnityEngine.Object location in LoadLocationAssets())
            {
                IReadOnlyList<string> sceneIDs = GetProperty<
                    IReadOnlyList<string>>(
                        location,
                        "ScenesRequiredForThisLocation");
                foreach (string sceneID in sceneIDs)
                {
                    string scenePath = $"{k_SceneFolder}/{sceneID}.unity";
                    Assert.That(
                        AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath),
                        Is.Not.Null,
                        scenePath);
                    Assert.That(enabledScenePaths, Does.Contain(scenePath));
                }
            }
        }

        [Test]
        public void LocationTriggerPrefabsUseConvexProBuilderVolumes()
        {
            Type proBuilderMeshType = GetRuntimeType(
                "UnityEngine.ProBuilder.ProBuilderMesh");
            Type polyShapeType = GetRuntimeType(
                "UnityEngine.ProBuilder.PolyShape");
            Type triggerType = GetRuntimeType("ZZ.EventTriggerLoadScene");
            foreach (UnityEngine.Object location in LoadLocationAssets())
            {
                string locationID = GetProperty<string>(location, "LocationID");
                string prefabPath = $"{k_TriggerFolder}/" +
                    $"{locationID} Trigger.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);
                MeshCollider collider = prefab?.GetComponent<MeshCollider>();
                Component trigger = prefab?.GetComponent(triggerType);

                Assert.That(prefab, Is.Not.Null, prefabPath);
                Assert.That(prefab.GetComponent(proBuilderMeshType), Is.Not.Null);
                Assert.That(prefab.GetComponent(polyShapeType), Is.Not.Null);
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.convex, Is.True);
                Assert.That(collider.isTrigger, Is.True);
                Assert.That(prefab.GetComponent<MeshRenderer>().enabled, Is.False);
                Assert.That(LayerMask.LayerToName(prefab.layer),
                    Is.EqualTo("Event Trigger"));
                Assert.That(new SerializedObject(trigger).FindProperty(
                    "m_worldLocation").objectReferenceValue,
                    Is.SameAs(location));
            }
        }

        public static void RunAllFocusedTests()
        {
            LargeWorldStreamingSystemTests tests = new();
            tests.SceneSetsOwnFourPhysicalSlicesAndDirectNeighbours();
            tests.SceneSetBuildsStableDeduplicatedRequiredSceneIDs();
            tests.PlayerLocationTrackingIsDictionaryDriven();
            tests.SceneQueueRetriesNetcodeBusyStateAndThrottlesOperations();
            tests.EveryPhysicalSliceIsEnabledInBuildSettings();
            tests.LocationTriggerPrefabsUseConvexProBuilderVolumes();
            WorldStreamingSystemTests.SceneLocationMappingMatchesActualAreaSceneNames();
            WorldStreamingSystemTests.AdjacencyLoadsCurrentAndDirectlyConnectedAreas();
            WorldStreamingSystemTests.MultiplayerProtectionUsesUnionAndAlwaysKeepsWorld();
            WorldStreamingSystemTests.NetworkSceneManagerQueuesOperationsUntilCompletionEvents();
            WorldStreamingSystemTests.AreaTriggerFiltersServerAndPlayerBeforeChangingArea();
            WorldStreamingSystemTests.LocalPlayerWaitsForLoadedSceneBeforeActivatingAPV();
            WorldStreamingSystemTests.BuildSettingsAndBakingSetContainEveryStreamingScene();
            WorldStreamingSystemTests.SessionRegistrationUpdatesStreamingPlayerOwnership();
            Debug.Log(
                "[LargeWorldStreamingSystemTests] 6 EP149-150 and 8 " +
                "streaming regression tests passed.");
        }

        private static UnityEngine.Object[] LoadLocationAssets()
        {
            Type locationType = GetRuntimeType("ZZ.WorldLocationSceneSet");
            return Enumerable.Range(0, 5)
                .Select(locationIndex => AssetDatabase.LoadAssetAtPath(
                    $"{k_LocationFolder}/" +
                    $"Area_01_Sub_Area_{locationIndex:00}.asset",
                    locationType))
                .Where(location => location != null)
                .ToArray();
        }

        private static object GetProperty(
            UnityEngine.Object target,
            string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(target);
        }

        private static T GetProperty<T>(
            UnityEngine.Object target,
            string propertyName)
        {
            return (T)GetProperty(target, propertyName);
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static string ReadSource(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)
                .FullName;
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }
    }
}
