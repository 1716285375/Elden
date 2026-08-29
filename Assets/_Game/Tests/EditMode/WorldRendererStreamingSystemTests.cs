using System;
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
    /// <summary>Focused EP151-152 Renderer/Collider decoupling validation.</summary>
    public static class WorldRendererStreamingSystemTests
    {
        [Test]
        public static void RendererVisibilityDoesNotDisableColliderOrGameObject()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            try
            {
                GameObject managerObject = new("Renderer Manager");
                SceneManager.MoveGameObjectToScene(managerObject, scene);
                Component rendererManager = managerObject.AddComponent(
                    GetRuntimeType("ZZ.WorldLocationRendererManager"));
                GameObject geometry = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(geometry, scene);

                Invoke(rendererManager, "RefreshSceneObjects");
                Invoke(rendererManager, "ToggleAllMeshRenderers", false);

                Assert.That(geometry.activeSelf, Is.True);
                Assert.That(geometry.GetComponent<BoxCollider>().enabled, Is.True);
                Assert.That(geometry.GetComponent<MeshRenderer>().enabled, Is.False);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public static void SpawnerSceneRejectsRootObjectDisabling()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            try
            {
                GameObject managerObject = new("Renderer Manager");
                SceneManager.MoveGameObjectToScene(managerObject, scene);
                Component rendererManager = managerObject.AddComponent(
                    GetRuntimeType("ZZ.WorldLocationRendererManager"));
                Invoke(rendererManager, "ConfigureScene", -1, false);
                GameObject networkSpawnerRoot = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(networkSpawnerRoot, scene);

                Invoke(rendererManager, "PrepareForGameMode");

                Assert.That(networkSpawnerRoot.activeSelf, Is.True);
                Assert.That(
                    networkSpawnerRoot.GetComponent<MeshRenderer>().enabled,
                    Is.False);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public static void EveryOwnedSceneIDMapsToABuildIndex()
        {
            HashSet<string> sceneIDs = GetWorldScenePaths()
                .Select(Path.GetFileNameWithoutExtension)
                .ToHashSet();
            MethodInfo buildIndexMethod = GetRuntimeType(
                    "ZZ.WorldSceneManager")
                .GetMethod(
                    "GetBuildIndexFromSceneID",
                    BindingFlags.Public | BindingFlags.Static);

            Assert.That(sceneIDs, Has.Count.EqualTo(20));
            Assert.That(buildIndexMethod, Is.Not.Null);
            foreach (string sceneID in sceneIDs)
            {
                Assert.That(
                    (int)buildIndexMethod.Invoke(null, new object[] { sceneID }),
                    Is.GreaterThanOrEqualTo(0),
                    sceneID);
            }
        }

        [Test]
        public static void LocalRendererRefreshUsesLoadCompleteAndCancelsOldWork()
        {
            string worldSceneManagerSource = ReadAssetText(
                "Assets/_Game/Scripts/World/Managers/WorldSceneManager.cs");
            string locationManagerSource = ReadAssetText(
                "Assets/_Game/Scripts/World/Managers/WorldSceneSubSceneManager.cs");

            Assert.That(
                worldSceneManagerSource,
                Does.Contain("SceneEventType.LoadComplete"));
            Assert.That(
                worldSceneManagerSource,
                Does.Contain("m_requiredRenderersCoroutine"));
            Assert.That(
                worldSceneManagerSource,
                Does.Contain("StopCoroutine(m_requiredRenderersCoroutine)"));
            Assert.That(
                worldSceneManagerSource,
                Does.Contain("GetBuildIndexFromSceneID"));
            Assert.That(
                locationManagerSource,
                Does.Contain("worldLocationID"));
            Assert.That(
                locationManagerSource,
                Does.Contain("localPlayer.SetAreaCurrentlyIn(localLocation)"));
        }

        [Test]
        public static void EditorAutomationAndModesAreAvailable()
        {
            Type rendererEditorType = GetEditorType(
                "ZZ.Editor.WorldLocationRendererManagerEditor");
            Type locationEditorType = GetEditorType(
                "ZZ.Editor.WorldLocationManagerEditor");
            Assert.That(rendererEditorType, Is.Not.Null);
            Assert.That(locationEditorType, Is.Not.Null);
            Assert.That(
                GetRuntimeType("ZZ.WorldLocationManager").GetMethod(
                    "EnableGameMode"),
                Is.Not.Null);
            Assert.That(
                GetRuntimeType("ZZ.WorldLocationManager").GetMethod(
                    "EnableLightBakeMode"),
                Is.Not.Null);
            Assert.That(
                GetRuntimeType("ZZ.WorldLocationRendererManager").GetMethod(
                    "ToggleAllMeshRenderersOverTime"),
                Is.Not.Null);
        }

        [Test]
        public static void AllWorldScenesContainConfiguredRendererManagers()
        {
            IReadOnlyList<string> scenePaths = GetWorldScenePaths();
            Type rendererManagerType = GetRuntimeType(
                "ZZ.WorldLocationRendererManager");
            Assert.That(scenePaths.Count, Is.EqualTo(20));
            foreach (string scenePath in scenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
                try
                {
                    Component rendererManager = scene
                        .GetRootGameObjects()
                        .Select(root => root.GetComponent(rendererManagerType))
                        .FirstOrDefault(manager => manager != null);
                    Assert.That(rendererManager, Is.Not.Null, scenePath);
                    Assert.That(
                        GetProperty<int>(rendererManager, "RendererSceneID"),
                        Is.EqualTo(
                            SceneUtility.GetBuildIndexByScenePath(scenePath)),
                        scenePath);
                    Assert.That(
                        GetProperty<bool>(rendererManager, "ManageRootObjects"),
                        Is.EqualTo(!scenePath.EndsWith(
                            "_Spawners.unity",
                            StringComparison.OrdinalIgnoreCase)),
                        scenePath);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>Runs focused checks and the prior streaming regression suite.</summary>
        public static void RunAllFocusedTests()
        {
            RendererVisibilityDoesNotDisableColliderOrGameObject();
            SpawnerSceneRejectsRootObjectDisabling();
            EveryOwnedSceneIDMapsToABuildIndex();
            LocalRendererRefreshUsesLoadCompleteAndCancelsOldWork();
            EditorAutomationAndModesAreAvailable();
            AllWorldScenesContainConfiguredRendererManagers();
            LargeWorldStreamingSystemTests.RunAllFocusedTests();
            WorldStreamingSystemTests
                .PersistentWorldAndReusableTriggerAreConfigured();
            Debug.Log(
                "[WorldRendererStreamingSystemTests] Six EP151-152 checks " +
                "and fifteen world-streaming regressions passed.");
        }

        private static IReadOnlyList<string> GetWorldScenePaths()
        {
            return AssetDatabase.FindAssets(
                    "t:Scene",
                    new[] { "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Contains("/Regions/"))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static string ReadAssetText(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return File.ReadAllText(Path.Combine(projectRoot, assetPath));
        }

        private static Type GetEditorType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type runtimeType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
            return runtimeType ?? throw new InvalidOperationException(
                $"Runtime type not found: {fullName}");
        }

        private static void Invoke(
            Component target,
            string methodName,
            params object[] parameters)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, parameters);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }
    }
}
