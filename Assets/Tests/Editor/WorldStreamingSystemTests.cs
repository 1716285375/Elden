using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Tests
{
    public class WorldStreamingSystemTests
    {
        private const string k_WorldScenePath =
            "Assets/Scenes/Levels/LV01_AbandonedMonastery/SCN_LV01_AbandonedMonastery.unity";
        private const string k_WorldLocationFolder =
            "Assets/Resources/World Locations";
        private const string k_BakingSetPath =
            "Assets/Settings/World Streaming Probe Volume Baking Set.asset";
        private const string k_TriggerPrefabPath =
            "Assets/Data/Prefabs/World Streaming/Area Load Trigger.prefab";

        private static readonly int[] s_areaLocations =
        {
            1,
            2,
            3,
            4,
            5
        };

        [Test]
        public static void SceneLocationMappingMatchesActualAreaSceneNames()
        {
            Assert.That(
                GetSceneID(0),
                Is.EqualTo(WorldScenePathLayout.MasterSceneName));
            for (int areaIndex = 0; areaIndex < s_areaLocations.Length; areaIndex++)
            {
                string expectedID = WorldScenePathLayout.GetSceneID(
                    areaIndex,
                    0);
                Assert.That(
                    GetSceneID(s_areaLocations[areaIndex]),
                    Is.EqualTo(expectedID));
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        WorldScenePathLayout.GetScenePath(expectedID)),
                    Is.Not.Null);
            }
        }

        [Test]
        public static void AdjacencyLoadsCurrentAndDirectlyConnectedAreas()
        {
            CollectionAssert.AreEqual(
                GetOwnedSceneIDs(0).Concat(GetOwnedSceneIDs(1)),
                GetScenesToLoad(1));
            CollectionAssert.AreEqual(
                GetOwnedSceneIDs(2)
                    .Concat(GetOwnedSceneIDs(1))
                    .Concat(GetOwnedSceneIDs(3)),
                GetScenesToLoad(3));
            CollectionAssert.AreEqual(
                GetOwnedSceneIDs(4).Concat(GetOwnedSceneIDs(3)),
                GetScenesToLoad(5));
        }

        [Test]
        public static void MultiplayerProtectionUsesUnionAndAlwaysKeepsWorld()
        {
            GameObject managerObject = new GameObject("Sub Scene Manager Test");
            GameObject playerAObject = new GameObject("Player A");
            GameObject playerBObject = new GameObject("Player B");
            managerObject.SetActive(false);
            playerAObject.SetActive(false);
            playerBObject.SetActive(false);
            try
            {
                managerObject.AddComponent(GetRuntimeType("Unity.Netcode.NetworkObject"));
                Component manager = managerObject.AddComponent(
                    GetRuntimeType("ZZ.WorldSceneSubSceneManager"));
                Component playerA = playerAObject.AddComponent(
                    GetRuntimeType("ZZ.PlayerManager"));
                Component playerB = playerBObject.AddComponent(
                    GetRuntimeType("ZZ.PlayerManager"));
                manager.GetType().GetMethod(
                    "EnsureLocationRegistry",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, null);
                IDictionary playersInLocation = (IDictionary)manager.GetType()
                    .GetField(
                        "m_playersInLocation",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(manager);
                UnityEngine.Object area00 = LoadLocationAsset(0);
                UnityEngine.Object area04 = LoadLocationAsset(4);
                ((IList)playersInLocation[area00]).Add(playerA);
                ((IList)playersInLocation[area04]).Add(playerB);

                CollectionAssert.AreEquivalent(
                    new[] { WorldScenePathLayout.MasterSceneName }
                        .Concat(GetOwnedSceneIDs(0))
                        .Concat(GetOwnedSceneIDs(1))
                        .Concat(GetOwnedSceneIDs(4))
                        .Concat(GetOwnedSceneIDs(3)),
                    (IEnumerable)manager.GetType().GetMethod(
                        "BuildDoNotUnloadSceneIDs").Invoke(manager, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(playerAObject);
                UnityEngine.Object.DestroyImmediate(playerBObject);
            }
        }

        [Test]
        public static void NetworkSceneManagerQueuesOperationsUntilCompletionEvents()
        {
            string source = ReadRuntimeSource("World Managers/WorldSceneManager.cs");

            Type managerType = GetRuntimeType("ZZ.WorldSceneManager");
            Assert.That(
                managerType.BaseType?.FullName,
                Is.EqualTo("Unity.Netcode.NetworkBehaviour"));
            Assert.That(source, Does.Contain("m_queuedSceneIDs.Enqueue(sceneID)"));
            Assert.That(
                source,
                Does.Contain("m_queuedUnloadSceneIDs.Enqueue(sceneID)"));
            Assert.That(source, Does.Contain("LoadSceneMode.Additive"));
            Assert.That(
                source,
                Does.Contain("SceneEventType.LoadEventCompleted"));
            Assert.That(
                source,
                Does.Contain("SceneEventType.UnloadEventCompleted"));
            Assert.That(source, Does.Contain("AddLoadedScene("));
            Assert.That(source, Does.Not.Contain(
                "m_loadedScenes.Add(sceneID)"));
        }

        [Test]
        public static void AreaTriggerFiltersServerAndPlayerBeforeChangingArea()
        {
            string source = ReadRuntimeSource(
                "World Managers/EventTriggerLoadScene.cs");
            Type triggerType = GetRuntimeType("ZZ.EventTriggerLoadScene");
            DisallowMultipleComponent disallowMultiple = triggerType
                .GetCustomAttributes<DisallowMultipleComponent>()
                .Single();

            Assert.That(disallowMultiple, Is.Not.Null);
            Assert.That(
                source,
                Does.Contain("NetworkManager.Singleton?.IsServer != true"));
            Assert.That(
                source,
                Does.Contain("other.GetComponentInParent<PlayerManager>()"));
            Assert.That(
                source,
                Does.Contain("LoadAreaBasedOnCurrentArea("));
            Assert.That(source, Does.Contain("WorldLocationSceneSet"));
            Assert.That(source, Does.Contain("meshCollider.convex = true"));
        }

        [Test]
        public static void LocalPlayerWaitsForLoadedSceneBeforeActivatingAPV()
        {
            string source = ReadRuntimeSource(
                "World Managers/WorldSceneSubSceneManager.cs");

            Assert.That(source, Does.Contain("player.IsOwner"));
            Assert.That(
                source,
                Does.Contain(
                    "WorldSceneManager.Instance?.IsSceneLoaded(activeSceneID)"));
            Assert.That(
                source,
                Does.Contain("ProbeReferenceVolume.instance?.SetActiveScene(scene)"));
        }

        [Test]
        public static void BuildSettingsAndBakingSetContainEveryStreamingScene()
        {
            HashSet<string> enabledScenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToHashSet();
            UnityEngine.Object bakingSet = AssetDatabase.LoadAssetAtPath(
                k_BakingSetPath,
                GetRuntimeType("UnityEngine.Rendering.ProbeVolumeBakingSet"));

            Assert.That(bakingSet, Is.Not.Null);
            Assert.That(enabledScenePaths, Does.Contain(k_WorldScenePath));
            IEnumerable<string> sceneGUIDs =
                (IEnumerable<string>)bakingSet.GetType()
                    .GetProperty("sceneGUIDs")
                    ?.GetValue(bakingSet);
            for (int locationIndex = 0;
                locationIndex < s_areaLocations.Length;
                locationIndex++)
            {
                foreach (string sceneID in GetOwnedSceneIDs(locationIndex))
                {
                    string scenePath =
                        WorldScenePathLayout.GetScenePath(sceneID);
                    Assert.That(enabledScenePaths, Does.Contain(scenePath));
                    Assert.That(
                        sceneGUIDs,
                        Does.Contain(AssetDatabase.AssetPathToGUID(scenePath)));
                }
            }

            SerializedObject serializedSet = new SerializedObject(bakingSet);
            Assert.That(
                serializedSet.FindProperty("singleSceneMode").boolValue,
                Is.False);
            Assert.That(
                serializedSet.FindProperty(
                    "settings.virtualOffsetSettings.useVirtualOffset").boolValue,
                Is.True);
        }

        [Test]
        public static void PersistentWorldAndReusableTriggerAreConfigured()
        {
            Scene scene = SceneManager.GetSceneByPath(k_WorldScenePath);
            bool openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest)
            {
                scene = EditorSceneManager.OpenScene(
                    k_WorldScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                GameObject manager = scene.GetRootGameObjects()
                    .Single(root => root.name == "World Streaming Manager");
                GameObject probe = scene.GetRootGameObjects()
                    .Single(root => root.name ==
                        "World Streaming Adaptive Probe Volume");
                GameObject triggerPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        k_TriggerPrefabPath);

                Assert.That(
                    manager.GetComponent(
                        GetRuntimeType("Unity.Netcode.NetworkObject")),
                    Is.Not.Null);
                Assert.That(
                    manager.GetComponent(GetRuntimeType("ZZ.WorldSceneManager")),
                    Is.Not.Null);
                Assert.That(
                    manager.GetComponent(
                        GetRuntimeType("ZZ.WorldSceneSubSceneManager")),
                    Is.Not.Null);
                Assert.That(
                    manager.transform.Find("Spawn Area Load Trigger")
                        ?.GetComponent(GetRuntimeType("ZZ.EventTriggerLoadScene")),
                    Is.Not.Null);
                Component probeVolume = probe.GetComponent(
                    GetRuntimeType("UnityEngine.Rendering.ProbeVolume"));
                Assert.That(probeVolume, Is.Not.Null);
                Assert.That(
                    new SerializedObject(probeVolume)
                        .FindProperty("mode").enumValueIndex,
                    Is.EqualTo(2));
                Assert.That(triggerPrefab, Is.Not.Null);
                Assert.That(
                    triggerPrefab.GetComponent<BoxCollider>()?.isTrigger,
                    Is.True);
            }
            finally
            {
                if (openedByTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public static void SessionRegistrationUpdatesStreamingPlayerOwnership()
        {
            string source = ReadRuntimeSource(
                "World Managers/WorldGameSessionManager.cs");

            Assert.That(
                source,
                Does.Contain("RegisterPlayerAtDefaultLocation(player)"));
            Assert.That(
                source,
                Does.Contain("WorldSceneSubSceneManager.Instance?.RemovePlayer(player)"));
        }

        private static UnityEngine.Object LoadLocationAsset(int locationIndex)
        {
            string assetName =
                WorldScenePathLayout.GetRegionFolderName(locationIndex);
            UnityEngine.Object location = AssetDatabase.LoadAssetAtPath(
                $"{k_WorldLocationFolder}/{assetName}.asset",
                GetRuntimeType("ZZ.WorldLocationSceneSet"));
            Assert.That(location, Is.Not.Null, assetName);
            return location;
        }

        private static IEnumerable<string> GetOwnedSceneIDs(int locationIndex)
        {
            for (int sliceIndex = 0; sliceIndex < 4; sliceIndex++)
            {
                yield return WorldScenePathLayout.GetSceneID(
                    locationIndex,
                    sliceIndex);
            }
        }

        private static string GetSceneID(int locationValue)
        {
            Type managerType = GetRuntimeType("ZZ.WorldSceneSubSceneManager");
            Type locationType = GetRuntimeType("ZZ.WorldSceneLocation");
            MethodInfo method = managerType.GetMethod(
                "GetSceneIDFromWorldSceneLocation",
                BindingFlags.Public | BindingFlags.Static);
            return (string)method.Invoke(
                null,
                new[] { Enum.ToObject(locationType, locationValue) });
        }

        private static string[] GetScenesToLoad(int locationValue)
        {
            Type managerType = GetRuntimeType("ZZ.WorldSceneSubSceneManager");
            Type locationType = GetRuntimeType("ZZ.WorldSceneLocation");
            MethodInfo method = managerType.GetMethod(
                "GetScenesToLoadForLocation",
                BindingFlags.Public | BindingFlags.Static);
            return ((IEnumerable<string>)method.Invoke(
                null,
                new[] { Enum.ToObject(locationType, locationValue) })).ToArray();
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static string ReadRuntimeSource(string relativePath)
        {
            return File.ReadAllText($"Assets/_Game/Scripts/{relativePath}");
        }
    }
}
