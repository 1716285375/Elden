using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP49 player moving-attack system.</summary>
    public static class MovingAttackSystemSetup
    {
        private const string k_ControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_RunAttackClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Locomotion/" +
            "straight_sword_main_run_attack_01.anim";
        private const string k_RollAttackClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Locomotion/" +
            "straight_sword_main_roll_attack_01_release.anim";
        private const string k_BackStepAttackClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/Sword/" +
            "straight_sword_main_back_step_attack_01_release.anim";
        private const string k_RollClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Locomotion/" +
            "core_main_roll_med_to_idle_F_01.anim";
        private const string k_BackStepClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Actions/" +
            "core_main_back_step_medium_02.anim";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_RunAttackStateName = "MainCore_RunAttack01";
        private const string k_RollAttackStateName = "MainCore_RollAttack01";
        private const string k_BackStepAttackStateName = "MainCore_BackStepAttack01";
        private const string k_EnableRollAttackEvent =
            "EnableCanPerformRollAttack";
        private const string k_EnableBackStepAttackEvent =
            "EnableCanPerformBackStepAttack";
        private const string k_DisableCommittedAttackEvent =
            "DisableCanPerformCommittedAttack";

        private static readonly string[] s_weaponPaths =
        {
            "Assets/Data/Items/Weapons/Melee Weapons/Unarmed.asset",
            "Assets/Data/Items/Weapons/Melee Weapons/Straight Sword.asset",
            "Assets/Data/Items/Weapons/Melee Weapons/Broadsword.asset"
        };

        private static readonly MovingAttackDefinition[] s_attackDefinitions =
        {
            new MovingAttackDefinition(
                AttackType.RunningAttack01,
                k_RunAttackStateName,
                k_RunAttackClipPath,
                new Vector3(1490f, 310f, 0f)),
            new MovingAttackDefinition(
                AttackType.RollAttack01,
                k_RollAttackStateName,
                k_RollAttackClipPath,
                new Vector3(1490f, 420f, 0f)),
            new MovingAttackDefinition(
                AttackType.BackStepAttack01,
                k_BackStepAttackStateName,
                k_BackStepAttackClipPath,
                new Vector3(1490f, 530f, 0f))
        };

        [MenuItem("Tools/Elden/Configure Moving Attack System")]
        public static void ConfigureMovingAttackSystem()
        {
            ConfigureAnimatorController();
            ConfigureCommittedActionWindows();
            ConfigureWeaponModifiers();
            AssetDatabase.SaveAssets();
            ValidateMovingAttackSystem();
            Debug.Log(
                "[MovingAttackSystemSetup] Configured running, roll, and backstep " +
                "attacks with Root Motion, RPC playback, hit windows, and modifiers.");
        }

        [MenuItem("Tools/Elden/Validate Moving Attack System")]
        public static void ValidateMovingAttackSystem()
        {
            ValidateAttackTypeIdentifiers();
            ValidateAnimatorController();
            ValidateMovingAttackEvents();
            ValidateCommittedActionWindows();
            ValidateAnimationEventReceivers();
            ValidateWeaponModifiers();
            ValidateRuntimeArchitecture();
            Debug.Log(
                "[MovingAttackSystemValidation] Attack priority, committed windows, " +
                "Animator states, Root Motion events, and independent modifiers are valid.");
        }

        private static void ConfigureAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            AnimatorControllerLayer actionLayer = controller.layers
                .FirstOrDefault(layer => layer.name == k_ActionLayerName) ??
                throw new InvalidOperationException(
                    $"Animator Controller needs {k_ActionLayerName}.");
            AnimatorStateMachine stateMachine = actionLayer.stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);

            foreach (MovingAttackDefinition definition in s_attackDefinitions)
            {
                AnimatorState state = stateMachine.states
                    .Select(childState => childState.state)
                    .FirstOrDefault(candidate =>
                        candidate.name == definition.StateName) ??
                    stateMachine.AddState(
                        definition.StateName,
                        definition.Position);
                state.motion = LoadRequiredAsset<AnimationClip>(
                    definition.ClipPath);
                ConfigureExitTransition(state, emptyState);
                EditorUtility.SetDirty(state);
            }

            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureExitTransition(
            AnimatorState attackState,
            AnimatorState emptyState)
        {
            AnimatorStateTransition transition = attackState.transitions
                .FirstOrDefault(candidate => candidate.destinationState == emptyState) ??
                attackState.AddTransition(emptyState);
            transition.hasExitTime = true;
            transition.exitTime = 0.9f;
            transition.hasFixedDuration = true;
            transition.duration = 0.2f;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.canTransitionToSelf = false;
            transition.conditions = Array.Empty<AnimatorCondition>();
            EditorUtility.SetDirty(transition);
        }

        private static void ConfigureCommittedActionWindows()
        {
            ConfigureCommittedActionWindow(
                LoadRequiredAsset<AnimationClip>(k_RollClipPath),
                k_EnableRollAttackEvent);
            ConfigureCommittedActionWindow(
                LoadRequiredAsset<AnimationClip>(k_BackStepClipPath),
                k_EnableBackStepAttackEvent);
        }

        private static void ConfigureCommittedActionWindow(
            AnimationClip clip,
            string enableEventName)
        {
            AnimationEvent[] preservedEvents = AnimationUtility.GetAnimationEvents(clip)
                .Where(animationEvent =>
                    animationEvent.functionName != enableEventName &&
                    animationEvent.functionName != k_DisableCommittedAttackEvent)
                .ToArray();
            AnimationEvent[] configuredEvents = preservedEvents
                .Concat(new[]
                {
                    new AnimationEvent
                    {
                        functionName = enableEventName,
                        time = clip.length * 0.55f,
                        messageOptions = SendMessageOptions.RequireReceiver
                    },
                    new AnimationEvent
                    {
                        functionName = k_DisableCommittedAttackEvent,
                        time = clip.length * 0.92f,
                        messageOptions = SendMessageOptions.RequireReceiver
                    }
                })
                .OrderBy(animationEvent => animationEvent.time)
                .ToArray();
            AnimationUtility.SetAnimationEvents(clip, configuredEvents);
            EditorUtility.SetDirty(clip);
        }

        private static void ConfigureWeaponModifiers()
        {
            foreach (string weaponPath in s_weaponPaths)
            {
                WeaponItem weapon = LoadRequiredAsset<WeaponItem>(weaponPath);
                SerializedObject serializedWeapon = new SerializedObject(weapon);
                SetFloat(
                    serializedWeapon,
                    "m_runningAttack01DamageModifier",
                    1.2f);
                SetFloat(
                    serializedWeapon,
                    "m_rollAttack01DamageModifier",
                    1.1f);
                SetFloat(
                    serializedWeapon,
                    "m_backStepAttack01DamageModifier",
                    1.15f);
                SetFloat(
                    serializedWeapon,
                    "m_runningAttack01StaminaCostMultiplier",
                    1.25f);
                SetFloat(
                    serializedWeapon,
                    "m_rollAttack01StaminaCostMultiplier",
                    1.15f);
                SetFloat(
                    serializedWeapon,
                    "m_backStepAttack01StaminaCostMultiplier",
                    1.2f);
                serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
            }
        }

        private static void ValidateAttackTypeIdentifiers()
        {
            if ((int)AttackType.RunningAttack01 != 6 ||
                (int)AttackType.RollAttack01 != 7 ||
                (int)AttackType.BackStepAttack01 != 8)
            {
                throw new InvalidOperationException(
                    "Moving AttackType values must append stable serialized identifiers.");
            }
        }

        private static void ValidateAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            AnimatorStateMachine stateMachine = controller.layers
                .First(layer => layer.name == k_ActionLayerName)
                .stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            foreach (MovingAttackDefinition definition in s_attackDefinitions)
            {
                AnimatorState state = GetRequiredState(
                    stateMachine,
                    definition.StateName);
                if (state.motion !=
                        LoadRequiredAsset<AnimationClip>(definition.ClipPath) ||
                    !state.transitions.Any(transition =>
                        transition.destinationState == emptyState &&
                        transition.hasExitTime))
                {
                    throw new InvalidOperationException(
                        $"Moving attack state {definition.StateName} is invalid.");
                }
            }
        }

        private static void ValidateMovingAttackEvents()
        {
            string[] requiredEvents =
            {
                "OpenDamageCollider",
                "CloseDamageCollider",
                "DrainStaminaBasedOnAttack",
                "EnableCanRotate",
                "DisableCanRotate"
            };
            foreach (MovingAttackDefinition definition in s_attackDefinitions)
            {
                string[] eventNames = AnimationUtility.GetAnimationEvents(
                        LoadRequiredAsset<AnimationClip>(definition.ClipPath))
                    .Select(animationEvent => animationEvent.functionName)
                    .ToArray();
                if (requiredEvents.Any(requiredEvent =>
                        !eventNames.Contains(requiredEvent)))
                {
                    throw new InvalidOperationException(
                        $"{definition.ClipPath} is missing moving-attack events.");
                }
            }
        }

        private static void ValidateCommittedActionWindows()
        {
            ValidateCommittedActionWindow(k_RollClipPath, k_EnableRollAttackEvent);
            ValidateCommittedActionWindow(
                k_BackStepClipPath,
                k_EnableBackStepAttackEvent);
        }

        private static void ValidateCommittedActionWindow(
            string clipPath,
            string enableEventName)
        {
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(
                LoadRequiredAsset<AnimationClip>(clipPath));
            AnimationEvent enableEvent = events.SingleOrDefault(animationEvent =>
                animationEvent.functionName == enableEventName);
            AnimationEvent disableEvent = events.SingleOrDefault(animationEvent =>
                animationEvent.functionName == k_DisableCommittedAttackEvent);
            if (enableEvent == null ||
                disableEvent == null ||
                enableEvent.time >= disableEvent.time)
            {
                throw new InvalidOperationException(
                    $"{clipPath} has an invalid committed attack window.");
            }
        }

        private static void ValidateAnimationEventReceivers()
        {
            BindingFlags publicInstance = BindingFlags.Public | BindingFlags.Instance;
            string[] animatorMethods =
            {
                k_EnableRollAttackEvent,
                k_EnableBackStepAttackEvent,
                k_DisableCommittedAttackEvent
            };
            if (animatorMethods.Any(methodName =>
                    typeof(PlayerAnimatorManager).GetMethod(
                        methodName,
                        publicInstance) == null))
            {
                throw new InvalidOperationException(
                    "PlayerAnimatorManager is missing moving-attack event receivers.");
            }
        }

        private static void ValidateWeaponModifiers()
        {
            foreach (string weaponPath in s_weaponPaths)
            {
                WeaponItem weapon = LoadRequiredAsset<WeaponItem>(weaponPath);
                if (!Mathf.Approximately(
                        weapon.GetAttackDamageModifier(AttackType.RunningAttack01),
                        1.2f) ||
                    !Mathf.Approximately(
                        weapon.GetAttackDamageModifier(AttackType.RollAttack01),
                        1.1f) ||
                    !Mathf.Approximately(
                        weapon.GetAttackDamageModifier(AttackType.BackStepAttack01),
                        1.15f) ||
                    !Mathf.Approximately(
                        weapon.GetStaminaCostMultiplier(AttackType.RunningAttack01),
                        1.25f) ||
                    !Mathf.Approximately(
                        weapon.GetStaminaCostMultiplier(AttackType.RollAttack01),
                        1.15f) ||
                    !Mathf.Approximately(
                        weapon.GetStaminaCostMultiplier(AttackType.BackStepAttack01),
                        1.2f))
                {
                    throw new InvalidOperationException(
                        $"Weapon {weapon.name} has invalid moving-attack modifiers.");
                }
            }
        }

        private static void ValidateRuntimeArchitecture()
        {
            BindingFlags publicInstance = BindingFlags.Public | BindingFlags.Instance;
            if (typeof(PlayerCombatManager).GetMethod(
                    "TryPerformRunningAttack",
                    publicInstance) == null ||
                typeof(PlayerCombatManager).GetMethod(
                    "TryPerformCommittedAttack",
                    publicInstance) == null ||
                typeof(PlayerLocomotionManager).GetMethod(
                    "StopSprinting",
                    publicInstance) == null ||
                typeof(CharacterCombatManager).GetMethod(
                    "ResetActionState",
                    publicInstance) == null)
            {
                throw new InvalidOperationException(
                    "Moving attacks require sprint priority, committed windows, and reset hooks.");
            }
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

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
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

        private readonly struct MovingAttackDefinition
        {
            public MovingAttackDefinition(
                AttackType attackType,
                string stateName,
                string clipPath,
                Vector3 position)
            {
                AttackType = attackType;
                StateName = stateName;
                ClipPath = clipPath;
                Position = position;
            }

            public AttackType AttackType { get; }
            public string StateName { get; }
            public string ClipPath { get; }
            public Vector3 Position { get; }
        }
    }
}
