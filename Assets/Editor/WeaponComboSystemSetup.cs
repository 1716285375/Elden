using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    public static class WeaponComboSystemSetup
    {
        private const string k_ControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_LightAttack01ClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/Sword/" +
            "straight_sword_main_light_attack_01.anim";
        private const string k_LightAttack02ClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/Sword/" +
            "straight_sword_main_light_attack_02.anim";
        private const string k_HeavyAttack01ClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/Sword/" +
            "straight_sword_main_charged_attack_01_release.anim";
        private const string k_ChargedAttack01ClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/Sword/" +
            "straight_sword_main_charged_attack_01_release_full.anim";
        private const string k_HeavyAttack02ClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/Sword/" +
            "straight_sword_main_charged_attack_02_release.anim";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_LightAttack01StateName = "Attack_01";
        private const string k_LightAttack02StateName = "Attack_Light_02";
        private const string k_LightAttack03StateName = "Attack_Light_03";
        private const string k_HeavyAttack01StateName = "Attack_02";
        private const string k_ChargedAttack01StateName = "Attack_Charged_01";
        private const string k_HeavyAttack02StateName = "Attack_Heavy_02";
        private const string k_CloseDamageColliderEvent = "CloseDamageCollider";
        private const string k_EnableComboEvent = "EnableCanDoCombo";
        private const string k_DisableComboEvent = "DisableCanDoCombo";

        private static readonly string[] s_comboClipPaths =
        {
            k_LightAttack01ClipPath,
            k_LightAttack02ClipPath,
            k_HeavyAttack01ClipPath,
            k_ChargedAttack01ClipPath,
            k_HeavyAttack02ClipPath
        };

        [MenuItem("Tools/Elden/Configure Weapon Combo System")]
        public static void ConfigureWeaponComboSystem()
        {
            ConfigureAnimatorController();
            AssetDatabase.SaveAssets();
            ValidateWeaponComboSystem();
            Debug.Log(
                "[WeaponComboSystemSetup] Configured three-hit light and two-hit " +
                "heavy combos driven by authored animation windows.");
        }

        [MenuItem("Tools/Elden/Validate Weapon Combo System")]
        public static void ValidateWeaponComboSystem()
        {
            ValidateAttackTypeIdentifiers();
            ValidateComboTransitions();
            ValidateAnimationEvents();
            ValidateAnimatorController();
            ValidateAnimationEventReceivers();
            ValidateWeaponModifiers();
            Debug.Log(
                "[WeaponComboSystemValidation] Combo transitions, animation events, " +
                "Animator states, event receivers, and attack modifiers are valid.");
        }

        private static void ConfigureAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            AnimatorStateMachine stateMachine = GetRequiredLayer(controller).stateMachine;
            AnimatorState emptyState = GetRequiredState(stateMachine, k_EmptyStateName);
            AnimatorState lightAttack02State = GetOrCreateState(
                stateMachine,
                k_LightAttack02StateName,
                new Vector3(1010f, 300f, 0f));
            AnimatorState lightAttack03State = GetOrCreateState(
                stateMachine,
                k_LightAttack03StateName,
                new Vector3(1230f, 300f, 0f));
            AnimatorState heavyAttack02State = GetOrCreateState(
                stateMachine,
                k_HeavyAttack02StateName,
                new Vector3(1230f, 430f, 0f));

            lightAttack02State.motion = LoadRequiredAsset<AnimationClip>(
                k_LightAttack02ClipPath);
            lightAttack03State.motion = LoadRequiredAsset<AnimationClip>(
                k_LightAttack01ClipPath);
            heavyAttack02State.motion = LoadRequiredAsset<AnimationClip>(
                k_HeavyAttack02ClipPath);
            ConfigureExitTransition(lightAttack02State, emptyState);
            ConfigureExitTransition(lightAttack03State, emptyState);
            ConfigureExitTransition(heavyAttack02State, emptyState);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureExitTransition(
            AnimatorState sourceState,
            AnimatorState destinationState)
        {
            AnimatorStateTransition transition = sourceState.transitions
                .FirstOrDefault(candidate =>
                    candidate.destinationState == destinationState) ??
                sourceState.AddTransition(destinationState);
            transition.hasExitTime = true;
            transition.exitTime = 0.9f;
            transition.hasFixedDuration = true;
            transition.duration = 0.25f;
            transition.conditions = Array.Empty<AnimatorCondition>();
            EditorUtility.SetDirty(transition);
        }

        private static void ValidateAttackTypeIdentifiers()
        {
            if ((int)AttackType.LightAttack01 != 0 ||
                (int)AttackType.HeavyAttack01 != 1 ||
                (int)AttackType.ChargedAttack01 != 2 ||
                (int)AttackType.LightAttack02 != 3 ||
                (int)AttackType.LightAttack03 != 4 ||
                (int)AttackType.HeavyAttack02 != 5)
            {
                throw new InvalidOperationException(
                    "AttackType identifiers must remain stable for serialized and RPC data.");
            }
        }

        private static void ValidateComboTransitions()
        {
            MethodInfo transitionMethod = typeof(PlayerCombatManager).GetMethod(
                "TryGetNextMainHandComboAttack",
                BindingFlags.Static | BindingFlags.NonPublic) ??
                throw new InvalidOperationException(
                    "PlayerCombatManager is missing combo transition resolution.");
            ValidateComboTransition(
                transitionMethod,
                AttackType.LightAttack01,
                AttackType.LightAttack01,
                true,
                AttackType.LightAttack02);
            ValidateComboTransition(
                transitionMethod,
                AttackType.LightAttack02,
                AttackType.LightAttack01,
                true,
                AttackType.LightAttack03);
            ValidateComboTransition(
                transitionMethod,
                AttackType.LightAttack03,
                AttackType.LightAttack01,
                false,
                default);
            ValidateComboTransition(
                transitionMethod,
                AttackType.HeavyAttack01,
                AttackType.HeavyAttack01,
                true,
                AttackType.HeavyAttack02);
            ValidateComboTransition(
                transitionMethod,
                AttackType.ChargedAttack01,
                AttackType.HeavyAttack01,
                true,
                AttackType.HeavyAttack02);
            ValidateComboTransition(
                transitionMethod,
                AttackType.HeavyAttack02,
                AttackType.HeavyAttack01,
                false,
                default);
            ValidateComboTransition(
                transitionMethod,
                AttackType.LightAttack01,
                AttackType.HeavyAttack01,
                false,
                default);
        }

        private static void ValidateComboTransition(
            MethodInfo transitionMethod,
            AttackType currentAttack,
            AttackType requestedOpeningAttack,
            bool expectedSuccess,
            AttackType expectedNextAttack)
        {
            object[] arguments =
            {
                currentAttack,
                requestedOpeningAttack,
                default(AttackType)
            };
            bool succeeded = (bool)transitionMethod.Invoke(null, arguments);
            AttackType nextAttack = (AttackType)arguments[2];
            if (succeeded != expectedSuccess ||
                succeeded && nextAttack != expectedNextAttack)
            {
                throw new InvalidOperationException(
                    $"Invalid combo transition from {currentAttack} " +
                    $"using {requestedOpeningAttack}.");
            }
        }

        private static void ValidateAnimationEvents()
        {
            foreach (string clipPath in s_comboClipPaths)
            {
                AnimationClip clip = LoadRequiredAsset<AnimationClip>(clipPath);
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
                float closeTime = GetRequiredEventTime(
                    events,
                    k_CloseDamageColliderEvent,
                    clipPath);
                float enableTime = GetRequiredEventTime(
                    events,
                    k_EnableComboEvent,
                    clipPath);
                float disableTime = GetRequiredEventTime(
                    events,
                    k_DisableComboEvent,
                    clipPath);
                if (closeTime >= enableTime ||
                    enableTime >= disableTime ||
                    disableTime > clip.length)
                {
                    throw new InvalidOperationException(
                        $"Combo events are out of order on {clip.name}.");
                }
            }
        }

        private static float GetRequiredEventTime(
            AnimationEvent[] events,
            string eventName,
            string clipPath)
        {
            AnimationEvent animationEvent = events.FirstOrDefault(
                candidate => candidate.functionName == eventName);
            return animationEvent != null
                ? animationEvent.time
                : throw new InvalidOperationException(
                    $"{clipPath} is missing {eventName}.");
        }

        private static void ValidateAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            AnimatorStateMachine stateMachine = GetRequiredLayer(controller).stateMachine;
            AnimatorState emptyState = GetRequiredState(stateMachine, k_EmptyStateName);
            ValidateAttackState(
                stateMachine,
                k_LightAttack01StateName,
                k_LightAttack01ClipPath,
                emptyState);
            ValidateAttackState(
                stateMachine,
                k_LightAttack02StateName,
                k_LightAttack02ClipPath,
                emptyState);
            ValidateAttackState(
                stateMachine,
                k_LightAttack03StateName,
                k_LightAttack01ClipPath,
                emptyState);
            ValidateAttackState(
                stateMachine,
                k_HeavyAttack01StateName,
                k_HeavyAttack01ClipPath,
                emptyState);
            ValidateAttackState(
                stateMachine,
                k_ChargedAttack01StateName,
                k_ChargedAttack01ClipPath,
                emptyState);
            ValidateAttackState(
                stateMachine,
                k_HeavyAttack02StateName,
                k_HeavyAttack02ClipPath,
                emptyState);
        }

        private static void ValidateAttackState(
            AnimatorStateMachine stateMachine,
            string stateName,
            string clipPath,
            AnimatorState emptyState)
        {
            AnimatorState state = GetRequiredState(stateMachine, stateName);
            AnimationClip expectedClip = LoadRequiredAsset<AnimationClip>(clipPath);
            bool hasExitTransition = state.transitions.Any(transition =>
                transition.destinationState == emptyState &&
                transition.hasExitTime);
            if (state.motion != expectedClip || !hasExitTransition)
            {
                throw new InvalidOperationException(
                    $"Animator state {stateName} has an invalid clip or exit.");
            }
        }

        private static void ValidateAnimationEventReceivers()
        {
            BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;
            if (typeof(PlayerAnimatorManager).GetMethod(
                    k_EnableComboEvent,
                    publicInstance) == null ||
                typeof(PlayerAnimatorManager).GetMethod(
                    k_DisableComboEvent,
                    publicInstance) == null ||
                typeof(PlayerCombatManager).GetMethod(
                    "EnableCanCombo",
                    publicInstance) == null ||
                typeof(PlayerCombatManager).GetMethod(
                    "DisableCanCombo",
                    publicInstance) == null)
            {
                throw new InvalidOperationException(
                    "Combo animation-event receivers are missing.");
            }
        }

        private static void ValidateWeaponModifiers()
        {
            string[] weaponPaths =
            {
                "Assets/_Game/Data/Items/Weapons/Melee Weapons/Unarmed.asset",
                "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset",
                "Assets/_Game/Data/Items/Weapons/Melee Weapons/Broadsword.asset"
            };
            foreach (string weaponPath in weaponPaths)
            {
                WeaponItem weapon = LoadRequiredAsset<WeaponItem>(weaponPath);
                if (!Mathf.Approximately(
                        weapon.GetAttackDamageModifier(AttackType.LightAttack01),
                        weapon.GetAttackDamageModifier(AttackType.LightAttack02)) ||
                    !Mathf.Approximately(
                        weapon.GetAttackDamageModifier(AttackType.LightAttack01),
                        weapon.GetAttackDamageModifier(AttackType.LightAttack03)) ||
                    !Mathf.Approximately(
                        weapon.GetAttackDamageModifier(AttackType.HeavyAttack01),
                        weapon.GetAttackDamageModifier(AttackType.HeavyAttack02)) ||
                    !Mathf.Approximately(
                        weapon.GetStaminaCostMultiplier(AttackType.HeavyAttack01),
                        weapon.GetStaminaCostMultiplier(AttackType.HeavyAttack02)))
                {
                    throw new InvalidOperationException(
                        $"Weapon {weapon.name} has inconsistent combo modifiers.");
                }
            }
        }

        private static AnimatorControllerLayer GetRequiredLayer(
            AnimatorController controller)
        {
            return controller.layers.FirstOrDefault(
                layer => layer.name == k_ActionLayerName) ??
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

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }
    }
}
