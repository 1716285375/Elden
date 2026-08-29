using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    public static class PlayerActionSetup
    {
        private const float k_RollSoundEventTimeSeconds = 0.26666668f;
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_RollStateName = "Roll_Forward_01";
        private const string k_BackStepStateName = "Back_Step_01";
        private const string k_ControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/Humanoid Animator Controller.controller";
        private const string k_RollClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Locomotion/" +
            "core_main_roll_med_to_idle_F_01.anim";
        private const string k_BackStepClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Actions/" +
            "core_main_back_step_medium_02.anim";
        private const string k_RollSoundFXPath =
            "Assets/Art/Audio/SFX/General/SFX_Roll_01.wav";
        private const string k_PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_MainMenuScenePath = WorldScenePathLayout.MainMenuScenePath;

        [MenuItem("Tools/Elden/Configure Player Actions")]
        public static void ConfigurePlayerActions()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(k_ControllerPath);
            AnimationClip rollClip = LoadRequiredAsset<AnimationClip>(k_RollClipPath);
            AnimationClip backStepClip = LoadRequiredAsset<AnimationClip>(k_BackStepClipPath);
            AudioClip rollingSoundFX = LoadRequiredAsset<AudioClip>(k_RollSoundFXPath);

            ConfigureActionLayer(controller, rollClip, backStepClip);
            ConfigureAnimationEvents(rollClip, backStepClip);
            ConfigurePlayerPrefab();
            ConfigureWorldSoundFXManager(rollingSoundFX);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidatePlayerActions();
            Debug.Log("[PlayerActionSetup] Configured Dodge actions, Root Motion, RPC playback, and rolling SFX.");
        }

        [MenuItem("Tools/Elden/Validate Player Actions")]
        public static void ValidatePlayerActions()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(k_ControllerPath);
            AnimationClip rollClip = LoadRequiredAsset<AnimationClip>(k_RollClipPath);
            AnimationClip backStepClip = LoadRequiredAsset<AnimationClip>(k_BackStepClipPath);
            AudioClip rollingSoundFX = LoadRequiredAsset<AudioClip>(k_RollSoundFXPath);

            ValidateActionLayer(controller, rollClip, backStepClip);
            ValidateAnimationEvents(rollClip, backStepClip);
            ValidatePlayerPrefab();
            ValidateWorldSoundFXManager(rollingSoundFX);
            Debug.Log("[PlayerActionValidation] Player Action System assets are valid.");
        }

        private static void ConfigureActionLayer(
            AnimatorController controller,
            AnimationClip rollClip,
            AnimationClip backStepClip)
        {
            AnimatorControllerLayer actionLayer = GetOrCreateActionLayer(controller);
            AnimatorStateMachine stateMachine = actionLayer.stateMachine;
            AnimatorState emptyState = GetOrCreateState(stateMachine, k_EmptyStateName);
            AnimatorState rollState = GetOrCreateState(stateMachine, k_RollStateName);
            AnimatorState backStepState = GetOrCreateState(stateMachine, k_BackStepStateName);

            emptyState.motion = null;
            rollState.motion = rollClip;
            backStepState.motion = backStepClip;
            stateMachine.defaultState = emptyState;

            EnsureResetActionFlags(emptyState);
            ConfigureExitTransition(rollState, emptyState);
            ConfigureExitTransition(backStepState, emptyState);

            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(emptyState);
            EditorUtility.SetDirty(rollState);
            EditorUtility.SetDirty(backStepState);
            EditorUtility.SetDirty(controller);
        }

        private static AnimatorControllerLayer GetOrCreateActionLayer(AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            for (int index = 0; index < layers.Length; index++)
            {
                if (layers[index].name != k_ActionLayerName)
                {
                    continue;
                }

                layers[index].defaultWeight = 1f;
                layers[index].avatarMask = null;
                layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
                controller.layers = layers;
                return controller.layers[index];
            }

            controller.AddLayer(k_ActionLayerName);
            layers = controller.layers;
            int actionLayerIndex = layers.Length - 1;
            layers[actionLayerIndex].defaultWeight = 1f;
            layers[actionLayerIndex].avatarMask = null;
            layers[actionLayerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;
            return controller.layers[actionLayerIndex];
        }

        private static AnimatorState GetOrCreateState(
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

            return stateMachine.AddState(stateName);
        }

        private static void EnsureResetActionFlags(AnimatorState emptyState)
        {
            foreach (StateMachineBehaviour behaviour in emptyState.behaviours)
            {
                if (behaviour is ResetActionFlags)
                {
                    return;
                }
            }

            emptyState.AddStateMachineBehaviour<ResetActionFlags>();
        }

        private static void ConfigureExitTransition(
            AnimatorState actionState,
            AnimatorState emptyState)
        {
            AnimatorStateTransition exitTransition = null;
            foreach (AnimatorStateTransition transition in actionState.transitions)
            {
                if (transition.destinationState == emptyState)
                {
                    exitTransition = transition;
                    break;
                }
            }

            exitTransition ??= actionState.AddTransition(emptyState);
            exitTransition.hasExitTime = true;
            exitTransition.exitTime = 1f;
            exitTransition.hasFixedDuration = true;
            exitTransition.duration = 0.1f;
            exitTransition.interruptionSource = TransitionInterruptionSource.None;
            exitTransition.canTransitionToSelf = false;
            EditorUtility.SetDirty(exitTransition);
        }

        private static void ConfigureAnimationEvents(
            AnimationClip rollClip,
            AnimationClip backStepClip)
        {
            AnimationEvent rollingSoundEvent = new AnimationEvent
            {
                time = Mathf.Min(k_RollSoundEventTimeSeconds, rollClip.length),
                functionName = nameof(CharacterSoundFXManager.PlayRollingSoundFX),
                messageOptions = SendMessageOptions.RequireReceiver
            };

            List<AnimationEvent> rollEvents = GetEventsWithoutRollingSoundFX(rollClip);
            rollEvents.Add(rollingSoundEvent);
            rollEvents.Sort((first, second) => first.time.CompareTo(second.time));

            AnimationUtility.SetAnimationEvents(rollClip, rollEvents.ToArray());
            AnimationUtility.SetAnimationEvents(
                backStepClip,
                GetEventsWithoutRollingSoundFX(backStepClip).ToArray());
            EditorUtility.SetDirty(rollClip);
            EditorUtility.SetDirty(backStepClip);
        }

        private static List<AnimationEvent> GetEventsWithoutRollingSoundFX(AnimationClip animationClip)
        {
            List<AnimationEvent> animationEvents = new List<AnimationEvent>();
            foreach (AnimationEvent animationEvent in AnimationUtility.GetAnimationEvents(animationClip))
            {
                if (animationEvent.functionName != nameof(CharacterSoundFXManager.PlayRollingSoundFX))
                {
                    animationEvents.Add(animationEvent);
                }
            }

            return animationEvents;
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    throw new InvalidOperationException("The Player prefab does not contain an Animator.");
                }

                RemoveComponentsExcept<PlayerAnimatorManager>(playerRoot, animator.gameObject);
                RemoveComponentsExcept<PlayerSoundFXManager>(playerRoot, animator.gameObject);
                PlayerAnimatorManager animatorManager =
                    GetOrAddComponent<PlayerAnimatorManager>(animator.gameObject);
                PlayerSoundFXManager soundFXManager =
                    GetOrAddComponent<PlayerSoundFXManager>(animator.gameObject);
                AudioSource audioSource = GetOrAddComponent<AudioSource>(animator.gameObject);
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 1f;

                PlayerManager playerManager = playerRoot.GetComponent<PlayerManager>();
                if (playerManager != null)
                {
                    SerializedObject serializedPlayerManager = new SerializedObject(playerManager);
                    SerializedProperty animatorManagerProperty =
                        serializedPlayerManager.FindProperty("m_playerAnimatorManager");
                    animatorManagerProperty.objectReferenceValue = animatorManager;
                    serializedPlayerManager.ApplyModifiedPropertiesWithoutUndo();
                }

                SerializedObject serializedSoundFXManager = new SerializedObject(soundFXManager);
                SerializedProperty audioSourceProperty =
                    serializedSoundFXManager.FindProperty("m_audioSource");
                audioSourceProperty.objectReferenceValue = audioSource;
                serializedSoundFXManager.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(animator);
                EditorUtility.SetDirty(soundFXManager);
                EditorUtility.SetDirty(audioSource);
                PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigureWorldSoundFXManager(AudioClip rollingSoundFX)
        {
            Scene scene = OpenSceneForEditing(k_MainMenuScenePath, out bool sceneWasLoaded);

            try
            {
                WorldSoundFXManager soundFXManager = FindComponentInScene<WorldSoundFXManager>(scene);
                if (soundFXManager == null)
                {
                    GameObject managerObject = new GameObject("World Sound FX Manager");
                    SceneManager.MoveGameObjectToScene(managerObject, scene);
                    soundFXManager = managerObject.AddComponent<WorldSoundFXManager>();
                }

                SerializedObject serializedManager = new SerializedObject(soundFXManager);
                SerializedProperty rollingSoundProperty =
                    serializedManager.FindProperty("m_rollingSoundFX");
                rollingSoundProperty.objectReferenceValue = rollingSoundFX;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(soundFXManager);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                CloseSceneIfNeeded(scene, sceneWasLoaded);
            }
        }

        private static void ValidateActionLayer(
            AnimatorController controller,
            AnimationClip rollClip,
            AnimationClip backStepClip)
        {
            AnimatorControllerLayer actionLayer = FindActionLayer(controller);
            if (!Mathf.Approximately(actionLayer.defaultWeight, 1f) ||
                actionLayer.avatarMask != null ||
                actionLayer.blendingMode != AnimatorLayerBlendingMode.Override)
            {
                throw new InvalidOperationException(
                    "Action Override must be an unmasked Override layer with weight 1.");
            }

            AnimatorStateMachine stateMachine = actionLayer.stateMachine;
            AnimatorState emptyState = FindRequiredState(stateMachine, k_EmptyStateName);
            AnimatorState rollState = FindRequiredState(stateMachine, k_RollStateName);
            AnimatorState backStepState = FindRequiredState(stateMachine, k_BackStepStateName);
            if (stateMachine.defaultState != emptyState ||
                rollState.motion != rollClip ||
                backStepState.motion != backStepClip)
            {
                throw new InvalidOperationException("Action Override states are not configured correctly.");
            }

            bool hasResetBehaviour = false;
            foreach (StateMachineBehaviour behaviour in emptyState.behaviours)
            {
                hasResetBehaviour |= behaviour is ResetActionFlags;
            }

            if (!hasResetBehaviour ||
                !HasExitTransition(rollState, emptyState) ||
                !HasExitTransition(backStepState, emptyState))
            {
                throw new InvalidOperationException("Action lifecycle reset or exit transitions are missing.");
            }
        }

        private static void ValidateAnimationEvents(
            AnimationClip rollClip,
            AnimationClip backStepClip)
        {
            if (CountRollingSoundFXEvents(rollClip) != 1 ||
                CountRollingSoundFXEvents(backStepClip) != 0)
            {
                throw new InvalidOperationException("Dodge animation events are not configured correctly.");
            }
        }

        private static int CountRollingSoundFXEvents(AnimationClip animationClip)
        {
            int eventCount = 0;
            foreach (AnimationEvent animationEvent in AnimationUtility.GetAnimationEvents(animationClip))
            {
                if (animationEvent.functionName == nameof(CharacterSoundFXManager.PlayRollingSoundFX))
                {
                    eventCount++;
                }
            }

            return eventCount;
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
                if (animator == null ||
                    animator.GetComponent<PlayerAnimatorManager>() == null ||
                    animator.GetComponent<PlayerSoundFXManager>() == null)
                {
                    throw new InvalidOperationException(
                        "The Player Animator needs PlayerAnimatorManager and PlayerSoundFXManager.");
                }

                AudioSource audioSource = animator.GetComponent<AudioSource>();
                if (audioSource == null ||
                    audioSource.playOnAwake ||
                    !Mathf.Approximately(audioSource.spatialBlend, 1f))
                {
                    throw new InvalidOperationException(
                        "The Player rolling AudioSource must be spatial and must not play on awake.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateWorldSoundFXManager(AudioClip rollingSoundFX)
        {
            Scene scene = OpenSceneForEditing(k_MainMenuScenePath, out bool sceneWasLoaded);

            try
            {
                WorldSoundFXManager soundFXManager = FindComponentInScene<WorldSoundFXManager>(scene);
                if (soundFXManager == null || soundFXManager.RollingSoundFX != rollingSoundFX)
                {
                    throw new InvalidOperationException(
                        "The Main Menu scene needs WorldSoundFXManager with the rolling clip assigned.");
                }
            }
            finally
            {
                CloseSceneIfNeeded(scene, sceneWasLoaded);
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

            throw new InvalidOperationException($"Animator Controller is missing {k_ActionLayerName}.");
        }

        private static AnimatorState FindRequiredState(
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

            throw new InvalidOperationException($"Animator Controller is missing state {stateName}.");
        }

        private static bool HasExitTransition(
            AnimatorState actionState,
            AnimatorState emptyState)
        {
            foreach (AnimatorStateTransition transition in actionState.transitions)
            {
                if (transition.destinationState == emptyState && transition.hasExitTime)
                {
                    return true;
                }
            }

            return false;
        }

        private static Scene OpenSceneForEditing(string scenePath, out bool sceneWasLoaded)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            sceneWasLoaded = scene.IsValid() && scene.isLoaded;
            return sceneWasLoaded
                ? scene
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        private static void CloseSceneIfNeeded(Scene scene, bool sceneWasLoaded)
        {
            if (!sceneWasLoaded && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                T component = rootObject.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static T LoadRequiredAsset<T>(string assetPath) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"Could not load {typeof(T).Name} at {assetPath}.");
            }

            return asset;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void RemoveComponentsExcept<T>(
            GameObject rootObject,
            GameObject retainedObject) where T : Component
        {
            foreach (T component in rootObject.GetComponentsInChildren<T>(true))
            {
                if (component.gameObject != retainedObject)
                {
                    UnityEngine.Object.DestroyImmediate(component, true);
                }
            }
        }
    }
}
