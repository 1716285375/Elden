using System;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the EP159-162 ladder resources.</summary>
    public static class LadderSystemSetup
    {
        private const string k_SourceControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_RuntimeControllerPath =
            "Assets/Data/Animations/Humanoid/Humanoid Runtime.controller";
        private const string k_LadderAnimationFolder =
            "Assets/Data/Animations/Ladders";
        private const string k_LadderPrefabPath =
            "Assets/Data/Prefabs/World Objects/Ladders/Standard Ladder.prefab";
        private const string k_PlayerPrefabPath =
            "Assets/Data/Prefabs/Player.prefab";
        private const string k_LadderLayerName = "Ladder Override";
        private const string k_SlidingParameter = "isSlidingDownLadder";
        private const float k_LadderHeight = 8f;
        private const float k_RungSpacing = 0.5f;

        private static readonly string[] s_overrideControllerPaths =
        {
            "Assets/Data/Animator Overrides/Weapons/" +
                "Unarmed Animator.overrideController",
            "Assets/Data/Animator Overrides/Weapons/" +
                "Broadsword Animator.overrideController",
            "Assets/Data/Animator Overrides/Weapons/" +
                "Straight Sword Animator.overrideController",
            "Assets/Data/Animator Overrides/Weapons/" +
                "Medium Shield Animator.overrideController",
            "Assets/Data/Animations/Archery/Bow.overrideController"
        };

        [MenuItem("Tools/ZZ/EP159-162/Configure Ladder System")]
        public static void ConfigureLadderSystem()
        {
            EnsureFolder("Assets/Data/Animations/Humanoid");
            EnsureFolder(k_LadderAnimationFolder);
            EnsureFolder("Assets/Data/Prefabs/World Objects/Ladders");
            AnimatorController controller = EnsureRuntimeController();
            ConfigureLadderLayer(controller);
            ConfigureOverrideControllers(controller);
            ConfigurePlayerPrefab(controller);
            ConfigureLadderPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateLadderSystem();
            Debug.Log(
                "[LadderSystemSetup] Configured EP159-162 fixed-rung ladder, " +
                "full-body animation layer, and multiplayer player controller.");
        }

        [MenuItem("Tools/ZZ/EP159-162/Validate Ladder System")]
        public static void ValidateLadderSystem()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_RuntimeControllerPath);
            ValidateLadderLayer(controller);
            ValidatePlayerControllers(controller);
            ValidateLadderPrefab();
            ValidateFallAnimationEvent();
            Debug.Log(
                "[LadderSystemSetup] EP159-162 ladder system validation passed.");
        }

        private static AnimatorController EnsureRuntimeController()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    k_RuntimeControllerPath);
            if (controller != null)
            {
                return controller;
            }

            AnimatorController source = LoadRequiredAsset<AnimatorController>(
                k_SourceControllerPath);
            if (!AssetDatabase.CopyAsset(
                k_SourceControllerPath,
                k_RuntimeControllerPath))
            {
                throw new InvalidOperationException(
                    $"Could not copy {source.name} to the runtime controller.");
            }

            AssetDatabase.ImportAsset(
                k_RuntimeControllerPath,
                ImportAssetOptions.ForceUpdate);
            controller = LoadRequiredAsset<AnimatorController>(
                k_RuntimeControllerPath);
            controller.name = "Humanoid Runtime";
            return controller;
        }

        private static void ConfigureLadderLayer(AnimatorController controller)
        {
            EnsureBoolParameter(controller, k_SlidingParameter);
            AnimatorControllerLayer layer = GetOrCreateLayer(controller);
            AnimatorStateMachine stateMachine = layer.stateMachine;
            AnimatorState empty = GetOrCreateState(stateMachine, "Empty");
            empty.motion = null;
            RemoveTransitions(empty);
            stateMachine.defaultState = empty;

            foreach (LadderStateDefinition definition in GetStateDefinitions())
            {
                AnimatorState state = GetOrCreateState(
                    stateMachine,
                    definition.StateName);
                state.motion = LoadRequiredAsset<AnimationClip>(
                    definition.ClipPath);
                state.speed = 1f;
                state.writeDefaultValues = true;
                RemoveTransitions(state);
                if (definition.HandState.HasValue)
                {
                    ConfigureExitBehaviour(
                        state,
                        definition.HandState.Value);
                }
            }

            foreach (AnimatorStateTransition transition in
                stateMachine.anyStateTransitions.ToArray())
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }

            EditorUtility.SetDirty(controller);
        }

        private static AnimatorControllerLayer GetOrCreateLayer(
            AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = Array.FindIndex(
                layers,
                layer => layer.name == k_LadderLayerName);
            if (layerIndex < 0)
            {
                AnimatorStateMachine stateMachine = new()
                {
                    name = k_LadderLayerName
                };
                AssetDatabase.AddObjectToAsset(stateMachine, controller);
                controller.AddLayer(new AnimatorControllerLayer
                {
                    name = k_LadderLayerName,
                    defaultWeight = 0f,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    stateMachine = stateMachine
                });
                layers = controller.layers;
                layerIndex = layers.Length - 1;
            }

            layers[layerIndex].defaultWeight = 0f;
            layers[layerIndex].blendingMode =
                AnimatorLayerBlendingMode.Override;
            layers[layerIndex].avatarMask = null;
            controller.layers = layers;
            return controller.layers[layerIndex];
        }

        private static void EnsureBoolParameter(
            AnimatorController controller,
            string parameterName)
        {
            AnimatorControllerParameter parameter = controller.parameters
                .FirstOrDefault(candidate => candidate.name == parameterName);
            if (parameter == null)
            {
                controller.AddParameter(
                    parameterName,
                    AnimatorControllerParameterType.Bool);
            }
            else if (parameter.type != AnimatorControllerParameterType.Bool)
            {
                throw new InvalidOperationException(
                    $"Animator parameter {parameterName} must be Boolean.");
            }
        }

        private static void ConfigureExitBehaviour(
            AnimatorState state,
            LadderHandState handState)
        {
            ToggleCanExitLadder[] behaviours = state.behaviours
                .OfType<ToggleCanExitLadder>()
                .ToArray();
            ToggleCanExitLadder behaviour = behaviours.FirstOrDefault();
            if (behaviour == null)
            {
                behaviour = state
                    .AddStateMachineBehaviour<ToggleCanExitLadder>();
            }

            behaviour.SetHandState(handState);
            foreach (ToggleCanExitLadder duplicate in behaviours.Skip(1))
            {
                UnityEngine.Object.DestroyImmediate(duplicate, true);
            }

            EditorUtility.SetDirty(state);
        }

        private static AnimationClip EnsureFallStartClip()
        {
            string targetPath =
                $"{k_LadderAnimationFolder}/Ladder Fall Start.anim";
            AnimationClip target = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                targetPath);
            if (target == null)
            {
                AnimationClip source = LoadRequiredAsset<AnimationClip>(
                    GetHumanoidClipPath(
                        "Locomotion/core_climb_fall_start_01.anim"));
                target = UnityEngine.Object.Instantiate(source);
                target.name = "Ladder Fall Start";
                AssetDatabase.CreateAsset(target, targetPath);
            }

            AnimationEvent fallEvent = new()
            {
                functionName = "FallFromLadderAnimationEvent",
                time = Mathf.Clamp(target.length * 0.72f, 0f, target.length)
            };
            AnimationUtility.SetAnimationEvents(
                target,
                new[] { fallEvent });
            EditorUtility.SetDirty(target);
            return target;
        }

        private static LadderStateDefinition[] GetStateDefinitions()
        {
            string interactions = "Interactions/";
            string locomotion = "Locomotion/";
            AnimationClip fallStart = EnsureFallStartClip();
            return new[]
            {
                Define(
                    "Enter Bottom",
                    interactions + "core_climb_up_enter_01.anim"),
                Define(
                    "Enter Top",
                    interactions + "core_climb_down_enter_01_RL.anim"),
                Define(
                    "Idle Left",
                    locomotion + "core_climb_idle_01_L.anim",
                    LadderHandState.Left),
                Define(
                    "Idle Right",
                    locomotion + "core_climb_idle_01_R.anim",
                    LadderHandState.Right),
                Define(
                    "Climb Up Left",
                    interactions + "core_climb_up_mid_01_L.anim"),
                Define(
                    "Climb Up Right",
                    interactions + "core_climb_up_mid_01_R.anim"),
                Define(
                    "Climb Down Left",
                    interactions + "core_climb_down_mid_01_L.anim"),
                Define(
                    "Climb Down Right",
                    interactions + "core_climb_down_mid_01_R.anim"),
                Define(
                    "Exit Top Left",
                    interactions + "core_climb_up_exit_01_L.anim"),
                Define(
                    "Exit Top Right",
                    interactions + "core_climb_up_exit_01_R.anim"),
                Define(
                    "Exit Bottom Left",
                    interactions + "core_climb_down_exit_01_L.anim"),
                Define(
                    "Exit Bottom Right",
                    interactions + "core_climb_down_exit_01_R.anim"),
                Define(
                    "Slide Start",
                    interactions + "core_climb_down_slide_01_start.anim"),
                Define(
                    "Slide Mid",
                    interactions + "core_climb_down_slide_01_mid.anim"),
                Define(
                    "Slide End",
                    interactions + "core_climb_down_slide_01_end.anim"),
                Define(
                    "Jump Off Start",
                    locomotion + "core_climb_down_jump_01_start.anim"),
                Define(
                    "Jump Off Mid",
                    locomotion + "core_climb_down_jump_01_mid.anim"),
                Define(
                    "Jump Off End",
                    locomotion + "core_climb_down_jump_01_end.anim"),
                new LadderStateDefinition(
                    "Fall Start",
                    AssetDatabase.GetAssetPath(fallStart)),
                Define(
                    "Fall Loop",
                    locomotion + "core_climb_down_jump_01_mid.anim")
            };
        }

        private static LadderStateDefinition Define(
            string stateName,
            string relativeClipPath,
            LadderHandState? handState = null)
        {
            return new LadderStateDefinition(
                stateName,
                GetHumanoidClipPath(relativeClipPath),
                handState);
        }

        private static string GetHumanoidClipPath(string relativePath)
        {
            return "Assets/Art/Animations/Characters/Humanoid/" +
                relativePath;
        }

        private static void ConfigureOverrideControllers(
            AnimatorController controller)
        {
            foreach (string path in s_overrideControllerPaths)
            {
                AnimatorOverrideController overrideController =
                    LoadRequiredAsset<AnimatorOverrideController>(path);
                overrideController.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(overrideController);
            }
        }

        private static void ConfigurePlayerPrefab(
            AnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerPrefabPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    throw new InvalidOperationException(
                        "Player prefab requires one child Animator.");
                }

                animator.runtimeAnimatorController = controller;
                PrefabUtility.SaveAsPrefabAsset(root, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureLadderPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_LadderPrefabPath);
            GameObject root = prefab != null
                ? PrefabUtility.LoadPrefabContents(k_LadderPrefabPath)
                : new GameObject(
                    "Standard Ladder",
                    typeof(NetworkObject),
                    typeof(Rigidbody));
            try
            {
                root.name = "Standard Ladder";
                Rigidbody rootRigidbody = GetOrAddComponent<Rigidbody>(root);
                rootRigidbody.isKinematic = true;
                rootRigidbody.useGravity = false;
                GetOrAddComponent<NetworkObject>(root);
                ConfigureLadderModel(root.transform);
                Transform horizontalPosition = GetOrCreateChild(
                    root.transform,
                    "Ladder Horizontal Position");
                horizontalPosition.localPosition = new Vector3(0f, 0f, -0.35f);
                horizontalPosition.localRotation = Quaternion.identity;
                Transform topExitLeft = GetOrCreateChild(
                    root.transform,
                    "Top Exit Left Hand Position");
                topExitLeft.localPosition = new Vector3(0f, k_LadderHeight, 0.8f);
                Transform topExitRight = GetOrCreateChild(
                    root.transform,
                    "Top Exit Right Hand Position");
                topExitRight.localPosition = new Vector3(
                    0f,
                    k_LadderHeight + 0.25f,
                    0.8f);
                Transform maximumExit = GetOrCreateChild(
                    root.transform,
                    "Maximum Top Exit Position");
                maximumExit.localPosition = new Vector3(
                    0f,
                    k_LadderHeight + 0.35f,
                    0.8f);

                ConfigureEntrance(
                    root.transform,
                    "Bottom Entrance",
                    false,
                    new Vector3(0f, 0f, -1f),
                    Quaternion.identity,
                    horizontalPosition,
                    topExitLeft,
                    topExitRight,
                    maximumExit);
                ConfigureEntrance(
                    root.transform,
                    "Top Entrance",
                    true,
                    new Vector3(0f, k_LadderHeight, 1f),
                    Quaternion.Euler(0f, 180f, 0f),
                    horizontalPosition,
                    topExitLeft,
                    topExitRight,
                    maximumExit);
                PrefabUtility.SaveAsPrefabAsset(root, k_LadderPrefabPath);
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

        private static void ConfigureLadderModel(Transform root)
        {
            Transform model = GetOrCreateChild(root, "Ladder Model");
            ConfigurePrimitive(
                model,
                "Left Rail",
                new Vector3(-0.65f, k_LadderHeight * 0.5f, 0f),
                new Vector3(0.12f, k_LadderHeight, 0.12f));
            ConfigurePrimitive(
                model,
                "Right Rail",
                new Vector3(0.65f, k_LadderHeight * 0.5f, 0f),
                new Vector3(0.12f, k_LadderHeight, 0.12f));
            int rungCount = Mathf.FloorToInt(k_LadderHeight / k_RungSpacing);
            for (int index = 0; index < rungCount; index++)
            {
                float height = (index + 1) * k_RungSpacing;
                ConfigurePrimitive(
                    model,
                    $"Rung {index + 1:00}",
                    new Vector3(0f, height, 0f),
                    new Vector3(1.3f, 0.08f, 0.1f));
            }
        }

        private static void ConfigurePrimitive(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale)
        {
            Transform primitive = FindDirectChild(parent, name);
            if (primitive == null)
            {
                GameObject gameObject = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                gameObject.name = name;
                primitive = gameObject.transform;
                primitive.SetParent(parent, false);
            }

            primitive.localPosition = localPosition;
            primitive.localRotation = Quaternion.identity;
            primitive.localScale = localScale;
        }

        private static void ConfigureEntrance(
            Transform root,
            string name,
            bool isTopEntrance,
            Vector3 localPosition,
            Quaternion localRotation,
            Transform horizontalPosition,
            Transform topExitLeft,
            Transform topExitRight,
            Transform maximumExit)
        {
            Transform entrance = GetOrCreateChild(root, name);
            entrance.localPosition = localPosition;
            entrance.localRotation = localRotation;
            entrance.localScale = Vector3.one;
            BoxCollider collider = GetOrAddComponent<BoxCollider>(
                entrance.gameObject);
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.size = new Vector3(2.2f, 2.2f, 1.4f);
            collider.isTrigger = true;
            Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(
                entrance.gameObject);
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            LadderInteractable interactable =
                GetOrAddComponent<LadderInteractable>(entrance.gameObject);
            Transform startPosition = GetOrCreateChild(
                entrance,
                "Start Position");
            startPosition.localPosition = Vector3.zero;
            startPosition.localRotation = Quaternion.identity;

            SerializedObject serialized = new(interactable);
            GetRequiredProperty(serialized, "m_interactableText")
                .stringValue = "Climb";
            GetRequiredProperty(serialized, "m_interactableCollider")
                .objectReferenceValue = collider;
            GetRequiredProperty(
                serialized,
                "m_autoDiscoverInteractableCollider").boolValue = false;
            GetRequiredProperty(serialized, "m_hostOnlyInteractable")
                .boolValue = false;
            GetRequiredProperty(
                serialized,
                "m_shouldDisableColliderAfterInteraction").boolValue = false;
            GetRequiredProperty(serialized, "m_isTopEntrance")
                .boolValue = isTopEntrance;
            GetRequiredProperty(serialized, "m_startPosition")
                .objectReferenceValue = startPosition;
            GetRequiredProperty(serialized, "m_ladderHorizontalPosition")
                .objectReferenceValue = horizontalPosition;
            GetRequiredProperty(serialized, "m_topExitLeftHandPosition")
                .objectReferenceValue = topExitLeft;
            GetRequiredProperty(serialized, "m_topExitRightHandPosition")
                .objectReferenceValue = topExitRight;
            GetRequiredProperty(serialized, "m_maxTopExitPosition")
                .objectReferenceValue = maximumExit;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateLadderLayer(AnimatorController controller)
        {
            AnimatorControllerLayer layer = controller.layers.FirstOrDefault(
                candidate => candidate.name == k_LadderLayerName);
            if (layer == null || layer.defaultWeight != 0f ||
                layer.blendingMode != AnimatorLayerBlendingMode.Override)
            {
                throw new InvalidOperationException(
                    "Ladder Override must be a zero-weight full-body override layer.");
            }

            AnimatorStateMachine stateMachine = layer.stateMachine;
            string[] stateNames = stateMachine.states
                .Select(child => child.state.name)
                .ToArray();
            string[] requiredStates = GetStateDefinitions()
                .Select(definition => definition.StateName)
                .Prepend("Empty")
                .ToArray();
            if (requiredStates.Any(state => !stateNames.Contains(state)) ||
                stateMachine.defaultState?.name != "Empty" ||
                stateMachine.states.Any(
                    child => child.state.transitions.Length > 0))
            {
                throw new InvalidOperationException(
                    "Ladder states must be complete and direct-play only.");
            }

            ValidateIdleBehaviour(stateMachine, "Idle Left", LadderHandState.Left);
            ValidateIdleBehaviour(
                stateMachine,
                "Idle Right",
                LadderHandState.Right);
        }

        private static void ValidateIdleBehaviour(
            AnimatorStateMachine stateMachine,
            string stateName,
            LadderHandState handState)
        {
            AnimatorState state = stateMachine.states
                .Select(child => child.state)
                .First(candidate => candidate.name == stateName);
            ToggleCanExitLadder[] behaviours = state.behaviours
                .OfType<ToggleCanExitLadder>()
                .ToArray();
            if (behaviours.Length != 1 || behaviours[0].HandState != handState)
            {
                throw new InvalidOperationException(
                    $"{stateName} requires one matching exit-window behaviour.");
            }
        }

        private static void ValidatePlayerControllers(
            AnimatorController controller)
        {
            GameObject playerPrefab = LoadRequiredAsset<GameObject>(
                k_PlayerPrefabPath);
            Animator playerAnimator = playerPrefab.GetComponentInChildren<Animator>(
                true);
            if (playerAnimator?.runtimeAnimatorController != controller)
            {
                throw new InvalidOperationException(
                    "Player prefab must use the tracked Humanoid Runtime controller.");
            }

            foreach (string path in s_overrideControllerPaths)
            {
                AnimatorOverrideController overrideController =
                    LoadRequiredAsset<AnimatorOverrideController>(path);
                if (overrideController.runtimeAnimatorController != controller)
                {
                    throw new InvalidOperationException(
                        $"{overrideController.name} must inherit Humanoid Runtime.");
                }
            }
        }

        private static void ValidateLadderPrefab()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(k_LadderPrefabPath);
            LadderInteractable[] entrances =
                prefab.GetComponentsInChildren<LadderInteractable>(true);
            NetworkObject[] networkObjects =
                prefab.GetComponentsInChildren<NetworkObject>(true);
            if (networkObjects.Length != 1 || networkObjects[0].gameObject != prefab ||
                entrances.Length != 2 ||
                entrances.Count(entrance => entrance.IsTopEntrance) != 1 ||
                entrances.Any(entrance =>
                    entrance.InteractableCollider == null ||
                    !entrance.InteractableCollider.isTrigger ||
                    entrance.GetComponent<Rigidbody>() == null ||
                    entrance.StartPosition == null ||
                    entrance.LadderHorizontalPosition == null))
            {
                throw new InvalidOperationException(
                    "Standard Ladder requires one root NetworkObject and two complete entrances.");
            }

            Transform model = prefab.transform.Find("Ladder Model");
            Transform[] rungs = model != null
                ? model.Cast<Transform>()
                    .Where(child => child.name.StartsWith("Rung "))
                    .OrderBy(child => child.localPosition.y)
                    .ToArray()
                : Array.Empty<Transform>();
            if (rungs.Length < 2 || rungs.Zip(rungs.Skip(1),
                    (first, second) => second.localPosition.y -
                        first.localPosition.y)
                .Any(spacing => !Mathf.Approximately(spacing, k_RungSpacing)))
            {
                throw new InvalidOperationException(
                    "Standard Ladder rungs must use one fixed spacing.");
            }
        }

        private static void ValidateFallAnimationEvent()
        {
            AnimationClip clip = LoadRequiredAsset<AnimationClip>(
                $"{k_LadderAnimationFolder}/Ladder Fall Start.anim");
            if (!AnimationUtility.GetAnimationEvents(clip).Any(
                animationEvent => animationEvent.functionName ==
                    "FallFromLadderAnimationEvent"))
            {
                throw new InvalidOperationException(
                    "Ladder Fall Start requires its gameplay release event.");
            }
        }

        private static AnimatorState GetOrCreateState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            AnimatorState state = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == stateName);
            return state ?? stateMachine.AddState(stateName);
        }

        private static void RemoveTransitions(AnimatorState state)
        {
            foreach (AnimatorStateTransition transition in
                state.transitions.ToArray())
            {
                state.RemoveTransition(transition);
            }
        }

        private static Transform GetOrCreateChild(
            Transform parent,
            string name)
        {
            Transform child = FindDirectChild(parent, name);
            if (child != null)
            {
                return child;
            }

            GameObject gameObject = new(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.transform;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            if (component == null)
            {
                throw new InvalidOperationException(
                    $"Failed to add {typeof(T).Name} to {gameObject.name}.");
            }

            return component;
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(
                propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} is missing " +
                    $"serialized property {propertyName}.");
            }

            return property;
        }

        private static T LoadRequiredAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required asset was not found at {path}.");
            }

            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parentPath = folderPath.Substring(
                0,
                folderPath.LastIndexOf('/'));
            string folderName = folderPath.Substring(
                folderPath.LastIndexOf('/') + 1);
            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private sealed class LadderStateDefinition
        {
            public LadderStateDefinition(
                string stateName,
                string clipPath,
                LadderHandState? handState = null)
            {
                StateName = stateName;
                ClipPath = clipPath;
                HandState = handState;
            }

            public string StateName { get; }
            public string ClipPath { get; }
            public LadderHandState? HandState { get; }
        }
    }
}
