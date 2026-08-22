using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    public static class DirectionalDamageAnimationSetup
    {
        private const string k_PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_AnimatorControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const float k_ReactionExitTime = 0.9f;
        private const float k_ReactionTransitionDuration = 0.1f;

        private static readonly ReactionDefinition[] s_reactionDefinitions =
        {
            new ReactionDefinition(
                DamageDirection.Front,
                "m_hitForwardAnimations",
                new[]
                {
                    "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
                        "core_oh_hit_reaction_medium_f_01.anim",
                    "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
                        "core_oh_hit_reaction_medium_f_02.anim",
                    "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
                        "core_oh_hit_reaction_medium_f_03.anim"
                }),
            new ReactionDefinition(
                DamageDirection.Back,
                "m_hitBackwardAnimations",
                new[]
                {
                    "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
                        "core_oh_hit_reaction_medium_B_01.anim",
                    "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
                        "core_oh_hit_reaction_medium_B_02.anim",
                    "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
                        "core_oh_hit_reaction_medium_B_03.anim"
                }),
            new ReactionDefinition(
                DamageDirection.Left,
                "m_hitLeftAnimations",
                new[]
                {
                    "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
                        "core_oh_hit_reaction_medium_L_01.anim",
                    "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
                        "core_oh_hit_reaction_medium_L_02.anim",
                    "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
                        "core_oh_hit_reaction_medium_L_03.anim"
                }),
            new ReactionDefinition(
                DamageDirection.Right,
                "m_hitRightAnimations",
                new[]
                {
                    "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
                        "core_oh_hit_reaction_medium_R_01.anim",
                    "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
                        "core_oh_hit_reaction_medium_R_02.anim",
                    "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
                        "core_oh_hit_reaction_medium_R_03.anim"
                })
        };

        [MenuItem("Tools/Elden/Configure Directional Damage Reactions")]
        public static void ConfigureDirectionalDamageReactions()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AnimatorControllerPath);
            ConfigureAnimatorController(controller);
            ConfigurePlayerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateDirectionalDamageReactions();
            Debug.Log(
                "[DirectionalDamageAnimationSetup] Configured four directions with " +
                "three non-repeating medium reactions each.");
        }

        [MenuItem("Tools/Elden/Validate Directional Damage Reactions")]
        public static void ValidateDirectionalDamageReactions()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AnimatorControllerPath);
            ValidateAnimatorController(controller);
            ValidatePlayerPrefab();
            ValidateDirectionRules();
            ValidateRandomSelectionContract();
            Debug.Log(
                "[DirectionalDamageAnimationValidation] Angles, reaction assets, Animator " +
                "states, and non-repeating selection are valid.");
        }

        private static void ConfigureAnimatorController(AnimatorController controller)
        {
            AnimatorStateMachine stateMachine = GetRequiredLayer(controller).stateMachine;
            AnimatorState emptyState = GetRequiredState(stateMachine, k_EmptyStateName);
            int reactionIndex = 0;
            foreach (ReactionDefinition definition in s_reactionDefinitions)
            {
                foreach (string animationPath in definition.AnimationPaths)
                {
                    AnimationClip animation = LoadRequiredAsset<AnimationClip>(animationPath);
                    AnimatorState reactionState = GetOrCreateState(
                        stateMachine,
                        animation.name,
                        GetStatePosition(reactionIndex));
                    reactionState.motion = animation;
                    ConfigureExitTransition(reactionState, emptyState);
                    reactionIndex++;
                }
            }

            EditorUtility.SetDirty(controller);
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                CharacterAnimatorManager animatorManager =
                    GetRequiredComponentInChildren<CharacterAnimatorManager>(playerRoot);
                SerializedObject serializedManager = new SerializedObject(animatorManager);
                foreach (ReactionDefinition definition in s_reactionDefinitions)
                {
                    SerializedProperty animations = GetRequiredProperty(
                        serializedManager,
                        definition.SerializedPropertyName);
                    animations.arraySize = definition.AnimationPaths.Length;
                    for (int animationIndex = 0;
                        animationIndex < definition.AnimationPaths.Length;
                        animationIndex++)
                    {
                        animations.GetArrayElementAtIndex(animationIndex)
                            .objectReferenceValue = LoadRequiredAsset<AnimationClip>(
                                definition.AnimationPaths[animationIndex]);
                    }
                }

                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(animatorManager);
                PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigureExitTransition(
            AnimatorState reactionState,
            AnimatorState emptyState)
        {
            AnimatorStateTransition exitTransition = null;
            foreach (AnimatorStateTransition transition in reactionState.transitions)
            {
                if (transition.destinationState == emptyState)
                {
                    exitTransition = transition;
                    break;
                }
            }

            exitTransition ??= reactionState.AddTransition(emptyState);
            exitTransition.hasExitTime = true;
            exitTransition.exitTime = k_ReactionExitTime;
            exitTransition.hasFixedDuration = true;
            exitTransition.duration = k_ReactionTransitionDuration;
            exitTransition.conditions = Array.Empty<AnimatorCondition>();
        }

        private static void ValidateAnimatorController(AnimatorController controller)
        {
            AnimatorStateMachine stateMachine = GetRequiredLayer(controller).stateMachine;
            AnimatorState emptyState = GetRequiredState(stateMachine, k_EmptyStateName);
            foreach (ReactionDefinition definition in s_reactionDefinitions)
            {
                foreach (string animationPath in definition.AnimationPaths)
                {
                    AnimationClip animation = LoadRequiredAsset<AnimationClip>(animationPath);
                    AnimatorState reactionState = GetRequiredState(
                        stateMachine,
                        animation.name);
                    if (reactionState.motion != animation ||
                        !HasConfiguredExitTransition(reactionState, emptyState))
                    {
                        throw new InvalidOperationException(
                            $"Damage reaction state {animation.name} is not configured.");
                    }
                }
            }
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                CharacterAnimatorManager animatorManager =
                    GetRequiredComponentInChildren<CharacterAnimatorManager>(playerRoot);
                SerializedObject serializedManager = new SerializedObject(animatorManager);
                foreach (ReactionDefinition definition in s_reactionDefinitions)
                {
                    SerializedProperty animations = GetRequiredProperty(
                        serializedManager,
                        definition.SerializedPropertyName);
                    if (animations.arraySize != definition.AnimationPaths.Length)
                    {
                        throw new InvalidOperationException(
                            $"{definition.Direction} needs exactly three reactions.");
                    }

                    for (int animationIndex = 0;
                        animationIndex < definition.AnimationPaths.Length;
                        animationIndex++)
                    {
                        AnimationClip expected = LoadRequiredAsset<AnimationClip>(
                            definition.AnimationPaths[animationIndex]);
                        if (animations.GetArrayElementAtIndex(animationIndex)
                            .objectReferenceValue != expected)
                        {
                            throw new InvalidOperationException(
                                $"{definition.Direction} reaction {animationIndex} is invalid.");
                        }
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateDirectionRules()
        {
            AssertDirection(0f, DamageDirection.Back);
            AssertDirection(45f, DamageDirection.Back);
            AssertDirection(90f, DamageDirection.Left);
            AssertDirection(145f, DamageDirection.Front);
            AssertDirection(180f, DamageDirection.Front);
            AssertDirection(-45f, DamageDirection.Back);
            AssertDirection(-90f, DamageDirection.Right);
            AssertDirection(-145f, DamageDirection.Front);
            AssertHitGeometry();
        }

        private static void AssertHitGeometry()
        {
            GameObject attacker = new GameObject("Directional Damage Test Attacker");
            GameObject target = new GameObject("Directional Damage Test Target");

            try
            {
                target.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                attacker.transform.position = Vector3.forward;
                AssertDirection(
                    TakeDamageEffect.CalculateHitAngle(
                        attacker.transform,
                        target.transform),
                    DamageDirection.Front);
                attacker.transform.position = Vector3.back;
                AssertDirection(
                    TakeDamageEffect.CalculateHitAngle(
                        attacker.transform,
                        target.transform),
                    DamageDirection.Back);
                attacker.transform.position = Vector3.left;
                AssertDirection(
                    TakeDamageEffect.CalculateHitAngle(
                        attacker.transform,
                        target.transform),
                    DamageDirection.Left);
                attacker.transform.position = Vector3.right;
                AssertDirection(
                    TakeDamageEffect.CalculateHitAngle(
                        attacker.transform,
                        target.transform),
                    DamageDirection.Right);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(attacker);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void ValidateRandomSelectionContract()
        {
            GameObject managerObject = new GameObject("Directional Damage Selection Test");
            AnimationClip firstAnimation = new AnimationClip();
            AnimationClip secondAnimation = new AnimationClip();

            try
            {
                CharacterAnimatorManager animatorManager =
                    managerObject.AddComponent<CharacterAnimatorManager>();
                FieldInfo lastAnimationField = typeof(CharacterAnimatorManager).GetField(
                    "m_lastDamageAnimationPlayed",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo selectionMethod = typeof(CharacterAnimatorManager).GetMethod(
                    "GetRandomDamageAnimation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (lastAnimationField == null || selectionMethod == null)
                {
                    throw new InvalidOperationException(
                        "Could not inspect the directional reaction selection contract.");
                }

                List<AnimationClip> sourceAnimations = new List<AnimationClip>
                {
                    firstAnimation,
                    secondAnimation
                };
                lastAnimationField.SetValue(animatorManager, firstAnimation);
                AnimationClip selectedAnimation = selectionMethod.Invoke(
                    animatorManager,
                    new object[] { sourceAnimations }) as AnimationClip;
                if (selectedAnimation != secondAnimation ||
                    sourceAnimations.Count != 2 ||
                    sourceAnimations[0] != firstAnimation)
                {
                    throw new InvalidOperationException(
                        "Reaction selection must avoid an immediate repeat without " +
                        "mutating its serialized source list.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(firstAnimation);
                UnityEngine.Object.DestroyImmediate(secondAnimation);
            }
        }

        private static void AssertDirection(
            float hitAngle,
            DamageDirection expectedDirection)
        {
            DamageDirection actualDirection = TakeDamageEffect.GetDamageDirection(hitAngle);
            if (actualDirection != expectedDirection)
            {
                throw new InvalidOperationException(
                    $"Hit angle {hitAngle} resolved to {actualDirection} instead of " +
                    $"{expectedDirection}.");
            }
        }

        private static bool HasConfiguredExitTransition(
            AnimatorState reactionState,
            AnimatorState emptyState)
        {
            foreach (AnimatorStateTransition transition in reactionState.transitions)
            {
                if (transition.destinationState == emptyState &&
                    transition.hasExitTime &&
                    Mathf.Approximately(transition.exitTime, k_ReactionExitTime) &&
                    transition.hasFixedDuration &&
                    Mathf.Approximately(
                        transition.duration,
                        k_ReactionTransitionDuration) &&
                    transition.conditions.Length == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static AnimatorControllerLayer GetRequiredLayer(
            AnimatorController controller)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name == k_ActionLayerName)
                {
                    return layer;
                }
            }

            throw new InvalidOperationException(
                $"Animator Controller needs the {k_ActionLayerName} layer.");
        }

        private static AnimatorState GetOrCreateState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Vector3 position)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state.name == stateName)
                {
                    return childState.state;
                }
            }

            return stateMachine.AddState(stateName, position);
        }

        private static AnimatorState GetRequiredState(
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

            throw new InvalidOperationException(
                $"Animator state {stateName} is missing from {k_ActionLayerName}.");
        }

        private static Vector3 GetStatePosition(int reactionIndex)
        {
            const float k_ColumnSpacing = 240f;
            const float k_RowSpacing = 90f;
            return new Vector3(
                900f + (reactionIndex / 3) * k_ColumnSpacing,
                100f + (reactionIndex % 3) * k_RowSpacing,
                0f);
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static T GetRequiredComponentInChildren<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponentInChildren<T>(true);
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{gameObject.name} needs a {typeof(T).Name}.");
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"Could not find {serializedObject.targetObject.GetType().Name}." +
                    propertyName);
        }

        private readonly struct ReactionDefinition
        {
            public ReactionDefinition(
                DamageDirection direction,
                string serializedPropertyName,
                string[] animationPaths)
            {
                Direction = direction;
                SerializedPropertyName = serializedPropertyName;
                AnimationPaths = animationPaths;
            }

            public DamageDirection Direction { get; }
            public string SerializedPropertyName { get; }
            public string[] AnimationPaths { get; }
        }
    }
}
