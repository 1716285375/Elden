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
            "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery/SCN_LV01_AbandonedMonastery.unity";
        private const string k_WorldLocationFolder =
            "Assets/_Game/Resources/World Locations";
        private const string k_BakingSetPath =
            "Assets/_Game/Settings/Rendering/Lighting/World Streaming Probe Volume Baking Set.asset";
        private const string k_TriggerPrefabPath =
            "Assets/_Game/Prefabs/World/Streaming/Area Load Trigger.prefab";

        /// <summary>
        /// WorldSceneLocation enum value -> (region, area) of the Scene Set that
        /// owns it. Values 6-8 are R01's per-Area streaming units.
        /// </summary>
        private static readonly (int LocationValue, int RegionIndex, int AreaIndex)[]
            s_locationMapping =
            {
                (1, 0, 0), // R01 A01 CliffPath
                (6, 0, 1), // R01 A02 Graveyard
                (7, 0, 2), // R01 A03 MainGate
                (8, 0, 3), // R01 A04 GateTower
                (2, 1, 0), // R02 A01 EntranceHall
                (3, 2, 0), // R03
                (4, 3, 0), // R04
                (5, 4, 0)  // R05
            };

        [Test]
        public static void SceneLocationMappingMatchesActualAreaSceneNames()
        {
            Assert.That(
                GetSceneID(0),
                Is.EqualTo(WorldScenePathLayout.MasterSceneName));
            foreach ((int locationValue, int regionIndex, int areaIndex) in
                s_locationMapping)
            {
                string expectedID = WorldScenePathLayout.GetSceneID(
                    regionIndex,
                    areaIndex,
                    0);
                Assert.That(
                    GetSceneID(locationValue),
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
                GetOwnedSceneIDs(0, 0).Concat(GetOwnedSceneIDs(0, 1)),
                GetScenesToLoad(1));
            CollectionAssert.AreEqual(
                GetOwnedSceneIDs(0, 1)
                    .Concat(GetOwnedSceneIDs(0, 0))
                    .Concat(GetOwnedSceneIDs(0, 2))
                    .Concat(GetOwnedSceneIDs(0, 3)),
                GetScenesToLoad(6));
            CollectionAssert.AreEqual(
                GetOwnedSceneIDs(0, 2)
                    .Concat(GetOwnedSceneIDs(0, 1))
                    .Concat(GetOwnedSceneIDs(0, 3)),
                GetScenesToLoad(7));
            CollectionAssert.AreEqual(
                GetOwnedSceneIDs(0, 3)
                    .Concat(GetOwnedSceneIDs(0, 1))
                    .Concat(GetOwnedSceneIDs(0, 2)),
                GetScenesToLoad(8));
            CollectionAssert.AreEqual(
                GetOwnedSceneIDs(1, 0)
                    .Concat(GetOwnedSceneIDs(0, 2))
                    .Concat(GetOwnedSceneIDs(2, 0)),
                GetScenesToLoad(2));
            CollectionAssert.AreEqual(
                GetOwnedSceneIDs(4, 0).Concat(GetOwnedSceneIDs(3, 0)),
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
                UnityEngine.Object area00 = LoadLocationAsset(
                    "R01_MonasteryOutskirts_A01_CliffPath");
                UnityEngine.Object area04 = LoadLocationAsset(
                    "R05_BossSanctum");
                ((IList)playersInLocation[area00]).Add(playerA);
                ((IList)playersInLocation[area04]).Add(playerB);

                CollectionAssert.AreEquivalent(
                    new[] { WorldScenePathLayout.MasterSceneName }
                        .Concat(GetOwnedSceneIDs(0, 0))
                        .Concat(GetOwnedSceneIDs(0, 1))
                        .Concat(GetOwnedSceneIDs(4, 0))
                        .Concat(GetOwnedSceneIDs(3, 0)),
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
            foreach (UnityEngine.Object location in LoadAllLocationAssets())
            {
                IReadOnlyList<string> sceneIDs = GetProperty<
                    IReadOnlyList<string>>(
                        location,
                        "ScenesRequiredForThisLocation");
                foreach (string sceneID in sceneIDs)
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
                GameObject manager = FindChild(scene, "World Streaming Manager");
                GameObject probe = FindChild(
                    scene,
                    "World Streaming Adaptive Probe Volume");
                GameObject triggerPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        k_TriggerPrefabPath);

                Assert.That(manager, Is.Not.Null, "World Streaming Manager");
                Assert.That(probe, Is.Not.Null, "Adaptive Probe Volume");
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

        private static UnityEngine.Object LoadLocationAsset(string assetName)
        {
            UnityEngine.Object location = AssetDatabase.LoadAssetAtPath(
                $"{k_WorldLocationFolder}/{assetName}.asset",
                GetRuntimeType("ZZ.WorldLocationSceneSet"));
            Assert.That(location, Is.Not.Null, assetName);
            return location;
        }

        private static UnityEngine.Object[] LoadAllLocationAssets()
        {
            Type locationType = GetRuntimeType("ZZ.WorldLocationSceneSet");
            return AssetDatabase.FindAssets("t:WorldLocationSceneSet",
                    new[] { k_WorldLocationFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath(
                    AssetDatabase.GUIDToAssetPath(guid),
                    locationType))
                .Where(location => location != null)
                .ToArray();
        }

        private static IEnumerable<string> GetOwnedSceneIDs(
            int regionIndex,
            int areaIndex)
        {
            for (int sliceIndex = 0; sliceIndex < 4; sliceIndex++)
            {
                yield return WorldScenePathLayout.GetSceneID(
                    regionIndex,
                    areaIndex,
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

        private static GameObject FindChild(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform match = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == objectName);
                if (match != null)
                {
                    return match.gameObject;
                }
            }

            return null;
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
            relativePath = RemapRuntimeSourcePath(relativePath);
            return File.ReadAllText($"Assets/_Game/Scripts/{relativePath}")
                .Replace("\r\n", "\n");
        }
        /// <summary>Maps a pre-refactor Script-relative path to the new layout.</summary>
        private static string RemapRuntimeSourcePath(string relativePath)
        {
            if (relativePath.StartsWith("Character/Player/Player UI/"))
                return "UI/Gameplay/Player/" + relativePath.Substring("Character/Player/Player UI/".Length);
            if (relativePath.StartsWith("Character/Player/"))
                return "Characters/Player/" + relativePath.Substring("Character/Player/".Length);
            if (relativePath.StartsWith("Character/AI/"))
                return "Characters/AI/" + relativePath.Substring("Character/AI/".Length);
            if (relativePath.StartsWith("Character/Effects/"))
                return "Characters/Common/Effects/" + relativePath.Substring("Character/Effects/".Length);
            if (relativePath.StartsWith("Character/Equipment/"))
                return "Characters/Common/Equipment/" + relativePath.Substring("Character/Equipment/".Length);
            if (relativePath.StartsWith("Character/Inventory/"))
                return "Characters/Common/Inventory/" + relativePath.Substring("Character/Inventory/".Length);
            if (relativePath.StartsWith("Character/Character UI/"))
                return "UI/Gameplay/Character/" + relativePath.Substring("Character/Character UI/".Length);
            if (relativePath.StartsWith("Character/Animation State Behaviors/"))
                return "Characters/Common/Animation State Behaviors/" + relativePath.Substring("Character/Animation State Behaviors/".Length);
            if (relativePath.StartsWith("Character/"))
                return "Characters/Common/" + relativePath.Substring("Character/".Length);
            if (relativePath.StartsWith("World Managers/AI/"))
                return "World/AI/" + relativePath.Substring("World Managers/AI/".Length);
            if (relativePath.StartsWith("World Managers/"))
                return "World/Managers/" + relativePath.Substring("World Managers/".Length);
            if (relativePath.StartsWith("World Objects/"))
                return "World/Objects/" + relativePath.Substring("World Objects/".Length);
            if (relativePath.StartsWith("Save System/"))
                return "Save/" + relativePath.Substring("Save System/".Length);
            if (relativePath.StartsWith("Menu Scene/"))
                return "UI/Frontend/" + relativePath.Substring("Menu Scene/".Length);
            if (relativePath.StartsWith("Effects/"))
                return "Combat/Effects/" + relativePath.Substring("Effects/".Length);
            if (relativePath.StartsWith("Damage/"))
                return "Combat/Damage/" + relativePath.Substring("Damage/".Length);
            if (relativePath.StartsWith("Actions/"))
                return "Combat/Actions/" + relativePath.Substring("Actions/".Length);
            if (relativePath.StartsWith("Projectiles/"))
                return "Combat/Projectiles/" + relativePath.Substring("Projectiles/".Length);
            if (relativePath.StartsWith("Spells/"))
                return "Abilities/Spells/" + relativePath.Substring("Spells/".Length);
            if (relativePath.StartsWith("Utility/"))
                return "Utilities/" + relativePath.Substring("Utility/".Length);
            return relativePath;
        }
    }
}
