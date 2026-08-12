using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    public static class PlayerMovementSetup
    {
        private const string InputActionsPath = "Assets/PlayerControls.inputactions";
        private const string WrapperPath = "Assets/PlayerControls.cs";
        private const string PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string PlayerInputManagerPrefabPath = "Assets/Data/Prefabs/Word Managers/Player Input Manager.prefab";
        private const string MainMenuScenePath = "Assets/Scenes/Scene_Main_Menu_01.unity";
        private const string WorldScenePath = "Assets/Scenes/Scene_World_01.unity";
        [MenuItem("Tools/Elden/Generate Player Controls Class")]
        public static void GeneratePlayerControlsClass()
        {
            AssetImporter importer = AssetImporter.GetAtPath(InputActionsPath);
            if (importer == null)
            {
                Debug.LogError($"Could not find Input Actions asset at {InputActionsPath}.");
                return;
            }

            SerializedObject serializedImporter = new SerializedObject(importer);
            SetBool(serializedImporter, "m_GenerateWrapperCode", true);
            SetString(serializedImporter, "m_WrapperCodePath", WrapperPath);
            SetString(serializedImporter, "m_WrapperClassName", "PlayerControls");
            SetString(serializedImporter, "m_WrapperCodeNamespace", "ZZ");
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();

            importer.SaveAndReimport();
            AssetDatabase.Refresh();
            Debug.Log($"Generated {WrapperPath} from {InputActionsPath}.");
        }

        [MenuItem("Tools/Elden/Configure Player Movement")]
        public static void ConfigurePlayerMovement()
        {
            ConfigurePlayerPrefab();
            ConfigurePlayerInputManagerPrefab();
            ConfigureMainMenuScene();
            ConfigureWorldCameraScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Configured player input and locomotion successfully.");
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                CharacterManager legacyManager = playerRoot.GetComponent<CharacterManager>();
                if (legacyManager != null && legacyManager.GetType() == typeof(CharacterManager))
                {
                    Object.DestroyImmediate(legacyManager, true);
                }

                GetOrAddComponent<PlayerManager>(playerRoot);
                GetOrAddComponent<PlayerLocomotionManager>(playerRoot);
                GetOrAddComponent<NetworkObject>(playerRoot);
                RemoveDuplicateComponents<PlayerNetworkManager>(playerRoot);
                GetOrAddComponent<PlayerNetworkManager>(playerRoot);
                CharacterController controller = GetOrAddComponent<CharacterController>(playerRoot);
                controller.center = new Vector3(0f, 1f, 0f);
                controller.height = 2f;
                controller.radius = 0.35f;
                controller.slopeLimit = 45f;
                controller.stepOffset = 0.3f;

                PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigureMainMenuScene()
        {
            Scene scene = SceneManager.GetSceneByPath(MainMenuScenePath);
            bool sceneWasLoaded = scene.IsValid() && scene.isLoaded;

            if (!sceneWasLoaded)
            {
                scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameObject inputManagerObject = null;
                foreach (GameObject rootObject in scene.GetRootGameObjects())
                {
                    if (rootObject.name == "Player Input Manager")
                    {
                        inputManagerObject = rootObject;
                        break;
                    }
                }

                if (inputManagerObject == null)
                {
                    inputManagerObject = new GameObject("Player Input Manager");
                    SceneManager.MoveGameObjectToScene(inputManagerObject, scene);
                }

                GetOrAddComponent<PlayerInputManager>(inputManagerObject);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!sceneWasLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ConfigurePlayerInputManagerPrefab()
        {
            GameObject inputManagerRoot = PrefabUtility.LoadPrefabContents(PlayerInputManagerPrefabPath);

            try
            {
                PlayerInputManager inputManager = GetOrAddComponent<PlayerInputManager>(inputManagerRoot);
                EditorUtility.SetDirty(inputManager);
                PrefabUtility.SaveAsPrefabAsset(inputManagerRoot, PlayerInputManagerPrefabPath);
                AssetDatabase.ForceReserializeAssets(new[] { PlayerInputManagerPrefabPath });
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(inputManagerRoot);
            }
        }

        private static void ConfigureWorldCameraScene()
        {
            Scene scene = SceneManager.GetSceneByPath(WorldScenePath);
            bool sceneWasLoaded = scene.IsValid() && scene.isLoaded;

            if (!sceneWasLoaded)
            {
                scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Additive);
            }

            try
            {
                PlayerCamera playerCamera = null;
                foreach (GameObject rootObject in scene.GetRootGameObjects())
                {
                    playerCamera = rootObject.GetComponentInChildren<PlayerCamera>(true);
                    if (playerCamera != null)
                    {
                        break;
                    }
                }

                if (playerCamera == null)
                {
                    Debug.LogError($"Could not find PlayerCamera in {WorldScenePath}.");
                    return;
                }

                Camera cameraObject = playerCamera.GetComponentInChildren<Camera>(true);
                if (cameraObject == null)
                {
                    Debug.LogError($"Could not find a child Camera under PlayerCamera in {WorldScenePath}.");
                    return;
                }

                playerCamera.SetCameraObject(cameraObject);
                EditorUtility.SetDirty(playerCamera);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!sceneWasLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void RemoveDuplicateComponents<T>(GameObject gameObject) where T : Component
        {
            T[] components = gameObject.GetComponents<T>();
            for (int index = 1; index < components.Length; index++)
            {
                Object.DestroyImmediate(components[index], true);
            }
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }
    }
}
