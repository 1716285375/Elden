using System;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    public static class PlayerMovementSetup
    {
        private const string k_InputActionsPath = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_WrapperPath = "Assets/_Game/Scripts/Generated/Input/PlayerControls.cs";
        private const string k_PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_PlayerInputManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player Input Manager.prefab";
        private const string k_MainMenuScenePath = WorldScenePathLayout.MainMenuScenePath;
        private const string k_WorldScenePath = WorldScenePathLayout.MasterScenePath;
        private const string k_RunPlayerUpdatesInEditMode =
            "RUN_PLAYER_UPDATES_IN_EDIT_MODE";

        [ZZTool("角色与输入", "生成玩家输入类", 400)]
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

        [ZZTool("角色与输入", "配置玩家移动", 410)]
        public static void ConfigurePlayerMovement()
        {
            ConfigurePlayerPrefab();
            ConfigurePlayerInputManagerPrefab();
            ConfigureMainMenuScene();
            ConfigureWorldCameraScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Configured player input and locomotion successfully.");
        }

        [ZZTool("角色与输入", "验证玩家输入", 420)]
        public static void ValidatePlayerInput()
        {
            InputActionAsset inputActions =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(k_InputActionsPath);
            if (inputActions == null)
            {
                throw new InvalidOperationException(
                    $"Could not load Input Actions asset at {k_InputActionsPath}.");
            }

            InputActionMap movementMap = inputActions.FindActionMap("Player Movement", true);
            InputAction dodgeAction = movementMap.FindAction("Dodge", true);
            InputAction sprintAction = movementMap.FindAction("Sprint", true);

            ValidateDodgeAction(dodgeAction);
            ValidateSprintAction(sprintAction);
            ValidateDodgeSprintTiming();
            ValidateSprintRules();
            Debug.Log(
                "[PlayerInputValidation] Dodge Tap, Sprint Hold/release, and Sprint rules are valid.");
        }

        [ZZTool("角色与输入", "配置相机系统", 430)]
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

                // Camera feedback gets its own transform between the pivot and the camera because
                // the camera's local position is rewritten by collision handling every frame.
                Transform feedbackPivot = cameraPivot.Find("Feedback Pivot");
                if (feedbackPivot == null)
                {
                    feedbackPivot = new GameObject("Feedback Pivot").transform;
                }

                feedbackPivot.SetParent(cameraPivot, false);
                feedbackPivot.localPosition = Vector3.zero;
                feedbackPivot.localRotation = Quaternion.identity;
                feedbackPivot.localScale = Vector3.one;

                mainCamera.gameObject.name = "Main Camera";
                mainCamera.tag = "MainCamera";
                mainCamera.transform.SetParent(feedbackPivot, false);
                mainCamera.transform.localPosition = new Vector3(0f, 0f, -2.5f);
                mainCamera.transform.localRotation = Quaternion.identity;
                mainCamera.transform.localScale = Vector3.one;

                playerCamera.ConfigureRig(cameraPivot, feedbackPivot, mainCamera);
                EditorUtility.SetDirty(playerCamera);
                EditorUtility.SetDirty(cameraRoot);
                EditorUtility.SetDirty(cameraPivot);
                EditorUtility.SetDirty(feedbackPivot);
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

        private static void ValidateDodgeAction(InputAction dodgeAction)
        {
            if (dodgeAction.type != InputActionType.Button ||
                dodgeAction.expectedControlType != "Button" ||
                !HasBinding(dodgeAction, "<Gamepad>/buttonEast", "Tap"))
            {
                throw new InvalidOperationException(
                    "Dodge must be a Button action with a Tap binding on Gamepad East.");
            }
        }

        private static void ValidateSprintAction(InputAction sprintAction)
        {
            if (sprintAction.type != InputActionType.PassThrough ||
                sprintAction.expectedControlType != "Button" ||
                !ContainsInteraction(sprintAction.interactions, "Hold") ||
                !HasBinding(sprintAction, "<Gamepad>/buttonEast", string.Empty) ||
                !HasBinding(sprintAction, "<Keyboard>/leftShift", string.Empty))
            {
                throw new InvalidOperationException(
                    "Sprint must be a Pass Through Button action with Hold on East and Left Shift.");
            }
        }

        private static bool HasBinding(
            InputAction action,
            string path,
            string requiredInteraction)
        {
            foreach (InputBinding binding in action.bindings)
            {
                if (!string.Equals(binding.path, path, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return string.IsNullOrEmpty(requiredInteraction) ||
                    ContainsInteraction(binding.interactions, requiredInteraction);
            }

            return false;
        }

        private static bool ContainsInteraction(string interactions, string expectedInteraction)
        {
            return interactions?.IndexOf(
                expectedInteraction,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ValidateDodgeSprintTiming()
        {
            InputSettings.BackgroundBehavior previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            bool didRunPlayerUpdatesInEditMode = IsInputFeatureEnabled(
                k_RunPlayerUpdatesInEditMode);
            PlayerControls playerControls = null;
            Gamepad validationGamepad = null;
            int dodgePerformedCount = 0;
            int sprintPerformedCount = 0;
            int sprintCanceledCount = 0;

            try
            {
                InputSystem.settings.backgroundBehavior =
                    InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.SetInternalFeatureFlag(
                    k_RunPlayerUpdatesInEditMode,
                    true);
                playerControls = new PlayerControls();
                validationGamepad = InputSystem.AddDevice<Gamepad>();
                playerControls.PlayerMovement.Dodge.performed += _ => dodgePerformedCount++;
                playerControls.PlayerMovement.Sprint.performed += _ => sprintPerformedCount++;
                playerControls.PlayerMovement.Sprint.canceled += _ => sprintCanceledCount++;
                playerControls.Enable();

                SetEastButton(validationGamepad, true);
                bool didReadPressed = validationGamepad.buttonEast.isPressed;
                SetEastButton(validationGamepad, false);
                if (dodgePerformedCount != 1 || sprintPerformedCount != 0)
                {
                    throw new InvalidOperationException(
                        "A quick East Button tap must perform Dodge without performing Sprint. " +
                        $"Dodge={dodgePerformedCount}, Sprint={sprintPerformedCount}, " +
                        $"Pressed={didReadPressed}, DeviceEnabled={validationGamepad.enabled}, " +
                        $"DodgeControls={playerControls.PlayerMovement.Dodge.controls.Count}.");
                }

                dodgePerformedCount = 0;
                sprintPerformedCount = 0;
                sprintCanceledCount = 0;
                SetEastButton(validationGamepad, true);
                WaitForHoldInteraction();
                UpdateInputSystem();
                if (dodgePerformedCount != 0 || sprintPerformedCount != 1)
                {
                    throw new InvalidOperationException(
                        "Holding East Button must perform Sprint without performing Dodge. " +
                        $"Dodge={dodgePerformedCount}, Sprint={sprintPerformedCount}.");
                }

                SetEastButton(validationGamepad, false);
                if (sprintCanceledCount != 1)
                {
                    throw new InvalidOperationException(
                        "Releasing East Button after Hold must cancel Sprint.");
                }
            }
            finally
            {
                if (playerControls != null)
                {
                    playerControls.Disable();
                    Object.DestroyImmediate(playerControls.asset);
                }

                if (validationGamepad != null && validationGamepad.added)
                {
                    InputSystem.RemoveDevice(validationGamepad);
                }

                InputSystem.settings.SetInternalFeatureFlag(
                    k_RunPlayerUpdatesInEditMode,
                    didRunPlayerUpdatesInEditMode);
                InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            }
        }

        private static void SetEastButton(Gamepad gamepad, bool isPressed)
        {
            GamepadState gamepadState = new GamepadState();
            if (isPressed)
            {
                gamepadState = gamepadState.WithButton(GamepadButton.East);
            }

            InputSystem.QueueStateEvent(gamepad, gamepadState);
            UpdateInputSystem();
        }

        private static void UpdateInputSystem()
        {
            InputSystem.Update();
        }

        private static bool IsInputFeatureEnabled(string featureName)
        {
            MethodInfo isFeatureEnabled = typeof(InputSettings).GetMethod(
                "IsFeatureEnabled",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (isFeatureEnabled == null)
            {
                return false;
            }

            object result = isFeatureEnabled.Invoke(
                InputSystem.settings,
                new object[] { featureName });
            return result is bool isEnabled && isEnabled;
        }

        private static void WaitForHoldInteraction()
        {
            float waitSeconds = Mathf.Max(
                InputSystem.settings.defaultHoldTime,
                InputSystem.settings.defaultTapTime) + 0.1f;
            Thread.Sleep(Mathf.CeilToInt(waitSeconds * 1000f));
        }

        private static void ValidateSprintRules()
        {
            MethodInfo canSprint = typeof(PlayerLocomotionManager).GetMethod(
                "CanSprint",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (canSprint == null ||
                CanSprint(canSprint, false, false, 1f, 100f) ||
                CanSprint(canSprint, true, false, 0f, 100f) ||
                CanSprint(canSprint, true, true, 1f, 100f) ||
                CanSprint(canSprint, true, false, 1f, 0f) ||
                !CanSprint(canSprint, true, false, 0.5f, 100f))
            {
                throw new InvalidOperationException(
                    "Sprint rules must require held input, movement, stamina, and no active action.");
            }
        }

        private static bool CanSprint(
            MethodInfo canSprint,
            bool isSprintInputHeld,
            bool isPerformingAction,
            float moveAmount,
            float currentStamina)
        {
            object result = canSprint.Invoke(
                null,
                new object[]
                {
                    isSprintInputHeld,
                    isPerformingAction,
                    moveAmount,
                    currentStamina
                });
            return result is bool canSprintResult && canSprintResult;
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
