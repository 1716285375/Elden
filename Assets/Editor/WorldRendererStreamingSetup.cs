using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Configures and validates EP151-152 Scene presentation managers.</summary>
    public static class WorldRendererStreamingSetup
    {
        private const string k_PersistentWorldScenePath =
            "Assets/Scenes/Scene_World_01.unity";
        private const string k_AreaSceneFolder = "Assets/Scenes/World Areas";
        private const string k_WorldLocationFolder =
            "Assets/Resources/World Locations";
        private const string k_WorldStreamingManagerName =
            "World Streaming Manager";
        private const string k_RendererManagerName =
            "World Location Renderer Manager";

        [MenuItem("Tools/Elden/Configure EP151-152 Renderer Streaming")]
        public static void ConfigureRendererStreaming()
        {
            IReadOnlyList<string> areaScenePaths = GetAreaScenePaths();
            if (areaScenePaths.Count == 0)
            {
                throw new InvalidOperationException(
                    "Configure EP149-150 world locations before EP151-152.");
            }

            Scene originalActiveScene = SceneManager.GetActiveScene();
            try
            {
                ConfigurePersistentLocationManager();
                foreach (string scenePath in areaScenePaths)
                {
                    ConfigureRendererScene(scenePath);
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

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateRendererStreaming();
            Debug.Log(
                "[WorldRendererStreamingSetup] Configured local Renderer " +
                "streaming for twenty additive Scenes. Spawner Roots remain active.");
        }

        [MenuItem("Tools/Elden/Validate EP151-152 Renderer Streaming")]
        public static void ValidateRendererStreaming()
        {
            ValidatePersistentLocationManager();
            IReadOnlyList<string> areaScenePaths = GetAreaScenePaths();
            if (areaScenePaths.Count != 20)
            {
                throw new InvalidOperationException(
                    $"Expected twenty world Scene slices, found " +
                    $"{areaScenePaths.Count}.");
            }

            foreach (string scenePath in areaScenePaths)
            {
                ValidateRendererScene(scenePath);
            }

            Debug.Log(
                "[WorldRendererStreamingValidation] EP151-152 Managers, Build " +
                "Indexes, Root caches, Renderer caches, and Spawner exclusions " +
                "are valid.");
        }

        private static void ConfigurePersistentLocationManager()
        {
            Scene scene = OpenSceneIfNeeded(
                k_PersistentWorldScenePath,
                out bool openedBySetup);
            try
            {
                GameObject managerObject = scene.GetRootGameObjects()
                    .FirstOrDefault(root =>
                        root.name == k_WorldStreamingManagerName) ??
                    throw new InvalidOperationException(
                        "Persistent World Streaming Manager is missing.");
                if (managerObject.GetComponent<WorldLocationManager>() == null)
                {
                    managerObject.AddComponent<WorldLocationManager>();
                    EditorUtility.SetDirty(managerObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                CloseSceneIfNeeded(scene, openedBySetup);
            }
        }

        private static void ConfigureRendererScene(string scenePath)
        {
            Scene scene = OpenSceneIfNeeded(scenePath, out bool openedBySetup);
            try
            {
                GameObject managerObject = scene.GetRootGameObjects()
                    .FirstOrDefault(root => root.name == k_RendererManagerName);
                if (managerObject == null)
                {
                    managerObject = new GameObject(k_RendererManagerName);
                    SceneManager.MoveGameObjectToScene(managerObject, scene);
                }

                WorldLocationRendererManager rendererManager =
                    managerObject.GetComponent<WorldLocationRendererManager>() ??
                    managerObject.AddComponent<WorldLocationRendererManager>();
                int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
                if (buildIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"Scene is missing from Build Settings: {scenePath}");
                }

                bool manageRootObjects = !IsSpawnerScene(scenePath);
                rendererManager.ConfigureScene(buildIndex, manageRootObjects);
                rendererManager.PrepareForGameMode();
                EditorUtility.SetDirty(managerObject);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                CloseSceneIfNeeded(scene, openedBySetup);
            }
        }

        private static void ValidatePersistentLocationManager()
        {
            Scene scene = OpenSceneIfNeeded(
                k_PersistentWorldScenePath,
                out bool openedByValidation);
            try
            {
                GameObject managerObject = scene.GetRootGameObjects()
                    .FirstOrDefault(root =>
                        root.name == k_WorldStreamingManagerName);
                if (managerObject?.GetComponent<WorldLocationManager>() == null)
                {
                    throw new InvalidOperationException(
                        "Persistent WorldLocationManager is missing.");
                }
            }
            finally
            {
                CloseSceneIfNeeded(scene, openedByValidation);
            }
        }

        private static void ValidateRendererScene(string scenePath)
        {
            Scene scene = OpenSceneIfNeeded(scenePath, out bool openedByValidation);
            try
            {
                WorldLocationRendererManager rendererManager = scene
                    .GetRootGameObjects()
                    .Select(root =>
                        root.GetComponent<WorldLocationRendererManager>())
                    .FirstOrDefault(manager => manager != null);
                int expectedBuildIndex = SceneUtility.GetBuildIndexByScenePath(
                    scenePath);
                bool expectedRootManagement = !IsSpawnerScene(scenePath);
                if (rendererManager == null ||
                    rendererManager.RendererSceneID != expectedBuildIndex ||
                    rendererManager.ManageRootObjects != expectedRootManagement)
                {
                    throw new InvalidOperationException(
                        $"Renderer Manager configuration is invalid: {scenePath}");
                }

                int expectedRootCount = scene.GetRootGameObjects()
                    .Count(root => root != rendererManager.gameObject);
                int expectedRendererCount = UnityEngine.Object
                    .FindObjectsByType<MeshRenderer>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Count(meshRenderer =>
                        meshRenderer.gameObject.scene == scene);
                if (rendererManager.RootObjects.Count != expectedRootCount ||
                    rendererManager.MeshRenderers.Count != expectedRendererCount ||
                    rendererManager.MeshRenderers.Any(meshRenderer =>
                        meshRenderer != null && meshRenderer.enabled))
                {
                    throw new InvalidOperationException(
                        $"Renderer Manager caches are incomplete: {scenePath}");
                }

                if (expectedRootManagement &&
                    rendererManager.RootObjects.Any(root =>
                        root != null && root.activeSelf))
                {
                    throw new InvalidOperationException(
                        $"Game Mode Roots must start disabled: {scenePath}");
                }

                if (!expectedRootManagement &&
                    rendererManager.RootObjects.Any(root =>
                        root != null && !root.activeSelf))
                {
                    throw new InvalidOperationException(
                        $"Spawner Roots must remain enabled: {scenePath}");
                }
            }
            finally
            {
                CloseSceneIfNeeded(scene, openedByValidation);
            }
        }

        private static IReadOnlyList<string> GetAreaScenePaths()
        {
            List<string> scenePaths = new();
            string[] locationGUIDs = AssetDatabase.FindAssets(
                "t:WorldLocationSceneSet",
                new[] { k_WorldLocationFolder });
            foreach (string locationGUID in locationGUIDs)
            {
                WorldLocationSceneSet location =
                    AssetDatabase.LoadAssetAtPath<WorldLocationSceneSet>(
                        AssetDatabase.GUIDToAssetPath(locationGUID));
                if (location == null)
                {
                    continue;
                }

                foreach (string sceneID in
                    location.ScenesRequiredForThisLocation)
                {
                    string scenePath =
                        $"{k_AreaSceneFolder}/{sceneID}.unity";
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) !=
                            null &&
                        !scenePaths.Contains(scenePath))
                    {
                        scenePaths.Add(scenePath);
                    }
                }
            }

            scenePaths.Sort(StringComparer.Ordinal);
            return scenePaths;
        }

        private static bool IsSpawnerScene(string scenePath)
        {
            return Path.GetFileNameWithoutExtension(scenePath).EndsWith(
                "_Spawners",
                StringComparison.OrdinalIgnoreCase);
        }

        private static Scene OpenSceneIfNeeded(
            string scenePath,
            out bool openedByCaller)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            openedByCaller = !scene.IsValid() || !scene.isLoaded;
            return openedByCaller
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)
                : scene;
        }

        private static void CloseSceneIfNeeded(Scene scene, bool openedByCaller)
        {
            if (openedByCaller && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
