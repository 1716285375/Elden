using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ.Editor
{
    public static class PlayerJumpSetup
    {
        private const string k_InputActionsPath = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_TagManagerPath = "ProjectSettings/TagManager.asset";
        private const string k_PhysicsManagerPath = "ProjectSettings/DynamicsManager.asset";
        private const string k_ControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/Humanoid Animator Controller.controller";
        private const string k_ClipFolder =
            "Assets/Art/Animations/Characters/Humanoid/Locomotion/";
        private const string k_ActionLayerName = "Action Override";
        private const string k_GroundedParameterName = "isGrounded";
        private const string k_InAirTimerParameterName = "inAirTimer";
        private const string k_EmptyStateName = "Empty";
        private const string k_FallingStateName = "Falling_01";
        private const string k_JumpStartStateName = "Jump Start";
        private const string k_JumpLiftStateName = "Jump Lift";
        private const string k_JumpIdleStateName = "Jump Idle";
        private const string k_JumpEndStateName = "Jump End";
        private const string k_GroundCheckPointName = "Ground Check Point";
        private const string k_ApplyJumpVelocityEventName = "ApplyJumpingVelocity";
        private const string k_CharacterLayerName = "Player";
        private const string k_DamageableCharacterLayerName = "Damageable Character";
        private const float k_FallingAnimationDelay = 0.25f;

        private static readonly StateDefinition[] s_StateDefinitions =
        {
            new StateDefinition(
                k_FallingStateName,
                k_ClipFolder + "core_main_jump_01_fall.anim",
                new Vector3(500f, -20f, 0f)),
            new StateDefinition(
                k_JumpStartStateName,
                k_ClipFolder + "core_main_jump_01_start.anim",
                new Vector3(500f, 90f, 0f)),
            new StateDefinition(
                k_JumpLiftStateName,
                k_ClipFolder + "core_main_jump_01_lift.anim",
                new Vector3(750f, 90f, 0f)),
            new StateDefinition(
                k_JumpIdleStateName,
                k_ClipFolder + "core_main_jump_01_idle.anim",
                new Vector3(1000f, 90f, 0f)),
            new StateDefinition(
                k_JumpEndStateName,
                k_ClipFolder + "core_main_jump_01_end.anim",
                new Vector3(750f, -20f, 0f))
        };

        [MenuItem("Tools/Elden/Configure Player Jumping")]
        public static void ConfigurePlayerJumping()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(k_ControllerPath);
            ConfigureJumpNetworking();
            ConfigureAnimator(controller);
            ConfigurePlayerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { k_ControllerPath, k_PlayerPrefabPath });
            ValidatePlayerJumping();
            Debug.Log("[PlayerJumpSetup] Configured ground, gravity, jump, and falling successfully.");
        }

        [MenuItem("Tools/Elden/Validate Player Jumping")]
        public static void ValidatePlayerJumping()
        {
            ValidateInputActions();
            ValidateAnimator(LoadRequiredAsset<AnimatorController>(k_ControllerPath));
            ValidatePlayerPrefab();
            ValidateJumpRules();
            ValidateJumpNetworking();
            Debug.Log(
                "[PlayerJumpValidation] Input, physics, network state, stamina, momentum, " +
                "and Animator are valid.");
        }

        /// <summary>
        /// Configures the character hitbox layer boundary used by networked players.
        /// </summary>
        [MenuItem("Tools/Elden/Configure Jump Networking Fix")]
        public static void ConfigureJumpNetworking()
        {
            int characterLayer = GetRequiredLayer(k_CharacterLayerName);
            int damageableCharacterLayer = EnsureLayer(k_DamageableCharacterLayerName);
            Physics.IgnoreLayerCollision(characterLayer, damageableCharacterLayer, true);
            UnityEngine.Object physicsManager =
                LoadRequiredSettingsAsset(k_PhysicsManagerPath);
            EditorUtility.SetDirty(physicsManager);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Validates jump-state authority and the character hitbox collision boundary.
        /// </summary>
        [MenuItem("Tools/Elden/Validate Jump Networking Fix")]
        public static void ValidateJumpNetworking()
        {
            int characterLayer = GetRequiredLayer(k_CharacterLayerName);
            int damageableCharacterLayer = GetRequiredLayer(k_DamageableCharacterLayerName);
            if (!Physics.GetIgnoreLayerCollision(characterLayer, damageableCharacterLayer) ||
                Physics.GetIgnoreLayerCollision(
                    damageableCharacterLayer,
                    damageableCharacterLayer))
            {
                throw new InvalidOperationException(
                    "Player must ignore Damageable Character without disabling self-collision " +
                    "for the damageable layer.");
            }

            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                CharacterNetworkManager networkManager =
                    playerRoot.GetComponent<CharacterNetworkManager>();
                if (networkManager == null ||
                    networkManager.IsJumping.ReadPerm !=
                        NetworkVariableReadPermission.Everyone ||
                    networkManager.IsJumping.WritePerm !=
                        NetworkVariableWritePermission.Owner ||
                    networkManager.IsJumping.Value)
                {
                    throw new InvalidOperationException(
                        "Jump state must default to false and be readable by everyone but " +
                        "writable only by the owner.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigureAnimator(AnimatorController controller)
        {
            EnsureParameter(
                controller,
                k_GroundedParameterName,
                AnimatorControllerParameterType.Bool);
            EnsureParameter(
                controller,
                k_InAirTimerParameterName,
                AnimatorControllerParameterType.Float);

            AnimatorStateMachine stateMachine = FindActionLayer(controller).stateMachine;
            AnimatorState emptyState = FindRequiredState(stateMachine, k_EmptyStateName);
            AnimatorState fallingState = GetOrCreateState(stateMachine, s_StateDefinitions[0]);
            AnimatorState jumpStartState = GetOrCreateState(stateMachine, s_StateDefinitions[1]);
            AnimatorState jumpLiftState = GetOrCreateState(stateMachine, s_StateDefinitions[2]);
            AnimatorState jumpIdleState = GetOrCreateState(stateMachine, s_StateDefinitions[3]);
            AnimatorState jumpEndState = GetOrCreateState(stateMachine, s_StateDefinitions[4]);

            ConfigureConditionalTransition(
                emptyState,
                fallingState,
                CreateCondition(
                    AnimatorConditionMode.IfNot,
                    0f,
                    k_GroundedParameterName),
                CreateCondition(
                    AnimatorConditionMode.Greater,
                    k_FallingAnimationDelay,
                    k_InAirTimerParameterName));
            ConfigureConditionalTransition(
                fallingState,
                jumpEndState,
                CreateCondition(
                    AnimatorConditionMode.If,
                    0f,
                    k_GroundedParameterName));
            ConfigureExitTransition(jumpStartState, jumpLiftState, 0.8f);
            ConfigureExitTransition(jumpLiftState, jumpIdleState, 0.8f);
            ConfigureConditionalTransition(
                jumpLiftState,
                jumpEndState,
                CreateCondition(
                    AnimatorConditionMode.If,
                    0f,
                    k_GroundedParameterName));
            ConfigureConditionalTransition(
                jumpIdleState,
                jumpEndState,
                CreateCondition(
                    AnimatorConditionMode.If,
                    0f,
                    k_GroundedParameterName));
            ConfigureExitTransition(jumpEndState, emptyState, 0.8f);

            EnsureStateBehaviour<ResetActionFlags>(emptyState);
            EnsureStateBehaviour<ResetJumpingState>(jumpEndState);
            EnsureJumpVelocityEvent(s_StateDefinitions[1].ClipPath);
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerLocomotionManager locomotion =
                    playerRoot.GetComponent<PlayerLocomotionManager>();
                if (locomotion == null)
                {
                    throw new InvalidOperationException(
                        "The Player prefab is missing PlayerLocomotionManager.");
                }

                Transform groundCheckPoint = playerRoot.transform.Find(k_GroundCheckPointName);
                if (groundCheckPoint == null)
                {
                    groundCheckPoint = new GameObject(k_GroundCheckPointName).transform;
                    groundCheckPoint.SetParent(playerRoot.transform, false);
                }

                groundCheckPoint.localPosition = new Vector3(0f, 0.1f, 0f);
                groundCheckPoint.localRotation = Quaternion.identity;
                groundCheckPoint.localScale = Vector3.one;
                groundCheckPoint.gameObject.layer = playerRoot.layer;

                SerializedObject serializedLocomotion = new SerializedObject(locomotion);
                SetObject(serializedLocomotion, "m_groundCheckPoint", groundCheckPoint);
                SetFloat(serializedLocomotion, "m_groundCheckRadius", 0.2f);
                SetInteger(serializedLocomotion, "m_groundLayers", 1);
                SetFloat(serializedLocomotion, "m_groundedYVelocity", -20f);
                SetFloat(serializedLocomotion, "m_fallStartYVelocity", -5f);
                SetFloat(serializedLocomotion, "m_gravityForce", -40f);
                SetFloat(serializedLocomotion, "m_jumpHeight", 2f);
                SetFloat(serializedLocomotion, "m_jumpForwardSpeed", 5f);
                SetFloat(serializedLocomotion, "m_freeFallingSpeed", 2f);
                SetFloat(serializedLocomotion, "m_sprintJumpMomentum", 1f);
                SetFloat(serializedLocomotion, "m_runJumpMomentum", 0.5f);
                SetFloat(serializedLocomotion, "m_walkJumpMomentum", 0.25f);
                SetFloat(serializedLocomotion, "m_jumpStaminaCost", 25f);
                serializedLocomotion.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(groundCheckPoint);
                EditorUtility.SetDirty(locomotion);
                PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateInputActions()
        {
            InputActionAsset inputActions = LoadRequiredAsset<InputActionAsset>(k_InputActionsPath);
            InputAction jump = inputActions.FindActionMap("Player Movement", true)
                .FindAction("Jump", true);
            if (jump.type != InputActionType.Button ||
                jump.expectedControlType != "Button" ||
                !HasBinding(jump, "<Gamepad>/buttonSouth") ||
                !HasBinding(jump, "<Keyboard>/f"))
            {
                throw new InvalidOperationException(
                    "Jump must be a Button action bound to Gamepad South and Keyboard F.");
            }
        }

        private static void ValidateAnimator(AnimatorController controller)
        {
            ValidateParameter(
                controller,
                k_GroundedParameterName,
                AnimatorControllerParameterType.Bool);
            ValidateParameter(
                controller,
                k_InAirTimerParameterName,
                AnimatorControllerParameterType.Float);

            AnimatorStateMachine stateMachine = FindActionLayer(controller).stateMachine;
            foreach (StateDefinition definition in s_StateDefinitions)
            {
                AnimatorState state = FindRequiredState(stateMachine, definition.StateName);
                if (AssetDatabase.GetAssetPath(state.motion) != definition.ClipPath)
                {
                    throw new InvalidOperationException(
                        $"{definition.StateName} must use {definition.ClipPath}.");
                }
            }

            AnimatorState jumpEndState = FindRequiredState(stateMachine, k_JumpEndStateName);
            if (!HasStateBehaviour<ResetJumpingState>(jumpEndState))
            {
                throw new InvalidOperationException(
                    "Jump End must clear the gameplay jump state on entry.");
            }

            AnimatorState emptyState = FindRequiredState(stateMachine, k_EmptyStateName);
            if (!HasStateBehaviour<ResetActionFlags>(emptyState))
            {
                throw new InvalidOperationException(
                    "Empty must retain the action and jump fail-safe reset.");
            }

            ValidateJumpVelocityEvent(s_StateDefinitions[1].ClipPath);
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                Transform groundCheckPoint = playerRoot.transform.Find(k_GroundCheckPointName);
                PlayerLocomotionManager locomotion =
                    playerRoot.GetComponent<PlayerLocomotionManager>();
                if (groundCheckPoint == null || locomotion == null)
                {
                    throw new InvalidOperationException(
                        "The Player prefab is missing its locomotion or ground check point.");
                }

                SerializedObject serializedLocomotion = new SerializedObject(locomotion);
                if (serializedLocomotion.FindProperty("m_groundCheckPoint").objectReferenceValue !=
                    groundCheckPoint ||
                    serializedLocomotion.FindProperty("m_groundLayers").intValue != 1 ||
                    serializedLocomotion.FindProperty("m_gravityForce").floatValue >= 0f ||
                    serializedLocomotion.FindProperty("m_freeFallingSpeed").floatValue >=
                    serializedLocomotion.FindProperty("m_runningSpeed").floatValue)
                {
                    throw new InvalidOperationException(
                        "The Player ground mask, gravity, or air control configuration is invalid.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateJumpRules()
        {
            Type locomotionType = typeof(PlayerLocomotionManager);
            MethodInfo canJump = GetRequiredMethod(locomotionType, "CanJump");
            if (Invoke<bool>(canJump, true, 100f, false, true) ||
                Invoke<bool>(canJump, false, 0f, false, true) ||
                Invoke<bool>(canJump, false, 100f, true, true) ||
                Invoke<bool>(canJump, false, 100f, false, false) ||
                !Invoke<bool>(canJump, false, 100f, false, true))
            {
                throw new InvalidOperationException(
                    "Jump rules must require ground contact, stamina, and no active action or jump.");
            }

            MethodInfo calculateVelocity = GetRequiredMethod(
                locomotionType,
                "CalculateJumpVelocity");
            float velocity = Invoke<float>(calculateVelocity, 2f, -40f);
            if (!Mathf.Approximately(velocity, Mathf.Sqrt(160f)))
            {
                throw new InvalidOperationException(
                    "Jump velocity must be derived from jump height and gravity.");
            }

            MethodInfo resolveMomentum = GetRequiredMethod(
                locomotionType,
                "ResolveJumpMomentumScale");
            float stationary = Invoke<float>(resolveMomentum, false, 0f, 1f, 0.5f, 0.25f);
            float walking = Invoke<float>(resolveMomentum, false, 0.5f, 1f, 0.5f, 0.25f);
            float running = Invoke<float>(resolveMomentum, false, 1f, 1f, 0.5f, 0.25f);
            float sprinting = Invoke<float>(resolveMomentum, true, 1f, 1f, 0.5f, 0.25f);
            if (!(stationary < walking && walking < running && running < sprinting))
            {
                throw new InvalidOperationException(
                    "Jump momentum must increase from stationary through sprinting.");
            }
        }

        private static AnimatorControllerLayer FindActionLayer(AnimatorController controller)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name == k_ActionLayerName)
                {
                    return layer;
                }
            }

            throw new InvalidOperationException(
                $"The Animator Controller is missing {k_ActionLayerName}.");
        }

        private static AnimatorState GetOrCreateState(
            AnimatorStateMachine stateMachine,
            StateDefinition definition)
        {
            AnimatorState state = FindState(stateMachine, definition.StateName);
            state ??= stateMachine.AddState(definition.StateName, definition.Position);
            state.motion = LoadRequiredAsset<AnimationClip>(definition.ClipPath);
            EditorUtility.SetDirty(state);
            return state;
        }

        private static AnimatorState FindRequiredState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return FindState(stateMachine, stateName) ??
                throw new InvalidOperationException(
                    $"The Animator Controller is missing state {stateName}.");
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state.name == stateName)
                {
                    return childState.state;
                }
            }

            return null;
        }

        private static void ConfigureConditionalTransition(
            AnimatorState source,
            AnimatorState destination,
            params AnimatorCondition[] conditions)
        {
            AnimatorStateTransition transition = GetOrCreateTransition(source, destination);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.1f;
            transition.conditions = conditions;
            EditorUtility.SetDirty(transition);
        }

        private static AnimatorCondition CreateCondition(
            AnimatorConditionMode mode,
            float threshold,
            string parameterName)
        {
            return new AnimatorCondition
            {
                mode = mode,
                threshold = threshold,
                parameter = parameterName
            };
        }

        private static void ConfigureExitTransition(
            AnimatorState source,
            AnimatorState destination,
            float exitTime)
        {
            AnimatorStateTransition transition = GetOrCreateTransition(source, destination);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = 0.05f;
            transition.conditions = Array.Empty<AnimatorCondition>();
            EditorUtility.SetDirty(transition);
        }

        private static AnimatorStateTransition GetOrCreateTransition(
            AnimatorState source,
            AnimatorState destination)
        {
            foreach (AnimatorStateTransition transition in source.transitions)
            {
                if (transition.destinationState == destination)
                {
                    return transition;
                }
            }

            return source.AddTransition(destination);
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == parameterName)
                {
                    if (parameter.type != parameterType)
                    {
                        throw new InvalidOperationException(
                            $"Animator parameter {parameterName} has the wrong type.");
                    }

                    return;
                }
            }

            controller.AddParameter(parameterName, parameterType);
        }

        private static void ValidateParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == parameterName && parameter.type == parameterType)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"The Animator Controller is missing {parameterName} ({parameterType}).");
        }

        private static void EnsureStateBehaviour<T>(AnimatorState state)
            where T : StateMachineBehaviour
        {
            if (!HasStateBehaviour<T>(state))
            {
                state.AddStateMachineBehaviour<T>();
            }
        }

        private static bool HasStateBehaviour<T>(AnimatorState state)
            where T : StateMachineBehaviour
        {
            foreach (StateMachineBehaviour behaviour in state.behaviours)
            {
                if (behaviour is T)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureJumpVelocityEvent(string clipPath)
        {
            AnimationClip clip = LoadRequiredAsset<AnimationClip>(clipPath);
            foreach (AnimationEvent animationEvent in AnimationUtility.GetAnimationEvents(clip))
            {
                if (animationEvent.functionName == k_ApplyJumpVelocityEventName)
                {
                    return;
                }
            }

            AnimationEvent takeOffEvent = new AnimationEvent
            {
                functionName = k_ApplyJumpVelocityEventName,
                time = Mathf.Clamp(clip.length * 0.25f, 0f, clip.length)
            };
            AnimationEvent[] existingEvents = AnimationUtility.GetAnimationEvents(clip);
            Array.Resize(ref existingEvents, existingEvents.Length + 1);
            existingEvents[^1] = takeOffEvent;
            AnimationUtility.SetAnimationEvents(clip, existingEvents);
            EditorUtility.SetDirty(clip);
        }

        private static void ValidateJumpVelocityEvent(string clipPath)
        {
            AnimationClip clip = LoadRequiredAsset<AnimationClip>(clipPath);
            int matchingEvents = 0;
            foreach (AnimationEvent animationEvent in AnimationUtility.GetAnimationEvents(clip))
            {
                if (animationEvent.functionName == k_ApplyJumpVelocityEventName)
                {
                    matchingEvents++;
                }
            }

            if (matchingEvents != 1)
            {
                throw new InvalidOperationException(
                    "Jump Start must contain exactly one ApplyJumpingVelocity event.");
            }
        }

        private static bool HasBinding(InputAction action, string bindingPath)
        {
            foreach (InputBinding binding in action.bindings)
            {
                if (string.Equals(
                    binding.path,
                    bindingPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static MethodInfo GetRequiredMethod(Type type, string methodName)
        {
            return type.GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static) ??
                throw new InvalidOperationException($"Could not find {type.Name}.{methodName}.");
        }

        private static T Invoke<T>(MethodInfo method, params object[] arguments)
        {
            object result = method.Invoke(null, arguments);
            return result is T typedResult
                ? typedResult
                : throw new InvalidOperationException($"{method.Name} returned an invalid result.");
        }

        private static int EnsureLayer(string layerName)
        {
            int existingLayer = LayerMask.NameToLayer(layerName);
            if (existingLayer >= 0)
            {
                return existingLayer;
            }

            UnityEngine.Object tagManager = LoadRequiredSettingsAsset(k_TagManagerPath);
            SerializedObject serializedTagManager = new SerializedObject(tagManager);
            SerializedProperty layers = serializedTagManager.FindProperty("layers") ??
                throw new InvalidOperationException("TagManager is missing its layers array.");
            for (int layerIndex = 8; layerIndex < layers.arraySize; layerIndex++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(layerIndex);
                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    continue;
                }

                layer.stringValue = layerName;
                serializedTagManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tagManager);
                AssetDatabase.SaveAssets();
                return layerIndex;
            }

            throw new InvalidOperationException(
                $"No empty user layer is available for '{layerName}'.");
        }

        private static int GetRequiredLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0
                ? layer
                : throw new InvalidOperationException(
                    $"Could not find the required '{layerName}' layer.");
        }

        private static UnityEngine.Object LoadRequiredSettingsAsset(string assetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            return assets.Length > 0
                ? assets[0]
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException($"Could not find {propertyName}.");
            property.floatValue = value;
        }

        private static void SetInteger(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException($"Could not find {propertyName}.");
            property.intValue = value;
        }

        private static void SetObject(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException($"Could not find {propertyName}.");
            property.objectReferenceValue = value;
        }

        private readonly struct StateDefinition
        {
            public StateDefinition(string stateName, string clipPath, Vector3 position)
            {
                StateName = stateName;
                ClipPath = clipPath;
                Position = position;
            }

            public string StateName { get; }
            public string ClipPath { get; }
            public Vector3 Position { get; }
        }
    }
}
