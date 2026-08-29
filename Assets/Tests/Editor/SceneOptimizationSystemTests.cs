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
    public class SceneOptimizationSystemTests
    {
        private const string k_ScenePath =
            "Assets/Scenes/Levels/LV01_AbandonedMonastery/SCN_LV01_AbandonedMonastery.unity";
        private const string k_OptimizationPath =
            "World/Location 01 - Ashen Crypt/EP101 Scene Optimization";

        [Test]
        public void SceneContainsBakedOcclusionDataAndViewVolume()
        {
            WithWorldScene(scene =>
            {
                Transform optimization = FindTransform(scene, k_OptimizationPath);
                OcclusionArea area = optimization.GetComponentInChildren<OcclusionArea>(true);

                Assert.That(area, Is.Not.Null);
                Assert.That(area.size.x, Is.GreaterThanOrEqualTo(100f));
                FileInfo occlusionData = new(
                    "Assets/Scenes/Levels/LV01_AbandonedMonastery/Shared/Occlusion/OcclusionCullingData.asset");
                Assert.That(occlusionData.Exists, Is.True);
                Assert.That(occlusionData.Length, Is.GreaterThan(1024));
            });
        }

        [Test]
        public void CryptMonumentUsesDescendingLODComplexityAndImpostor()
        {
            WithWorldScene(scene =>
            {
                Transform monument = FindTransform(
                    scene,
                    k_OptimizationPath +
                    "/LOD and Impostor Objects/Distant Crypt Monument LOD");
                LODGroup lodGroup = monument.GetComponent<LODGroup>();
                LOD[] lods = lodGroup.GetLODs();
                int[] triangleCounts = lods
                    .Select(lod => lod.renderers.Single()
                        .GetComponent<MeshFilter>()
                        .sharedMesh.triangles.Length / 3)
                    .ToArray();

                Assert.That(lods, Has.Length.EqualTo(3));
                Assert.That(lods[0].screenRelativeTransitionHeight, Is.EqualTo(0.35f));
                Assert.That(lods[1].screenRelativeTransitionHeight, Is.EqualTo(0.12f));
                Assert.That(lods[2].screenRelativeTransitionHeight, Is.EqualTo(0.03f));
                Assert.That(triangleCounts[0], Is.GreaterThan(triangleCounts[1]));
                Assert.That(triangleCounts[1], Is.GreaterThan(triangleCounts[2]));
                Assert.That(lods[2].renderers.Single().name, Does.Contain("Impostor"));
            });
        }

        [Test]
        public void EveryLightActivationVolumeHasTriggerAndAuthoredLights()
        {
            WithWorldScene(scene =>
            {
                Type activationType = GetRuntimeType("ZZ.AreaLightActivationTrigger");
                Transform optimization = FindTransform(scene, k_OptimizationPath);
                Component[] activations = optimization
                    .GetComponentsInChildren(activationType, true)
                    .Cast<Component>()
                    .ToArray();

                Assert.That(activations, Has.Length.GreaterThanOrEqualTo(5));
                foreach (Component activation in activations)
                {
                    BoxCollider trigger = activation.GetComponent<BoxCollider>();
                    SerializedObject serializedActivation = new(activation);
                    SerializedProperty lights = serializedActivation.FindProperty(
                        "m_areaLights");

                    Assert.That(trigger, Is.Not.Null);
                    Assert.That(trigger.isTrigger, Is.True);
                    Assert.That(lights.arraySize, Is.GreaterThan(0));
                    for (int index = 0; index < lights.arraySize; index++)
                    {
                        Assert.That(
                            lights.GetArrayElementAtIndex(index).objectReferenceValue,
                            Is.Not.Null);
                    }
                }
            });
        }

        [TestCase(false, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        public void AreaLightsOnlyTrackTheLocalNetworkPlayer(
            bool isSpawned,
            bool isOwner,
            bool expected)
        {
            MethodInfo method = GetRuntimeType(
                "ZZ.AreaLightActivationTrigger").GetMethod(
                "ShouldTrackPlayer",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(
                method?.Invoke(null, new object[] { isSpawned, isOwner }),
                Is.EqualTo(expected));
        }

        [Test]
        public void OptimizationUsesStaticBatchingWithoutPerFrameLightPolling()
        {
            string source = File.ReadAllText(
                "Assets/_Game/Scripts/World/Managers/AreaLightActivationTrigger.cs");
            WithWorldScene(scene =>
            {
                Transform floor = FindTransform(
                    scene,
                    "World/Location 01 - Ashen Crypt/" +
                    "Sub Location 00 - Grace and Entry/Floors/" +
                    "Dungeon_Floor_Entry_5x5_A");
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(
                    floor.gameObject);

                Assert.That(flags.HasFlag(StaticEditorFlags.BatchingStatic), Is.True);
            });
            Assert.That(source, Does.Not.Contain("void Update()"));
            Assert.That(source, Does.Not.Contain("void LateUpdate()"));
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
            Type type = Type.GetType(fullName + ", Assembly-CSharp");

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
