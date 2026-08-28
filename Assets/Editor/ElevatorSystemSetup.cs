using System;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the reusable world assets required by EP145-148.</summary>
    public static class ElevatorSystemSetup
    {
        private const string k_PrefabFolder =
            "Assets/Data/Prefabs/World Objects/Elevator";
        private const string k_AnimationFolder =
            "Assets/Data/Animations/Environment/Elevator";
        private const string k_ElevatorPrefabPath =
            k_PrefabFolder + "/Elevator.prefab";
        private const string k_CallStationPrefabPath =
            k_PrefabFolder + "/Call Elevator.prefab";
        private const string k_LeverStationPrefabPath =
            k_PrefabFolder + "/Call Elevator Lever.prefab";
        private const string k_ButtonControllerPath =
            k_AnimationFolder + "/Elevator Button.controller";
        private const string k_LeverControllerPath =
            k_AnimationFolder + "/Elevator Lever.controller";

        [MenuItem("Tools/Elden/Configure Elevator System")]
        public static void ConfigureElevatorSystem()
        {
            EnsureFolder(k_PrefabFolder);
            EnsureFolder(k_AnimationFolder);
            AnimatorController buttonController = ConfigureButtonController();
            AnimatorController leverController = ConfigureLeverController();
            ConfigureElevatorPrefab(buttonController);
            ConfigureCallStationPrefab();
            ConfigureLeverStationPrefab(leverController);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateElevatorSystem();
            Debug.Log(
                "[ElevatorSystemSetup] Configured elevator, occupancy Trigger, " +
                "pressure button, call station, and synchronized lever assets.");
        }

        [MenuItem("Tools/Elden/Validate Elevator System")]
        public static void ValidateElevatorSystem()
        {
            GameObject elevatorPrefab = LoadRequiredAsset<GameObject>(
                k_ElevatorPrefabPath);
            ElevatorInteractable elevator = elevatorPrefab.GetComponent<
                ElevatorInteractable>();
            IsOnElevatorTrigger occupancy = elevatorPrefab
                .GetComponentInChildren<IsOnElevatorTrigger>(true);
            ElevatorButtonTrigger button = elevatorPrefab
                .GetComponentInChildren<ElevatorButtonTrigger>(true);
            Collider[] colliders = elevatorPrefab.GetComponentsInChildren<
                Collider>(true);
            if (elevator == null ||
                elevatorPrefab.GetComponent<NetworkObject>() == null ||
                elevatorPrefab.GetComponent<Rigidbody>() == null ||
                elevator.ElevatorPlatform == null ||
                elevator.InteractableCollider == null ||
                !elevator.InteractableCollider.isTrigger ||
                occupancy == null ||
                !occupancy.GetComponent<Collider>().isTrigger ||
                button == null ||
                !colliders.Any(collider => !collider.isTrigger))
            {
                throw new InvalidOperationException(
                    "Elevator prefab must separate physical, interaction, and " +
                    "occupancy Colliders under one NetworkObject.");
            }

            GameObject callStationPrefab = LoadRequiredAsset<GameObject>(
                k_CallStationPrefabPath);
            GameObject leverStationPrefab = LoadRequiredAsset<GameObject>(
                k_LeverStationPrefabPath);
            if (callStationPrefab.GetComponent<CallElevatorInteractable>() == null ||
                callStationPrefab.GetComponent<NetworkObject>() == null ||
                leverStationPrefab.GetComponent<
                    CallElevatorLeverInteractable>() == null ||
                leverStationPrefab.GetComponent<NetworkObject>() == null)
            {
                throw new InvalidOperationException(
                    "Call and lever station prefabs are incomplete.");
            }

            ValidateController(
                LoadRequiredAsset<AnimatorController>(k_ButtonControllerPath),
                "PushDown",
                "PushedDown",
                "Release");
            ValidateController(
                LoadRequiredAsset<AnimatorController>(k_LeverControllerPath),
                "PullLever",
                "ReleaseLever");
            Debug.Log(
                "[ElevatorSystemValidation] EP145-148 authored assets are valid.");
        }

        private static AnimatorController ConfigureButtonController()
        {
            AnimationClip idle = ConfigurePositionClip(
                k_AnimationFolder + "/Elevator Button Idle.anim",
                0f,
                0f,
                0.01f);
            AnimationClip pushDown = ConfigurePositionClip(
                k_AnimationFolder + "/Elevator Button Push Down.anim",
                0f,
                -0.12f,
                0.25f);
            AnimationClip pushedDown = ConfigurePositionClip(
                k_AnimationFolder + "/Elevator Button Pushed Down.anim",
                -0.12f,
                -0.12f,
                0.01f);
            AnimationClip release = ConfigurePositionClip(
                k_AnimationFolder + "/Elevator Button Release.anim",
                -0.12f,
                0f,
                0.25f);
            return ConfigureController(
                k_ButtonControllerPath,
                ("Idle", idle),
                ("PushDown", pushDown),
                ("PushedDown", pushedDown),
                ("Release", release));
        }

        private static AnimatorController ConfigureLeverController()
        {
            AnimationClip idle = ConfigureRotationClip(
                k_AnimationFolder + "/Elevator Lever Idle.anim",
                0f,
                0f,
                0.01f);
            AnimationClip pull = ConfigureRotationClip(
                k_AnimationFolder + "/Elevator Lever Pull.anim",
                0f,
                -55f,
                0.35f);
            AnimationClip release = ConfigureRotationClip(
                k_AnimationFolder + "/Elevator Lever Release.anim",
                -55f,
                0f,
                0.35f);
            return ConfigureController(
                k_LeverControllerPath,
                ("Idle", idle),
                ("PullLever", pull),
                ("ReleaseLever", release));
        }

        private static AnimationClip ConfigurePositionClip(
            string assetPath,
            float startHeight,
            float endHeight,
            float duration)
        {
            return ConfigureClip(
                assetPath,
                "m_LocalPosition.y",
                startHeight,
                endHeight,
                duration);
        }

        private static AnimationClip ConfigureRotationClip(
            string assetPath,
            float startAngle,
            float endAngle,
            float duration)
        {
            return ConfigureClip(
                assetPath,
                "localEulerAnglesRaw.z",
                startAngle,
                endAngle,
                duration);
        }

        private static AnimationClip ConfigureClip(
            string assetPath,
            string propertyName,
            float startValue,
            float endValue,
            float duration)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                assetPath);
            if (clip == null)
            {
                clip = new AnimationClip
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(assetPath)
                };
                AssetDatabase.CreateAsset(clip, assetPath);
            }

            clip.ClearCurves();
            AnimationCurve curve = AnimationCurve.Linear(
                0f,
                startValue,
                Mathf.Max(0.01f, duration),
                endValue);
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    string.Empty,
                    typeof(Transform),
                    propertyName),
                curve);
            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController ConfigureController(
            string controllerPath,
            params (string Name, AnimationClip Clip)[] states)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(
                    controllerPath);
            }

            AnimatorStateMachine stateMachine =
                controller.layers[0].stateMachine;
            AnimatorState idleState = null;
            foreach ((string stateName, AnimationClip clip) in states)
            {
                AnimatorState state = stateMachine.states
                    .Select(childState => childState.state)
                    .FirstOrDefault(candidate => candidate.name == stateName) ??
                    stateMachine.AddState(stateName);
                state.motion = clip;
                if (stateName == "Idle")
                {
                    idleState = state;
                }
            }

            stateMachine.defaultState = idleState ?? stateMachine.states[0].state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigureElevatorPrefab(
            RuntimeAnimatorController buttonController)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_ElevatorPrefabPath);
            GameObject root = prefab != null
                ? PrefabUtility.LoadPrefabContents(k_ElevatorPrefabPath)
                : new GameObject(
                    "Elevator",
                    typeof(NetworkObject),
                    typeof(Rigidbody),
                    typeof(ElevatorInteractable));
            try
            {
                root.name = "Elevator";
                ElevatorInteractable elevator = GetOrAddComponent<
                    ElevatorInteractable>(root);
                Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(root);
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                GetOrAddComponent<NetworkObject>(root);

                Transform platform = GetOrCreatePrimitive(
                    root.transform,
                    "Platform",
                    PrimitiveType.Cube);
                platform.localPosition = Vector3.zero;
                platform.localRotation = Quaternion.identity;
                platform.localScale = new Vector3(4f, 0.4f, 4f);
                BoxCollider physicalCollider = platform.GetComponent<BoxCollider>();
                physicalCollider.isTrigger = false;

                Transform interaction = GetOrCreateChild(
                    platform,
                    "Interaction Trigger",
                    typeof(BoxCollider));
                BoxCollider interactionCollider = GetOrAddComponent<BoxCollider>(
                    interaction.gameObject);
                interactionCollider.isTrigger = true;
                interactionCollider.center = new Vector3(0f, 1.2f, 0f);
                interactionCollider.size = new Vector3(3.5f, 2.4f, 3.5f);

                Transform occupancy = GetOrCreateChild(
                    platform,
                    "Occupancy Trigger",
                    typeof(BoxCollider),
                    typeof(IsOnElevatorTrigger));
                BoxCollider occupancyCollider = GetOrAddComponent<BoxCollider>(
                    occupancy.gameObject);
                occupancyCollider.isTrigger = true;
                occupancyCollider.center = new Vector3(0f, 1.1f, 0f);
                occupancyCollider.size = new Vector3(3.8f, 2.2f, 3.8f);
                IsOnElevatorTrigger occupancyTrigger = GetOrAddComponent<
                    IsOnElevatorTrigger>(occupancy.gameObject);

                Transform buttonTriggerTransform = GetOrCreateChild(
                    platform,
                    "Pressure Button Trigger",
                    typeof(BoxCollider),
                    typeof(ElevatorButtonTrigger));
                buttonTriggerTransform.localPosition = new Vector3(0f, 0.25f, 1.35f);
                BoxCollider buttonCollider = GetOrAddComponent<BoxCollider>(
                    buttonTriggerTransform.gameObject);
                buttonCollider.isTrigger = true;
                buttonCollider.size = new Vector3(0.9f, 0.35f, 0.9f);
                ElevatorButtonTrigger buttonTrigger = GetOrAddComponent<
                    ElevatorButtonTrigger>(buttonTriggerTransform.gameObject);
                Transform buttonVisual = GetOrCreateChild(
                    buttonTriggerTransform,
                    "Button Visual",
                    typeof(Animator));
                buttonVisual.localPosition = Vector3.zero;
                buttonVisual.localRotation = Quaternion.identity;
                buttonVisual.localScale = Vector3.one;
                Transform buttonModel = GetOrCreatePrimitive(
                    buttonVisual,
                    "Button Model",
                    PrimitiveType.Cylinder);
                buttonModel.localPosition = Vector3.zero;
                buttonModel.localRotation = Quaternion.identity;
                buttonModel.localScale = new Vector3(0.35f, 0.08f, 0.35f);
                RemoveCollider(buttonModel.gameObject);
                Animator buttonAnimator = GetOrAddComponent<Animator>(
                    buttonVisual.gameObject);
                buttonAnimator.runtimeAnimatorController = buttonController;

                SerializedObject serializedElevator = new(elevator);
                SetBaseInteractionProperties(
                    serializedElevator,
                    "Operate Elevator",
                    interactionCollider);
                GetRequiredProperty(serializedElevator, "m_elevatorPlatform")
                    .objectReferenceValue = platform;
                GetRequiredProperty(serializedElevator, "m_destinationLow")
                    .vector3Value = Vector3.zero;
                GetRequiredProperty(serializedElevator, "m_destinationHigh")
                    .vector3Value = new Vector3(0f, 8f, 0f);
                GetRequiredProperty(serializedElevator, "m_movementSpeed")
                    .floatValue = 2f;
                GetRequiredProperty(serializedElevator, "m_movementOffset")
                    .floatValue = 0.25f;
                serializedElevator.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedOccupancy = new(occupancyTrigger);
                GetRequiredProperty(serializedOccupancy, "m_elevator")
                    .objectReferenceValue = elevator;
                serializedOccupancy.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedButton = new(buttonTrigger);
                GetRequiredProperty(serializedButton, "m_elevator")
                    .objectReferenceValue = elevator;
                GetRequiredProperty(serializedButton, "m_buttonAnimator")
                    .objectReferenceValue = buttonAnimator;
                GetRequiredProperty(
                    serializedButton,
                    "m_minimumButtonReleaseTime").floatValue = 2f;
                serializedButton.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, k_ElevatorPrefabPath);
            }
            finally
            {
                if (prefab != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void ConfigureCallStationPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_CallStationPrefabPath);
            GameObject root = prefab != null
                ? PrefabUtility.LoadPrefabContents(k_CallStationPrefabPath)
                : new GameObject(
                    "Call Elevator",
                    typeof(NetworkObject),
                    typeof(Rigidbody),
                    typeof(BoxCollider),
                    typeof(CallElevatorInteractable));
            try
            {
                root.name = "Call Elevator";
                CallElevatorInteractable station = GetOrAddComponent<
                    CallElevatorInteractable>(root);
                GetOrAddComponent<NetworkObject>(root);
                Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(root);
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                BoxCollider interactionCollider = GetOrAddComponent<BoxCollider>(
                    root);
                interactionCollider.isTrigger = true;
                interactionCollider.center = new Vector3(0f, 1f, 0f);
                interactionCollider.size = new Vector3(1.2f, 2f, 1.2f);

                Transform visual = GetOrCreatePrimitive(
                    root.transform,
                    "Call Pedestal",
                    PrimitiveType.Cube);
                visual.localPosition = new Vector3(0f, 0.55f, 0f);
                visual.localScale = new Vector3(0.6f, 1.1f, 0.6f);
                RemoveCollider(visual.gameObject);

                SerializedObject serializedStation = new(station);
                SetBaseInteractionProperties(
                    serializedStation,
                    "Call Elevator",
                    interactionCollider);
                serializedStation.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_CallStationPrefabPath);
            }
            finally
            {
                if (prefab != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void ConfigureLeverStationPrefab(
            RuntimeAnimatorController leverController)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_LeverStationPrefabPath);
            GameObject root = prefab != null
                ? PrefabUtility.LoadPrefabContents(k_LeverStationPrefabPath)
                : new GameObject(
                    "Call Elevator Lever",
                    typeof(NetworkObject),
                    typeof(Rigidbody),
                    typeof(BoxCollider),
                    typeof(CallElevatorLeverInteractable));
            try
            {
                root.name = "Call Elevator Lever";
                CallElevatorLeverInteractable lever = GetOrAddComponent<
                    CallElevatorLeverInteractable>(root);
                GetOrAddComponent<NetworkObject>(root);
                Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(root);
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                BoxCollider interactionCollider = GetOrAddComponent<BoxCollider>(
                    root);
                interactionCollider.isTrigger = true;
                interactionCollider.center = new Vector3(0f, 1f, 0f);
                interactionCollider.size = new Vector3(1.2f, 2f, 1.2f);

                Transform pedestal = GetOrCreatePrimitive(
                    root.transform,
                    "Lever Pedestal",
                    PrimitiveType.Cube);
                pedestal.localPosition = new Vector3(0f, 0.45f, 0f);
                pedestal.localScale = new Vector3(0.55f, 0.9f, 0.55f);
                RemoveCollider(pedestal.gameObject);

                Transform pivot = GetOrCreateChild(
                    root.transform,
                    "Lever Pivot",
                    typeof(Animator));
                pivot.localPosition = new Vector3(0f, 1.1f, 0f);
                pivot.localRotation = Quaternion.identity;
                Animator animator = GetOrAddComponent<Animator>(pivot.gameObject);
                animator.runtimeAnimatorController = leverController;
                Transform handle = GetOrCreatePrimitive(
                    pivot,
                    "Lever Handle",
                    PrimitiveType.Cube);
                handle.localPosition = new Vector3(0f, 0.45f, 0f);
                handle.localScale = new Vector3(0.12f, 0.9f, 0.12f);
                RemoveCollider(handle.gameObject);

                SerializedObject serializedLever = new(lever);
                SetBaseInteractionProperties(
                    serializedLever,
                    "Pull Lever",
                    interactionCollider);
                GetRequiredProperty(serializedLever, "m_leverAnimator")
                    .objectReferenceValue = animator;
                GetRequiredProperty(
                    serializedLever,
                    "m_timeToWaitAfterPullingLeverToMoveElevator").floatValue =
                        1f;
                serializedLever.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_LeverStationPrefabPath);
            }
            finally
            {
                if (prefab != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void SetBaseInteractionProperties(
            SerializedObject serializedInteractable,
            string prompt,
            Collider interactionCollider)
        {
            GetRequiredProperty(serializedInteractable, "m_interactableText")
                .stringValue = prompt;
            GetRequiredProperty(serializedInteractable, "m_interactableCollider")
                .objectReferenceValue = interactionCollider;
            GetRequiredProperty(serializedInteractable, "m_hostOnlyInteractable")
                .boolValue = false;
            GetRequiredProperty(
                serializedInteractable,
                "m_shouldDisableColliderAfterInteraction").boolValue = false;
        }

        private static void ValidateController(
            AnimatorController controller,
            params string[] requiredStates)
        {
            string[] stateNames = controller.layers[0].stateMachine.states
                .Select(childState => childState.state.name)
                .ToArray();
            if (requiredStates.Any(stateName => !stateNames.Contains(stateName)))
            {
                throw new InvalidOperationException(
                    $"{controller.name} is missing a required animation state.");
            }
        }

        private static Transform GetOrCreatePrimitive(
            Transform parent,
            string childName,
            PrimitiveType primitiveType)
        {
            Transform existing = FindDirectChild(parent, childName);
            if (existing != null)
            {
                return existing;
            }

            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = childName;
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform GetOrCreateChild(
            Transform parent,
            string childName,
            params Type[] componentTypes)
        {
            Transform existing = FindDirectChild(parent, childName);
            if (existing != null)
            {
                return existing;
            }

            GameObject child = new(childName, componentTypes);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform FindDirectChild(
            Transform parent,
            string childName)
        {
            for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
            {
                Transform child = parent.GetChild(childIndex);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int segmentIndex = 1;
                segmentIndex < segments.Length;
                segmentIndex++)
            {
                string nextPath = currentPath + "/" + segments[segmentIndex];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[segmentIndex]);
                }

                currentPath = nextPath;
            }
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.name} is missing " +
                    $"{propertyName}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }
    }
}
