using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    public static class PlayerMovementSetup
    {
        private const string k_InputActionsPath = "Assets/PlayerControls.inputactions";
        private const string k_WrapperPath = "Assets/PlayerControls.cs";
        private const string k_PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_PlayerInputManagerPrefabPath = "Assets/Data/Prefabs/Word Managers/Player Input Manager.prefab";
        private const string k_MainMenuScenePath = "Assets/Scenes/Scene_Main_Menu_01.unity";
        private const string k_WorldScenePath = "Assets/Scenes/Scene_World_01.unity";

        [MenuItem("Tools/Elden/Generate Player Controls Class")]
        public static void GeneratePlayerControlsClass()
        {
            AssetImporter importer = AssetImporter.GetAtPath(k_InputActionsPath);
            if (importer == null)
            {
                Debug.LogError($"Could not find Input Actions asset at {k_InputActionsPath}.");
                return;
            }

            SerializedObject serializedImporter = new SerializedObject(importer);
            SetBool(serializedImporter, "m_GenerateWrapperCode", true);
            SetString(serializedImporter, "m_WrapperCodePath", k_WrapperPath);
            SetString(serializedImporter, "m_WrapperClassName", "PlayerControls");
            SetString(serializedImporter, "m_WrapperCodeNamespace", "ZZ");
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();

            importer.SaveAndReimport();
            AssetDatabase.Refresh();
            Debug.Log($"Generated {k_WrapperPath} from {k_InputActionsPath}.");
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

        [MenuItem("Tools/Elden/Configure Camera System")]
        public static void ConfigureCameraSystem()
        {
            ConfigureCameraRig(k_MainMenuScenePath);
            ConfigureCameraRig(k_WorldScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[CameraSystemSetup] Configured Player Camera rigs in Main Menu and World scenes.");
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

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

                PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigureMainMenuScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_MainMenuScenePath);
            bool sceneWasLoaded = scene.IsValid() && scene.isLoaded;

            if (!sceneWasLoaded)
            {
                scene = EditorSceneManager.OpenScene(k_MainMenuScenePath, OpenSceneMode.Additive);
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
            GameObject inputManagerRoot = PrefabUtility.LoadPrefabContents(k_PlayerInputManagerPrefabPath);

            try
            {
                PlayerInputManager inputManager = GetOrAddComponent<PlayerInputManager>(inputManagerRoot);
                EditorUtility.SetDirty(inputManager);
                PrefabUtility.SaveAsPrefabAsset(inputManagerRoot, k_PlayerInputManagerPrefabPath);
                AssetDatabase.ForceReserializeAssets(new[] { k_PlayerInputManagerPrefabPath });
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(inputManagerRoot);
            }
        }

        private static void ConfigureWorldCameraScene()
        {
            ConfigureCameraRig(k_WorldScenePath);
        }

        private static void ConfigureCameraRig(string scenePath)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool sceneWasLoaded = scene.IsValid() && scene.isLoaded;

            if (!sceneWasLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                PlayerCamera playerCamera = null;
                Camera mainCamera = null;

                foreach (GameObject rootObject in scene.GetRootGameObjects())
                {
                    playerCamera ??= rootObject.GetComponentInChildren<PlayerCamera>(true);

                    Camera candidate = rootObject.GetComponentInChildren<Camera>(true);
                    if (candidate != null && (mainCamera == null || candidate.CompareTag("MainCamera")))
                    {
                        mainCamera = candidate;
                    }
                }

                if (mainCamera == null)
                {
                    Debug.LogError($"[CameraSystemSetup] Could not find a Camera in {scenePath}.");
                    return;
                }

                GameObject cameraRoot;
                if (playerCamera == null)
                {
                    cameraRoot = new GameObject("Player Camera");
                    SceneManager.MoveGameObjectToScene(cameraRoot, scene);
                    playerCamera = cameraRoot.AddComponent<PlayerCamera>();
                }
                else
                {
                    cameraRoot = playerCamera.gameObject;
                    cameraRoot.name = "Player Camera";
                }

                Transform cameraPivot = cameraRoot.transform.Find("Camera Pivot");
                if (cameraPivot == null)
                {
                    GameObject pivotObject = new GameObject("Camera Pivot");
                    cameraPivot = pivotObject.transform;
                }

                cameraRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                cameraRoot.transform.localScale = Vector3.one;

                cameraPivot.SetParent(cameraRoot.transform, false);
                cameraPivot.localPosition = new Vector3(0f, 1.65f, 0f);
                cameraPivot.localRotation = Quaternion.identity;
                cameraPivot.localScale = Vector3.one;

                mainCamera.gameObject.name = "Main Camera";
                mainCamera.tag = "MainCamera";
                mainCamera.transform.SetParent(cameraPivot, false);
                mainCamera.transform.localPosition = new Vector3(0f, 0f, -2.5f);
                mainCamera.transform.localRotation = Quaternion.identity;
                mainCamera.transform.localScale = Vector3.one;

                playerCamera.ConfigureRig(cameraPivot, mainCamera);
                EditorUtility.SetDirty(playerCamera);
                EditorUtility.SetDirty(cameraRoot);
                EditorUtility.SetDirty(cameraPivot);
                EditorUtility.SetDirty(mainCamera);
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
