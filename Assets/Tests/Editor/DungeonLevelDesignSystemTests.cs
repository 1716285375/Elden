using System;
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
    public class DungeonLevelDesignSystemTests
    {
        private const string k_ScenePath =
            "Assets/Scenes/Levels/LV01_AbandonedMonastery/SCN_LV01_AbandonedMonastery.unity";
        private const string k_LocationPath =
            "World/Location 01 - Ashen Crypt";
        private const string k_SettingsFolder =
            "Assets/_Game/Settings/LevelDesign";

        [Test]
        public void WorldSceneContainsCompleteAshenCryptRouteStructure()
        {
            WithWorldScene(scene =>
            {
                Transform location = FindTransform(scene, k_LocationPath);

                Assert.That(location, Is.Not.Null);
                AssertZone(location, "Sub Location 00 - Grace and Entry");
                AssertZone(location, "Sub Location 01 - Upper Path A");
                AssertZone(location, "Sub Location 02 - Lower Path B");
                AssertZone(location, "Sub Location 03 - Convergence and Shortcut");
                AssertZone(location, "Sub Location 04 - Visible Reward Wing");
                AssertZone(location, "Boss Room");
            });
        }

        [Test]
        public void DungeonHasTwoServerReplicatedOneWayGates()
        {
            WithWorldScene(scene =>
            {
                Type gateType = GetRuntimeType("ZZ.DungeonOneWayGate");
                Component[] gates = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren(gateType, true))
                    .Cast<Component>()
                    .ToArray();

                Assert.That(gates, Has.Length.EqualTo(2));
                Assert.That(
                    gates.Select(gate => gate.name),
                    Does.Contain("Locked From Other Side Gate"));
                Assert.That(
                    gates.Select(gate => gate.name),
                    Does.Contain("Grace Boss Shortcut Gate"));
                Assert.That(
                    gates.All(gate => gate.GetComponent(
                        GetRuntimeType("Unity.Netcode.NetworkObject")) != null),
                    Is.True);
            });
        }

        [TestCase(5f, true, true)]
        [TestCase(-5f, true, false)]
        [TestCase(5f, false, false)]
        [TestCase(-5f, false, true)]
        public void OneWayGateSideRuleMatchesAuthoredDirection(
            float playerForwardOffset,
            bool allowedFromPositiveSide,
            bool expected)
        {
            MethodInfo method = GetRuntimeType("ZZ.DungeonOneWayGate").GetMethod(
                "IsOnAllowedSide",
                BindingFlags.Public | BindingFlags.Static);
            object result = method?.Invoke(
                null,
                new object[]
                {
                    Vector3.zero,
                    Vector3.forward,
                    new Vector3(0f, 0f, playerForwardOffset),
                    allowedFromPositiveSide
                });

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void SpawnAndGraceStartAtDungeonEntrance()
        {
            WithWorldScene(scene =>
            {
                Transform spawn = FindTransform(scene, "Player Spawn Point");
                Transform grace = FindTransform(scene, "First Step Site of Grace");

                Assert.That(spawn.position, Is.EqualTo(new Vector3(23f, 0.1f, 0f)));
                Assert.That(grace.position, Is.EqualTo(new Vector3(22f, 0f, 2.5f)));
            });
        }

        [Test]
        public void DungeonLightingAndPostProcessingAssetsAreBaked()
        {
            string lightingSettingsPath =
                k_SettingsFolder + "/EP99-100 World Lighting Settings.lighting";
            string volumeProfilePath =
                k_SettingsFolder + "/EP99-100 Ashen Crypt Volume.asset";
            LightingSettings lightingSettings =
                AssetDatabase.LoadAssetAtPath<LightingSettings>(lightingSettingsPath);
            UnityEngine.Object volumeProfile =
                AssetDatabase.LoadMainAssetAtPath(volumeProfilePath);
            UnityEngine.Object[] volumeAssets =
                AssetDatabase.LoadAllAssetsAtPath(volumeProfilePath);

            Assert.That(lightingSettings, Is.Not.Null);
            Assert.That(
                lightingSettings.mixedBakeMode,
                Is.EqualTo(MixedLightingMode.Shadowmask));
            Assert.That(lightingSettings.lightmapResolution, Is.EqualTo(12f));
            Assert.That(volumeProfile, Is.Not.Null);
            Assert.That(volumeProfile.GetType().Name, Is.EqualTo("VolumeProfile"));
            Assert.That(volumeAssets, Has.Length.EqualTo(5));
            Assert.That(volumeAssets, Has.None.Null);
            Assert.That(
                File.Exists("Assets/Scenes/Levels/LV01_AbandonedMonastery/Shared/Lighting/Baked/LightingData.asset"),
                Is.True);
            Assert.That(
                Directory.GetFiles(
                    "Assets/Scenes/Levels/LV01_AbandonedMonastery/Shared/Lighting/Baked",
                    "*shadowmask.png").Length,
                Is.GreaterThan(0));
        }

        [Test]
        public void DungeonGeometryContributesToStaticLightingAndNavigation()
        {
            WithWorldScene(scene =>
            {
                Transform floor = FindTransform(
                    scene,
                    k_LocationPath +
                    "/Sub Location 00 - Grace and Entry/Floors/" +
                    "Dungeon_Floor_Entry_5x5_A");
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(
                    floor.gameObject);

                Assert.That(
                    flags.HasFlag(StaticEditorFlags.ContributeGI),
                    Is.True);
                Assert.That(
                    flags.HasFlag(StaticEditorFlags.NavigationStatic),
                    Is.True);
                Assert.That(
                    scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                        .Any(component => component.GetType().Name == "NavMeshSurface"),
                    Is.True);
            });
        }

        private static void AssertZone(Transform location, string zoneName)
        {
            Transform zone = location.Find(zoneName);

            Assert.That(zone, Is.Not.Null, zoneName);
            Assert.That(zone.Find("Floors"), Is.Not.Null, zoneName + "/Floors");
            Assert.That(zone.Find("Walls"), Is.Not.Null, zoneName + "/Walls");
            Assert.That(zone.Find("Props"), Is.Not.Null, zoneName + "/Props");
        }

        private static Transform FindTransform(Scene scene, string path)
        {
            string[] segments = path.Split('/');
            Transform current = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == segments[0])
                ?.transform;
            for (int index = 1; index < segments.Length && current != null; index++)
            {
                current = current.Find(segments[index]);
            }

            return current;
        }

        private static Type GetRuntimeType(string fullName)
        {
            string assemblyName = fullName.StartsWith("Unity.Netcode.", StringComparison.Ordinal)
                ? "Unity.Netcode.Runtime"
                : "Assembly-CSharp";
            Type type = Type.GetType($"{fullName}, {assemblyName}");

            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static void WithWorldScene(Action<Scene> assertion)
        {
            Scene scene = SceneManager.GetSceneByPath(k_ScenePath);
            bool shouldCloseScene = !scene.IsValid() || !scene.isLoaded;
            if (shouldCloseScene)
            {
                scene = EditorSceneManager.OpenScene(
                    k_ScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                assertion(scene);
            }
            finally
            {
                if (shouldCloseScene)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
