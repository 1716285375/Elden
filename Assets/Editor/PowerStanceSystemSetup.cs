using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP125 Power Stance attack set.</summary>
    public static class PowerStanceSystemSetup
    {
        private const string k_ControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Base/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_GroundedParameter = "isGrounded";
        private const string k_CombatSwordFolder =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Sword/";
        private const string k_LocomotionFolder =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Locomotion/";

        private static readonly string[] s_weaponPaths =
        {
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Unarmed.asset",
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset",
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Broadsword.asset"
        };

        private static readonly PowerStanceClip[] s_actionClips =
        {
            new PowerStanceClip(
                "Dual_Attack_01",
                k_CombatSwordFolder +
                    "straight_sword_dw_light_attack_01.anim",
                true,
                true,
                120f),
            new PowerStanceClip(
                "Dual_Attack_02",
                k_CombatSwordFolder +
                    "straight_sword_dw_light_attack_02.anim",
                true,
                false,
                260f),
            new PowerStanceClip(
                "Dual_Run_Attack",
                k_LocomotionFolder +
                    "straight_sword_dw_run_attack_01.anim",
                false,
                true,
                400f),
            new PowerStanceClip(
                "Dual_Roll_Attack",
                k_LocomotionFolder +
                    "straight_sword_dw_roll_attack_01_release.anim",
                false,
                false,
                540f),
            new PowerStanceClip(
                "Dual_BackStep_Attack",
                k_CombatSwordFolder +
                    "straight_sword_dw_back_step_attack_04_release.anim",
                false,
                true,
                680f)
        };

        private static readonly string s_jumpStartClipPath =
            k_LocomotionFolder +
            "straight_sword_dw_jump_attack_01_charge.anim";
        private static readonly string s_jumpIdleClipPath =
            k_LocomotionFolder +
            "straight_sword_dw_jump_attack_01_idle.anim";
        private static readonly string s_jumpEndClipPath =
            k_LocomotionFolder +
            "straight_sword_dw_jump_attack_01_end.anim";

        /// <summary>Builds the dual attack graph, hit windows, and balance data.</summary>
        [MenuItem("Tools/Elden/Configure Power Stance System")]
        public static void ConfigurePowerStanceSystem()
        {
            ConfigureAnimatorController();
            ConfigureAnimationClips();
            ConfigureWeaponModifiers();
            AssetDatabase.SaveAssets();
            ValidatePowerStanceSystem();
            Debug.Log(
                "[PowerStanceSystemSetup] Configured EP125 dual attacks, " +
                "independent hit windows, stamina events, and modifiers.");
        }

        /// <summary>Validates the complete EP125 runtime and authored asset contract.</summary>
        [MenuItem("Tools/Elden/Validate Power Stance System")]
        public static void ValidatePowerStanceSystem()
        {
            ValidateAttackTypes();
            ValidateAnimatorController();
            ValidateAnimationEvents();
            ValidateWeaponModifiers();
            ValidateRuntimeArchitecture();
            Debug.Log(
                "[PowerStanceSystemValidation] EP125 input routing, state " +
                "priority, animation events, damage, and stamina are valid.");
        }

        private static void ConfigureAnimatorController()
        {
            AnimatorController controller =
                LoadRequiredAsset<AnimatorController>(k_ControllerPath);
            AnimatorStateMachine stateMachine = controller.layers
                .Single(layer => layer.name == k_ActionLayerName)
                .stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);

            foreach (PowerStanceClip clipDefinition in s_actionClips)
            {
                AnimatorState state = GetOrAddState(
                    stateMachine,
                    clipDefinition.StateName,
                    new Vector3(2860f, clipDefinition.RowY, 0f));
                state.motion = LoadRequiredAsset<AnimationClip>(
                    clipDefinition.ClipPath);
                ClearTransitions(state);
                AddExitTransition(state, emptyState, 0.9f, 0.08f);
                EditorUtility.SetDirty(state);
            }

            ConfigureJumpGraph(stateMachine, emptyState);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureJumpGraph(
            AnimatorStateMachine stateMachine,
            AnimatorState emptyState)
        {
            AnimatorState startState = GetOrAddState(
                stateMachine,
                "Dual_Jump_Attack_Start",
                new Vector3(3260f, 180f, 0f));
            AnimatorState idleState = GetOrAddState(
                stateMachine,
                "Dual_Jump_Attack_Idle",
                new Vector3(3520f, 180f, 0f));
            AnimatorState endState = GetOrAddState(
                stateMachine,
                "Dual_Jump_Attack_End",
                new Vector3(3780f, 180f, 0f));
            startState.motion = LoadRequiredAsset<AnimationClip>(
                s_jumpStartClipPath);
            idleState.motion = LoadRequiredAsset<AnimationClip>(
                s_jumpIdleClipPath);
            endState.motion = LoadRequiredAsset<AnimationClip>(
                s_jumpEndClipPath);

            ClearTransitions(startState);
            ClearTransitions(idleState);
            ClearTransitions(endState);
            AddGroundedTransition(
                startState,
                idleState,
                false,
                true,
                1f,
                0f);
            AddGroundedTransition(
                startState,
                endState,
                true,
                false,
                0f,
                0.02f);
            AddGroundedTransition(
                idleState,
                endState,
                true,
                false,
                0f,
                0.02f);
            AddExitTransition(endState, emptyState, 0.9f, 0.08f);
            if (endState.behaviours.All(behaviour =>
                    behaviour is not ResetJumpingState))
            {
                endState.AddStateMachineBehaviour<ResetJumpingState>();
            }

            EditorUtility.SetDirty(startState);
            EditorUtility.SetDirty(idleState);
            EditorUtility.SetDirty(endState);
        }

        private static void ConfigureAnimationClips()
        {
            foreach (PowerStanceClip clipDefinition in s_actionClips)
            {
                AnimationClip clip = LoadRequiredAsset<AnimationClip>(
                    clipDefinition.ClipPath);
                SetLooping(clip, false);
                AnimationUtility.SetAnimationEvents(
                    clip,
                    BuildDualAttackEvents(
                        clip,
                        clipDefinition.OffHandFirst,
                        clipDefinition.SupportsCombo));
                EditorUtility.SetDirty(clip);
            }

            AnimationClip jumpStart = LoadRequiredAsset<AnimationClip>(
                s_jumpStartClipPath);
            AnimationClip jumpIdle = LoadRequiredAsset<AnimationClip>(
                s_jumpIdleClipPath);
            AnimationClip jumpEnd = LoadRequiredAsset<AnimationClip>(
                s_jumpEndClipPath);
            SetLooping(jumpStart, false);
            SetLooping(jumpIdle, true);
            SetLooping(jumpEnd, false);
            AnimationUtility.SetAnimationEvents(
                jumpEnd,
                BuildDualAttackEvents(jumpEnd, true, false));
            EditorUtility.SetDirty(jumpStart);
            EditorUtility.SetDirty(jumpIdle);
            EditorUtility.SetDirty(jumpEnd);
        }

        private static AnimationEvent[] BuildDualAttackEvents(
            AnimationClip clip,
            bool offHandFirst,
            bool supportsCombo)
        {
            List<AnimationEvent> events = new()
            {
                CreateEvent(clip, 0.08f, "DisableCanRotate")
            };
            AddHandStrikeEvents(events, clip, offHandFirst, 0.2f);
            AddHandStrikeEvents(events, clip, !offHandFirst, 0.43f);
            if (supportsCombo)
            {
                events.Add(CreateEvent(clip, 0.6f, "EnableCanDoCombo"));
                events.Add(CreateEvent(clip, 0.84f, "DisableCanDoCombo"));
            }

            events.Add(CreateEvent(clip, 0.72f, "EnableCanRoll"));
            events.Add(CreateEvent(clip, 0.76f, "EnableCanRotate"));
            return events.OrderBy(animationEvent => animationEvent.time)
                .ToArray();
        }

        private static void AddHandStrikeEvents(
            ICollection<AnimationEvent> events,
            AnimationClip clip,
            bool useOffHand,
            float normalizedStartTime)
        {
            string handName = useOffHand ? "OffHand" : "MainHand";
            events.Add(CreateEvent(
                clip,
                normalizedStartTime,
                $"Open{handName}DamageCollider"));
            events.Add(CreateEvent(
                clip,
                normalizedStartTime + 0.02f,
                "DrainStaminaBasedOnAttack"));
            events.Add(CreateEvent(
                clip,
                normalizedStartTime + 0.14f,
                $"Close{handName}DamageCollider"));
        }

        private static AnimationEvent CreateEvent(
            AnimationClip clip,
            float normalizedTime,
            string functionName)
        {
            return new AnimationEvent
            {
                time = Mathf.Clamp01(normalizedTime) * clip.length,
                functionName = functionName,
                messageOptions = SendMessageOptions.RequireReceiver
            };
        }

        private static void ConfigureWeaponModifiers()
        {
            string[] modifierProperties =
            {
                "m_dualAttack01DamageModifier",
                "m_dualAttack02DamageModifier",
                "m_dualJumpAttackDamageModifier",
                "m_dualRunAttackDamageModifier",
                "m_dualRollAttackDamageModifier",
                "m_dualBackstepAttackDamageModifier"
            };
            foreach (string weaponPath in s_weaponPaths)
            {
                WeaponItem weapon = LoadRequiredAsset<WeaponItem>(weaponPath);
                SerializedObject serializedWeapon = new SerializedObject(weapon);
                foreach (string propertyName in modifierProperties)
                {
                    SerializedProperty property = serializedWeapon
                        .FindProperty(propertyName) ??
                        throw new InvalidOperationException(
                            $"{weapon.name} is missing {propertyName}.");
                    property.floatValue = 0.77f;
                }

                serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
            }
        }

        private static void ValidateAttackTypes()
        {
            if ((int)AttackType.DualAttack01 != 11 ||
                (int)AttackType.DualAttack02 != 12 ||
                (int)AttackType.DualJumpAttack != 13 ||
                (int)AttackType.DualRunAttack != 14 ||
                (int)AttackType.DualRollAttack != 15 ||
                (int)AttackType.DualBackstepAttack != 16)
            {
                throw new InvalidOperationException(
                    "Dual AttackType values must append stable identifiers.");
            }
        }

        private static void ValidateAnimatorController()
        {
            AnimatorController controller =
                LoadRequiredAsset<AnimatorController>(k_ControllerPath);
            AnimatorStateMachine stateMachine = controller.layers
                .Single(layer => layer.name == k_ActionLayerName)
                .stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            foreach (PowerStanceClip clipDefinition in s_actionClips)
            {
                AnimatorState state = GetRequiredState(
                    stateMachine,
                    clipDefinition.StateName);
                if (state.motion != LoadRequiredAsset<AnimationClip>(
                        clipDefinition.ClipPath) ||
                    !HasExitTransition(state, emptyState))
                {
                    throw new InvalidOperationException(
                        $"Power Stance state {clipDefinition.StateName} is invalid.");
                }
            }

            ValidateJumpGraph(stateMachine, emptyState);
        }

        private static void ValidateJumpGraph(
            AnimatorStateMachine stateMachine,
            AnimatorState emptyState)
        {
            AnimatorState startState = GetRequiredState(
                stateMachine,
                "Dual_Jump_Attack_Start");
            AnimatorState idleState = GetRequiredState(
                stateMachine,
                "Dual_Jump_Attack_Idle");
            AnimatorState endState = GetRequiredState(
                stateMachine,
                "Dual_Jump_Attack_End");
            ValidateGroundedTransition(
                startState,
                idleState,
                AnimatorConditionMode.IfNot,
                true);
            ValidateGroundedTransition(
                startState,
                endState,
                AnimatorConditionMode.If,
                false);
            ValidateGroundedTransition(
                idleState,
                endState,
                AnimatorConditionMode.If,
                false);
            if (startState.motion != LoadRequiredAsset<AnimationClip>(
                    s_jumpStartClipPath) ||
                idleState.motion != LoadRequiredAsset<AnimationClip>(
                    s_jumpIdleClipPath) ||
                endState.motion != LoadRequiredAsset<AnimationClip>(
                    s_jumpEndClipPath) ||
                !HasExitTransition(endState, emptyState) ||
                endState.behaviours.All(behaviour =>
                    behaviour is not ResetJumpingState))
            {
                throw new InvalidOperationException(
                    "Power Stance jumping attack graph is invalid.");
            }
        }

        private static void ValidateAnimationEvents()
        {
            foreach (PowerStanceClip clipDefinition in s_actionClips)
            {
                ValidateDualEvents(
                    clipDefinition.ClipPath,
                    clipDefinition.SupportsCombo);
            }

            ValidateDualEvents(s_jumpEndClipPath, false);
        }

        private static void ValidateDualEvents(
            string clipPath,
            bool requiresComboWindow)
        {
            string[] eventNames = AnimationUtility.GetAnimationEvents(
                    LoadRequiredAsset<AnimationClip>(clipPath))
                .Select(animationEvent => animationEvent.functionName)
                .ToArray();
            string[] requiredEvents =
            {
                "OpenMainHandDamageCollider",
                "CloseMainHandDamageCollider",
                "OpenOffHandDamageCollider",
                "CloseOffHandDamageCollider"
            };
            if (requiredEvents.Any(requiredEvent =>
                    !eventNames.Contains(requiredEvent)) ||
                eventNames.Count(eventName =>
                    eventName == "DrainStaminaBasedOnAttack") != 2 ||
                (requiresComboWindow &&
                    (!eventNames.Contains("EnableCanDoCombo") ||
                        !eventNames.Contains("DisableCanDoCombo"))))
            {
                throw new InvalidOperationException(
                    $"Power Stance clip {clipPath} has invalid events.");
            }
        }

        private static void ValidateWeaponModifiers()
        {
            AttackType[] dualAttackTypes =
            {
                AttackType.DualAttack01,
                AttackType.DualAttack02,
                AttackType.DualJumpAttack,
                AttackType.DualRunAttack,
                AttackType.DualRollAttack,
                AttackType.DualBackstepAttack
            };
            foreach (string weaponPath in s_weaponPaths)
            {
                WeaponItem weapon = LoadRequiredAsset<WeaponItem>(weaponPath);
                if (dualAttackTypes.Any(attackType =>
                        !Mathf.Approximately(
                            weapon.GetAttackDamageModifier(attackType),
                            0.77f)) ||
                    !Mathf.Approximately(
                        weapon.GetStaminaCostMultiplier(
                            AttackType.DualRunAttack),
                        weapon.GetStaminaCostMultiplier(
                            AttackType.RunningAttack01)) ||
                    !Mathf.Approximately(
                        weapon.GetStaminaCostMultiplier(
                            AttackType.DualRollAttack),
                        weapon.GetStaminaCostMultiplier(
                            AttackType.RollAttack01)) ||
                    !Mathf.Approximately(
                        weapon.GetStaminaCostMultiplier(
                            AttackType.DualBackstepAttack),
                        weapon.GetStaminaCostMultiplier(
                            AttackType.BackStepAttack01)))
                {
                    throw new InvalidOperationException(
                        $"Weapon {weapon.name} has invalid dual modifiers.");
                }
            }
        }

        private static void ValidateRuntimeArchitecture()
        {
            const BindingFlags k_PublicInstance =
                BindingFlags.Public | BindingFlags.Instance;
            if (typeof(PlayerCombatManager).GetMethod(
                    "PerformPowerStanceLeftHandAction",
                    k_PublicInstance) == null ||
                typeof(PlayerEquipmentManager).GetMethod(
                    "OpenMainHandDamageCollider",
                    k_PublicInstance) == null ||
                typeof(PlayerEquipmentManager).GetMethod(
                    "OpenOffHandDamageCollider",
                    k_PublicInstance) == null ||
                typeof(PlayerAnimatorManager).GetMethod(
                    "OpenMainHandDamageCollider",
                    k_PublicInstance) == null ||
                typeof(CharacterNetworkManager).GetMethod(
                    "NotifyServerOfAttackActionServerRpc",
                    k_PublicInstance) == null)
            {
                throw new InvalidOperationException(
                    "Power Stance runtime routing is incomplete.");
            }
        }

        private static void SetLooping(AnimationClip clip, bool shouldLoop)
        {
            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = shouldLoop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
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
            foreach (AnimatorStateTransition transition in
                state.transitions.ToArray())
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
            AnimatorStateTransition transition = source.AddTransition(
                destination);
            transition.hasExitTime = hasExitTime;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.canTransitionToSelf = false;
            transition.AddCondition(
                isGrounded
                    ? AnimatorConditionMode.If
                    : AnimatorConditionMode.IfNot,
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
            AnimatorStateTransition transition = source.AddTransition(
                destination);
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
                .SingleOrDefault(candidate =>
                    candidate.destinationState == destination);
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

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private readonly struct PowerStanceClip
        {
            public PowerStanceClip(
                string stateName,
                string clipPath,
                bool supportsCombo,
                bool offHandFirst,
                float rowY)
            {
                StateName = stateName;
                ClipPath = clipPath;
                SupportsCombo = supportsCombo;
                OffHandFirst = offHandFirst;
                RowY = rowY;
            }

            public string StateName { get; }
            public string ClipPath { get; }
            public bool SupportsCombo { get; }
            public bool OffHandFirst { get; }
            public float RowY { get; }
        }
    }
}
