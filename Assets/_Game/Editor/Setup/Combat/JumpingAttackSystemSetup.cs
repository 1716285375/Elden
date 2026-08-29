using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP81 jumping-attack system.</summary>
    public static class JumpingAttackSystemSetup
    {
        private const string k_ControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Base/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_OverrideControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/Overrides/Straight Sword.overrideController";
        private const string k_LocomotionClipFolder =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Locomotion/";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_GroundedParameter = "isGrounded";

        private static readonly string[] s_weaponPaths =
        {
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Unarmed.asset",
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset",
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Broadsword.asset"
        };

        private static readonly JumpAttackClipSet s_mainHandClips =
            new JumpAttackClipSet(
                "MainJumpLightAttack",
                "MainJumpHeavy",
                k_LocomotionClipFolder +
                    "straight_sword_main_jump_light_attack_01.anim",
                k_LocomotionClipFolder +
                    "straight_sword_main_jump_attack_01_charge.anim",
                k_LocomotionClipFolder +
                    "straight_sword_main_jump_attack_01_idle.anim",
                k_LocomotionClipFolder +
                    "straight_sword_main_jump_attack_01_end.anim",
                1760f);

        private static readonly JumpAttackClipSet s_twoHandClips =
            new JumpAttackClipSet(
                "TwoHandJumpLightAttack",
                "TwoHandJumpHeavy",
                k_LocomotionClipFolder +
                    "straight_sword_th_jump_light_attack_01.anim",
                k_LocomotionClipFolder +
                    "straight_sword_th_jump_attack_01_charge.anim",
                k_LocomotionClipFolder +
                    "straight_sword_th_jump_attack_01_idle.anim",
                k_LocomotionClipFolder +
                    "straight_sword_th_jump_attack_01_end.anim",
                2460f);

        [MenuItem("Tools/Elden/Configure Jumping Attack System")]
        public static void ConfigureJumpingAttackSystem()
        {
            ConfigureAnimatorController();
            ConfigureAnimationSettings();
            ConfigureWeaponModifiers();
            AssetDatabase.SaveAssets();
            ValidateJumpingAttackSystem();
            Debug.Log(
                "[JumpingAttackSystemSetup] Configured light and heavy jumping " +
                "attacks for main-hand and two-hand weapon stances.");
        }

        [MenuItem("Tools/Elden/Validate Jumping Attack System")]
        public static void ValidateJumpingAttackSystem()
        {
            ValidateAttackTypeIdentifiers();
            ValidateAnimatorController();
            ValidateAnimationEvents();
            ValidateWeaponModifiers();
            ValidateRuntimeArchitecture();
            Debug.Log(
                "[JumpingAttackSystemValidation] Input priority, Animator graphs, " +
                "landing resets, hit windows, and attack modifiers are valid.");
        }

        private static void ConfigureAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            AnimatorOverrideController overrideController =
                LoadRequiredAsset<AnimatorOverrideController>(
                    k_OverrideControllerPath);
            AnimatorControllerLayer actionLayer = controller.layers
                .FirstOrDefault(layer => layer.name == k_ActionLayerName) ??
                throw new InvalidOperationException(
                    $"Animator Controller needs {k_ActionLayerName}.");
            EnsureGroundedParameter(controller);
            AnimatorStateMachine stateMachine = actionLayer.stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);

            ConfigureClipSet(
                stateMachine,
                emptyState,
                overrideController,
                s_mainHandClips,
                120f);
            ConfigureClipSet(
                stateMachine,
                emptyState,
                overrideController,
                s_twoHandClips,
                520f);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureClipSet(
            AnimatorStateMachine stateMachine,
            AnimatorState emptyState,
            AnimatorOverrideController overrideController,
            JumpAttackClipSet clipSet,
            float rowY)
        {
            AnimatorState lightState = GetOrAddState(
                stateMachine,
                clipSet.LightStateName,
                new Vector3(clipSet.ColumnX, rowY, 0f));
            lightState.motion = ResolveOriginalClip(
                overrideController,
                LoadRequiredAsset<AnimationClip>(clipSet.LightClipPath));
            ClearTransitions(lightState);
            AddExitTransition(lightState, emptyState, 0.9f, 0.08f);

            AnimatorState heavyStartState = GetOrAddState(
                stateMachine,
                clipSet.HeavyPrefix + "Start",
                new Vector3(clipSet.ColumnX, rowY + 140f, 0f));
            AnimatorState heavyIdleState = GetOrAddState(
                stateMachine,
                clipSet.HeavyPrefix + "Idle",
                new Vector3(clipSet.ColumnX + 260f, rowY + 140f, 0f));
            AnimatorState heavyEndState = GetOrAddState(
                stateMachine,
                clipSet.HeavyPrefix + "End",
                new Vector3(clipSet.ColumnX + 520f, rowY + 140f, 0f));
            heavyStartState.motion = ResolveOriginalClip(
                overrideController,
                LoadRequiredAsset<AnimationClip>(clipSet.HeavyStartClipPath));
            heavyIdleState.motion = ResolveOriginalClip(
                overrideController,
                LoadRequiredAsset<AnimationClip>(clipSet.HeavyIdleClipPath));
            heavyEndState.motion = ResolveOriginalClip(
                overrideController,
                LoadRequiredAsset<AnimationClip>(clipSet.HeavyEndClipPath));

            ClearTransitions(heavyStartState);
            ClearTransitions(heavyIdleState);
            ClearTransitions(heavyEndState);
            AddGroundedTransition(
                heavyStartState,
                heavyIdleState,
                false,
                true,
                1f,
                0f);
            AddGroundedTransition(
                heavyStartState,
                heavyEndState,
                true,
                false,
                0f,
                0.02f);
            AddGroundedTransition(
                heavyIdleState,
                heavyEndState,
                true,
                false,
                0f,
                0.02f);
            AddExitTransition(heavyEndState, emptyState, 0.9f, 0.08f);
            if (heavyEndState.behaviours.All(behaviour =>
                    behaviour is not ResetJumpingState))
            {
                heavyEndState.AddStateMachineBehaviour<ResetJumpingState>();
            }

            EditorUtility.SetDirty(lightState);
            EditorUtility.SetDirty(heavyStartState);
            EditorUtility.SetDirty(heavyIdleState);
            EditorUtility.SetDirty(heavyEndState);
        }

        private static void ConfigureAnimationSettings()
        {
            AnimationClip mainLight = LoadRequiredAsset<AnimationClip>(
                s_mainHandClips.LightClipPath);
            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(mainLight);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(mainLight, settings);
            EditorUtility.SetDirty(mainLight);
        }

        private static void ConfigureWeaponModifiers()
        {
            foreach (string weaponPath in s_weaponPaths)
            {
                WeaponItem weapon = LoadRequiredAsset<WeaponItem>(weaponPath);
                SerializedObject serializedWeapon = new SerializedObject(weapon);
                SetFloat(
                    serializedWeapon,
                    "m_lightJumpingAttack01DamageModifier",
                    1f);
                SetFloat(
                    serializedWeapon,
                    "m_heavyJumpingAttack01DamageModifier",
                    1.8f);
                serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
            }
        }

        private static void ValidateAttackTypeIdentifiers()
        {
            if ((int)AttackType.LightJumpingAttack01 != 9 ||
                (int)AttackType.HeavyJumpingAttack01 != 10)
            {
                throw new InvalidOperationException(
                    "Jump AttackType values must append stable serialized identifiers.");
            }
        }

        private static void ValidateAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            AnimatorOverrideController overrideController =
                LoadRequiredAsset<AnimatorOverrideController>(
                    k_OverrideControllerPath);
            AnimatorStateMachine stateMachine = controller.layers
                .First(layer => layer.name == k_ActionLayerName)
                .stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);

            ValidateClipSet(
                stateMachine,
                emptyState,
                overrideController,
                s_mainHandClips);
            ValidateClipSet(
                stateMachine,
                emptyState,
                overrideController,
                s_twoHandClips);
            if (LoadRequiredAsset<AnimationClip>(s_mainHandClips.LightClipPath)
                .isLooping)
            {
                throw new InvalidOperationException(
                    "The main-hand light jumping attack must not loop.");
            }
        }

        private static void ValidateClipSet(
            AnimatorStateMachine stateMachine,
            AnimatorState emptyState,
            AnimatorOverrideController overrideController,
            JumpAttackClipSet clipSet)
        {
            AnimatorState lightState = GetRequiredState(
                stateMachine,
                clipSet.LightStateName);
            AnimatorState heavyStartState = GetRequiredState(
                stateMachine,
                clipSet.HeavyPrefix + "Start");
            AnimatorState heavyIdleState = GetRequiredState(
                stateMachine,
                clipSet.HeavyPrefix + "Idle");
            AnimatorState heavyEndState = GetRequiredState(
                stateMachine,
                clipSet.HeavyPrefix + "End");

            ValidateResolvedClip(
                overrideController,
                lightState,
                clipSet.LightClipPath);
            ValidateResolvedClip(
                overrideController,
                heavyStartState,
                clipSet.HeavyStartClipPath);
            ValidateResolvedClip(
                overrideController,
                heavyIdleState,
                clipSet.HeavyIdleClipPath);
            ValidateResolvedClip(
                overrideController,
                heavyEndState,
                clipSet.HeavyEndClipPath);
            ValidateGroundedTransition(
                heavyStartState,
                heavyIdleState,
                AnimatorConditionMode.IfNot,
                true);
            ValidateGroundedTransition(
                heavyStartState,
                heavyEndState,
                AnimatorConditionMode.If,
                false);
            ValidateGroundedTransition(
                heavyIdleState,
                heavyEndState,
                AnimatorConditionMode.If,
                false);
            if (!HasExitTransition(lightState, emptyState) ||
                !HasExitTransition(heavyEndState, emptyState) ||
                heavyEndState.behaviours.All(behaviour =>
                    behaviour is not ResetJumpingState))
            {
                throw new InvalidOperationException(
                    $"Jump attack graph {clipSet.HeavyPrefix} is incomplete.");
            }
        }

        private static void ValidateAnimationEvents()
        {
            ValidateEvents(
                s_mainHandClips.LightClipPath,
                "DrainStaminaBasedOnAttack",
                "OpenDamageCollider",
                "CloseDamageCollider");
            ValidateEvents(
                s_twoHandClips.LightClipPath,
                "DrainStaminaBasedOnAttack",
                "OpenDamageCollider",
                "CloseDamageCollider");
            ValidateEvents(
                s_mainHandClips.HeavyStartClipPath,
                "DrainStaminaBasedOnAttack",
                "OpenDamageCollider");
            ValidateEvents(
                s_mainHandClips.HeavyEndClipPath,
                "CloseDamageCollider");
            ValidateEvents(
                s_twoHandClips.HeavyStartClipPath,
                "DrainStaminaBasedOnAttack",
                "OpenDamageCollider");
            ValidateEvents(
                s_twoHandClips.HeavyEndClipPath,
                "CloseDamageCollider");
        }

        private static void ValidateWeaponModifiers()
        {
            foreach (string weaponPath in s_weaponPaths)
            {
                WeaponItem weapon = LoadRequiredAsset<WeaponItem>(weaponPath);
                if (!Mathf.Approximately(
                        weapon.GetAttackDamageModifier(
                            AttackType.LightJumpingAttack01),
                        1f) ||
                    !Mathf.Approximately(
                        weapon.GetAttackDamageModifier(
                            AttackType.HeavyJumpingAttack01),
                        1.8f) ||
                    !Mathf.Approximately(
                        weapon.GetStaminaCostMultiplier(
                            AttackType.LightJumpingAttack01),
                        weapon.GetStaminaCostMultiplier(
                            AttackType.LightAttack01)) ||
                    !Mathf.Approximately(
                        weapon.GetStaminaCostMultiplier(
                            AttackType.HeavyJumpingAttack01),
                        weapon.GetStaminaCostMultiplier(
                            AttackType.HeavyAttack01)))
                {
                    throw new InvalidOperationException(
                        $"Weapon {weapon.name} has invalid jumping-attack modifiers.");
                }
            }
        }

        private static void ValidateRuntimeArchitecture()
        {
            BindingFlags nonPublicStatic =
                BindingFlags.NonPublic | BindingFlags.Static;
            BindingFlags publicInstance = BindingFlags.Public | BindingFlags.Instance;
            if (typeof(WeaponItemBasedAction).GetMethod(
                    "ResolveJumpAttackContext",
                    nonPublicStatic) == null ||
                typeof(CharacterCombatManager).GetMethod(
                    "ReplicateAttack",
                    publicInstance) == null ||
                typeof(WeaponManager).GetMethod(
                    "SetAttackType",
                    publicInstance) == null ||
                typeof(SpellManager).GetMethod(
                    "FixedUpdate",
                    BindingFlags.NonPublic | BindingFlags.Instance) == null)
            {
                throw new InvalidOperationException(
                    "Jumping attacks require priority, replication, damage, and " +
                    "fixed-speed projectile contracts.");
            }
        }

        private static void EnsureGroundedParameter(AnimatorController controller)
        {
            AnimatorControllerParameter parameter = controller.parameters
                .FirstOrDefault(candidate => candidate.name == k_GroundedParameter);
            if (parameter == null)
            {
                controller.AddParameter(
                    k_GroundedParameter,
                    AnimatorControllerParameterType.Bool);
            }
            else if (parameter.type != AnimatorControllerParameterType.Bool)
            {
                throw new InvalidOperationException(
                    $"Animator parameter {k_GroundedParameter} must be a bool.");
            }
        }

        private static AnimatorState GetOrAddState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Vector3 position)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName) ??
                stateMachine.AddState(stateName, position);
        }

        private static AnimatorState GetRequiredState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName) ??
                throw new InvalidOperationException(
                    $"Animator state {stateName} is missing.");
        }

        private static void ClearTransitions(AnimatorState state)
        {
            foreach (AnimatorStateTransition transition in state.transitions.ToArray())
            {
                state.RemoveTransition(transition);
            }
        }

        private static void AddGroundedTransition(
            AnimatorState source,
            AnimatorState destination,
            bool isGrounded,
            bool hasExitTime,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = hasExitTime;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.canTransitionToSelf = false;
            transition.AddCondition(
                isGrounded ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                k_GroundedParameter);
            EditorUtility.SetDirty(transition);
        }

        private static void AddExitTransition(
            AnimatorState source,
            AnimatorState destination,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.canTransitionToSelf = false;
            EditorUtility.SetDirty(transition);
        }

        private static void ValidateGroundedTransition(
            AnimatorState source,
            AnimatorState destination,
            AnimatorConditionMode mode,
            bool hasExitTime)
        {
            AnimatorStateTransition transition = source.transitions
                .SingleOrDefault(candidate => candidate.destinationState == destination);
            if (transition == null ||
                transition.hasExitTime != hasExitTime ||
                transition.conditions.Length != 1 ||
                transition.conditions[0].parameter != k_GroundedParameter ||
                transition.conditions[0].mode != mode)
            {
                throw new InvalidOperationException(
                    $"Transition {source.name} -> {destination.name} is invalid.");
            }
        }

        private static bool HasExitTransition(
            AnimatorState source,
            AnimatorState destination)
        {
            return source.transitions.Any(transition =>
                transition.destinationState == destination &&
                transition.hasExitTime &&
                transition.conditions.Length == 0);
        }

        private static AnimationClip ResolveOriginalClip(
            AnimatorOverrideController overrideController,
            AnimationClip overrideClip)
        {
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new();
            overrideController.GetOverrides(overrides);
            AnimationClip originalClip = overrides
                .FirstOrDefault(pair => pair.Value == overrideClip)
                .Key;
            if (originalClip != null)
            {
                return originalClip;
            }

            SerializedProperty clips = new SerializedObject(overrideController)
                .FindProperty("m_Clips");
            for (int index = 0; clips != null && index < clips.arraySize; index++)
            {
                SerializedProperty pair = clips.GetArrayElementAtIndex(index);
                if (pair.FindPropertyRelative("m_OverrideClip").objectReferenceValue !=
                    overrideClip)
                {
                    continue;
                }

                return pair.FindPropertyRelative("m_OriginalClip")
                    .objectReferenceValue as AnimationClip ??
                    throw new InvalidOperationException(
                        $"Override {overrideClip.name} has no original clip.");
            }

            throw new InvalidOperationException(
                $"Straight Sword override is missing {overrideClip.name}.");
        }

        private static void ValidateResolvedClip(
            AnimatorOverrideController overrideController,
            AnimatorState state,
            string expectedClipPath)
        {
            AnimationClip expectedClip = LoadRequiredAsset<AnimationClip>(
                expectedClipPath);
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new();
            overrideController.GetOverrides(overrides);
            AnimationClip resolvedClip = overrides
                .FirstOrDefault(pair => pair.Key == state.motion)
                .Value;
            if (resolvedClip != expectedClip)
            {
                throw new InvalidOperationException(
                    $"State {state.name} does not resolve to {expectedClip.name}.");
            }
        }

        private static void ValidateEvents(
            string clipPath,
            params string[] requiredEventNames)
        {
            string[] eventNames = AnimationUtility.GetAnimationEvents(
                    LoadRequiredAsset<AnimationClip>(clipPath))
                .Select(animationEvent => animationEvent.functionName)
                .ToArray();
            if (requiredEventNames.Any(requiredEvent =>
                    !eventNames.Contains(requiredEvent)))
            {
                throw new InvalidOperationException(
                    $"Animation {clipPath} is missing a jumping-attack event.");
            }
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} is missing " +
                    $"serialized property {propertyName}.");
            property.floatValue = value;
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private readonly struct JumpAttackClipSet
        {
            public JumpAttackClipSet(
                string lightStateName,
                string heavyPrefix,
                string lightClipPath,
                string heavyStartClipPath,
                string heavyIdleClipPath,
                string heavyEndClipPath,
                float columnX)
            {
                LightStateName = lightStateName;
                HeavyPrefix = heavyPrefix;
                LightClipPath = lightClipPath;
                HeavyStartClipPath = heavyStartClipPath;
                HeavyIdleClipPath = heavyIdleClipPath;
                HeavyEndClipPath = heavyEndClipPath;
                ColumnX = columnX;
            }

            public string LightStateName { get; }
            public string HeavyPrefix { get; }
            public string LightClipPath { get; }
            public string HeavyStartClipPath { get; }
            public string HeavyIdleClipPath { get; }
            public string HeavyEndClipPath { get; }
            public float ColumnX { get; }
        }
    }
}
