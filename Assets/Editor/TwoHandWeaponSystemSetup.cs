using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the complete EP60-62 two-hand weapon system.</summary>
    public static class TwoHandWeaponSystemSetup
    {
        private const int k_TwoHandingEffectID = 1;
        private const string k_PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_InputActionsPath = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_ControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_TwoHandingEffectPath =
            "Assets/Data/Effects/Static Effects/Two Handing Effect.asset";
        private const string k_LightActionPath = "Assets/Data/Actions/Light Attack.asset";
        private const string k_HeavyActionPath = "Assets/Data/Actions/Heavy Attack.asset";
        private const string k_LocomotionFolder =
            "Assets/Art/Animations/Characters/Humanoid/Locomotion/";
        private const string k_CombatFolder =
            "Assets/Art/Animations/Characters/Humanoid/Combat/Sword/";
        private const string k_BaseLayerName = "Base Layer";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_OneHandLocomotionStateName = "Locomotion One Handed";
        private const string k_TwoHandLocomotionStateName = "Locomotion Two Handed";
        private const string k_TwoHandBlockingIdleStateName = "Blocking Idle Two Handed";
        private const string k_TwoHandBlockingLocomotionStateName =
            "Blocking Locomotion Two Handed";
        private const string k_IsTwoHandingParameter = "isTwoHandingWeapon";
        private const string k_IsBlockingParameter = "isBlocking";
        private const string k_IsMovingParameter = "isMoving";
        private const string k_HorizontalParameter = "Horizontal";
        private const string k_VerticalParameter = "Vertical";

        private static readonly WeaponDefinition[] s_weaponDefinitions =
        {
            new WeaponDefinition(
                "Assets/Data/Items/Weapons/Melee Weapons/Unarmed.asset",
                WeaponClass.Unarmed,
                false),
            new WeaponDefinition(
                "Assets/Data/Items/Weapons/Melee Weapons/Straight Sword.asset",
                WeaponClass.StraightSword,
                true),
            new WeaponDefinition(
                "Assets/Data/Items/Weapons/Melee Weapons/Broadsword.asset",
                WeaponClass.StraightSword,
                true),
            new WeaponDefinition(
                "Assets/Data/Items/Weapons/Melee Weapons/Medium Shield.asset",
                WeaponClass.Shield,
                true)
        };

        private static readonly AttackStateDefinition[] s_attackStates =
        {
            new AttackStateDefinition(
                "TwoHand_Attack_Light_01",
                "straight_sword_th_light_attack_01.anim",
                true),
            new AttackStateDefinition(
                "TwoHand_Attack_Light_02",
                "straight_sword_th_light_attack_02.anim",
                true),
            new AttackStateDefinition(
                "TwoHand_Attack_Light_03",
                "straight_sword_th_light_attack_01.anim",
                true),
            new AttackStateDefinition(
                "TwoHand_Attack_Heavy_01",
                "straight_sword_th_charged_attack_01_release.anim",
                true),
            new AttackStateDefinition(
                "TwoHand_Attack_Heavy_02",
                "straight_sword_th_charged_attack_02_release.anim",
                false),
            new AttackStateDefinition(
                "TwoHand_Attack_Charged_01",
                "straight_sword_th_charged_attack_01_release_full.anim",
                true),
            new AttackStateDefinition(
                "TwoHand_Attack_Charge_01",
                "straight_sword_th_charged_attack_01_charge.anim",
                false,
                false),
            new AttackStateDefinition(
                "TwoHand_RunAttack01",
                "../Locomotion/straight_sword_th_run_attack_01.anim",
                false),
            new AttackStateDefinition(
                "TwoHand_RollAttack01",
                "../Locomotion/straight_sword_th_roll_attack_01_release.anim",
                false),
            new AttackStateDefinition(
                "TwoHand_BackStepAttack01",
                "straight_sword_th_back_step_attack_02_release.anim",
                false)
        };

        [MenuItem("Tools/Elden/Configure Two-Hand Weapon System")]
        public static void ConfigureTwoHandWeaponSystem()
        {
            ConfigureInputActions();
            TwoHandingEffect effect = ConfigureStaticEffect();
            ConfigureWeaponData();
            ConfigurePlayerPrefab(effect);
            ConfigureAnimatorController();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                k_InputActionsPath,
                ImportAssetOptions.ForceSynchronousImport);
            ValidateTwoHandWeaponSystem();
            Debug.Log(
                "[TwoHandWeaponSystemSetup] Configured modifier input, replicated stance, " +
                "back/hip storage, two-hand locomotion, attacks, blocking, and Strength.");
        }

        [MenuItem("Tools/Elden/Validate Two-Hand Weapon System")]
        public static void ValidateTwoHandWeaponSystem()
        {
            ValidateInputActions();
            ValidateWeaponData();
            ValidatePlayerPrefab();
            ValidateAnimatorController();
            ValidateRuntimeArchitecture();
            ValidateStrengthFormula();
            Debug.Log(
                "[TwoHandWeaponSystemValidation] Input, network replay, model routing, " +
                "Animator states/events, static effect, and Strength formula are valid.");
        }

        private static void ConfigureInputActions()
        {
            InputActionAsset inputActions = LoadRequiredAsset<InputActionAsset>(
                k_InputActionsPath);
            InputActionMap movementMap = inputActions.FindActionMap("Player Movement", true);
            InputAction modifier = GetOrCreateAction(
                movementMap,
                "Two Hand Weapon",
                InputActionType.PassThrough);
            InputAction right = GetOrCreateAction(
                movementMap,
                "Two Hand Right Weapon",
                InputActionType.Button);
            InputAction left = GetOrCreateAction(
                movementMap,
                "Two Hand Left Weapon",
                InputActionType.Button);
            EnsureBinding(modifier, "<Gamepad>/buttonNorth", "Gamepad", "hold");
            EnsureBinding(modifier, "<Keyboard>/y", "Keyboard&Mouse", "hold");
            EnsureBinding(right, "<Gamepad>/rightShoulder", "Gamepad", string.Empty);
            EnsureBinding(right, "<Mouse>/leftButton", "Keyboard&Mouse", string.Empty);
            EnsureBinding(left, "<Gamepad>/leftShoulder", "Gamepad", string.Empty);
            EnsureBinding(left, "<Keyboard>/leftCtrl", "Keyboard&Mouse", string.Empty);
            EditorUtility.SetDirty(inputActions);
        }

        private static TwoHandingEffect ConfigureStaticEffect()
        {
            EnsureAssetFolder("Assets/Data/Effects");
            EnsureAssetFolder("Assets/Data/Effects/Static Effects");
            TwoHandingEffect effect = AssetDatabase.LoadAssetAtPath<TwoHandingEffect>(
                k_TwoHandingEffectPath);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<TwoHandingEffect>();
                AssetDatabase.CreateAsset(effect, k_TwoHandingEffectPath);
            }

            SerializedObject serializedEffect = new SerializedObject(effect);
            SetInt(serializedEffect, "m_staticEffectID", k_TwoHandingEffectID);
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
            return effect;
        }

        private static void ConfigureWeaponData()
        {
            WeaponItemBasedAction lightAction =
                LoadRequiredAsset<WeaponItemBasedAction>(k_LightActionPath);
            WeaponItemBasedAction heavyAction =
                LoadRequiredAsset<WeaponItemBasedAction>(k_HeavyActionPath);
            foreach (WeaponDefinition definition in s_weaponDefinitions)
            {
                WeaponItem weapon = LoadRequiredAsset<WeaponItem>(definition.Path);
                SerializedObject serializedWeapon = new SerializedObject(weapon);
                SetEnum(serializedWeapon, "m_weaponClass", (int)definition.WeaponClass);
                SetObjectReference(
                    serializedWeapon,
                    "m_twoHandRightAction",
                    definition.SupportsTwoHanding ? lightAction : null);
                SetObjectReference(
                    serializedWeapon,
                    "m_twoHandRightHeavyAction",
                    definition.SupportsTwoHanding ? heavyAction : null);
                serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
            }
        }

        private static void ConfigurePlayerPrefab(TwoHandingEffect effect)
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerNetworkManager networkManager =
                    GetRequiredComponent<PlayerNetworkManager>(playerRoot);
                SetObjectReference(
                    new SerializedObject(networkManager),
                    "m_twoHandingEffect",
                    effect);
                CharacterStatsManager statsManager =
                    GetRequiredComponent<CharacterStatsManager>(playerRoot);
                SerializedObject serializedStats = new SerializedObject(statsManager);
                SetInt(serializedStats, "m_strengthLevel", 10);
                SetInt(serializedStats, "m_strengthModifier", 0);
                serializedStats.ApplyModifiedPropertiesWithoutUndo();

                Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman)
                {
                    throw new InvalidOperationException(
                        "Player prefab needs a Humanoid Animator for two-hand storage slots.");
                }

                ConfigureWeaponSlot(
                    animator.GetBoneTransform(HumanBodyBones.Chest),
                    "Back Weapon Slot",
                    WeaponModelSlot.BackSlot);
                ConfigureWeaponSlot(
                    animator.GetBoneTransform(HumanBodyBones.Hips),
                    "Hip Weapon Slot",
                    WeaponModelSlot.HipSlot);
                if (PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the Player two-hand configuration.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigureAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            EnsureParameter(
                controller,
                k_IsTwoHandingParameter,
                AnimatorControllerParameterType.Bool);
            AnimatorStateMachine baseStateMachine = GetRequiredLayer(
                controller,
                k_BaseLayerName).stateMachine;
            AnimatorState oneHandLocomotion = GetRequiredState(
                baseStateMachine,
                k_OneHandLocomotionStateName);
            AnimatorState twoHandLocomotion = GetOrCreateState(
                baseStateMachine,
                k_TwoHandLocomotionStateName,
                new Vector3(350f, -130f, 0f));
            twoHandLocomotion.motion = ConfigureTwoHandLocomotionBlendTree(
                controller,
                twoHandLocomotion);
            ConfigureBoolTransition(
                oneHandLocomotion,
                twoHandLocomotion,
                k_IsTwoHandingParameter,
                true);
            ConfigureBoolTransition(
                twoHandLocomotion,
                oneHandLocomotion,
                k_IsTwoHandingParameter,
                false);
            ConfigureTwoHandBlocking(baseStateMachine, twoHandLocomotion);
            ConfigureTwoHandAttackStates(controller);
            EditorUtility.SetDirty(controller);
        }

        private static BlendTree ConfigureTwoHandLocomotionBlendTree(
            AnimatorController controller,
            AnimatorState state)
        {
            BlendTree blendTree = state.motion as BlendTree;
            if (blendTree == null)
            {
                blendTree = new BlendTree { name = "Two-Hand Locomotion Blend Tree" };
                AssetDatabase.AddObjectToAsset(blendTree, controller);
            }

            List<ChildMotion> children = new List<ChildMotion>
            {
                CreateChildMotion("straight_sword_th_idle_01.anim", Vector2.zero)
            };
            AddDirectionalLocomotionChildren(children, "core_th_walk_", 0.5f);
            AddDirectionalLocomotionChildren(children, "core_th_run_", 1f);
            children.Add(CreateChildMotion("core_th_run_F_01.anim", new Vector2(0f, 2f)));
            blendTree.blendType = BlendTreeType.FreeformCartesian2D;
            blendTree.blendParameter = k_HorizontalParameter;
            blendTree.blendParameterY = k_VerticalParameter;
            blendTree.useAutomaticThresholds = false;
            blendTree.children = children.ToArray();
            EditorUtility.SetDirty(blendTree);
            return blendTree;
        }

        private static void AddDirectionalLocomotionChildren(
            ICollection<ChildMotion> children,
            string clipPrefix,
            float magnitude)
        {
            children.Add(CreateChildMotion(clipPrefix + "F_01.anim", new Vector2(0f, magnitude)));
            children.Add(CreateChildMotion(clipPrefix + "B_01.anim", new Vector2(0f, -magnitude)));
            children.Add(CreateChildMotion(clipPrefix + "L_01.anim", new Vector2(-magnitude, 0f)));
            children.Add(CreateChildMotion(clipPrefix + "R_01.anim", new Vector2(magnitude, 0f)));
            children.Add(CreateChildMotion(
                clipPrefix + "FL_01.anim",
                new Vector2(-magnitude, magnitude)));
            children.Add(CreateChildMotion(
                clipPrefix + "FR_01.anim",
                new Vector2(magnitude, magnitude)));
            children.Add(CreateChildMotion(
                clipPrefix + "BL_01.anim",
                new Vector2(-magnitude, -magnitude)));
            children.Add(CreateChildMotion(
                clipPrefix + "BR_01.anim",
                new Vector2(magnitude, -magnitude)));
        }

        private static void ConfigureTwoHandBlocking(
            AnimatorStateMachine stateMachine,
            AnimatorState twoHandLocomotion)
        {
            AnimatorState blockingIdle = GetRequiredState(
                stateMachine,
                k_TwoHandBlockingIdleStateName);
            AnimatorState blockingLocomotion = GetRequiredState(
                stateMachine,
                k_TwoHandBlockingLocomotionStateName);
            ConfigureBoolTransition(
                twoHandLocomotion,
                blockingIdle,
                k_IsBlockingParameter,
                true);
            ConfigureBoolTransition(
                blockingIdle,
                twoHandLocomotion,
                k_IsBlockingParameter,
                false);
            ConfigureBoolTransition(
                blockingLocomotion,
                twoHandLocomotion,
                k_IsBlockingParameter,
                false);
            ConfigureBoolTransition(
                blockingIdle,
                blockingLocomotion,
                k_IsMovingParameter,
                true);
            ConfigureBoolTransition(
                blockingLocomotion,
                blockingIdle,
                k_IsMovingParameter,
                false);
        }

        private static void ConfigureTwoHandAttackStates(AnimatorController controller)
        {
            AnimatorStateMachine stateMachine = GetRequiredLayer(
                controller,
                k_ActionLayerName).stateMachine;
            AnimatorState emptyState = GetRequiredState(stateMachine, k_EmptyStateName);
            for (int stateIndex = 0; stateIndex < s_attackStates.Length; stateIndex++)
            {
                AttackStateDefinition definition = s_attackStates[stateIndex];
                AnimationClip clip = LoadRequiredAsset<AnimationClip>(
                    ResolveAttackClipPath(definition.ClipName));
                AnimatorState attackState = GetOrCreateState(
                    stateMachine,
                    definition.StateName,
                    new Vector3(
                        1140f + stateIndex % 2 * 270f,
                        -250f + stateIndex / 2 * 90f,
                        0f));
                attackState.motion = clip;
                ConfigureExitTransition(attackState, emptyState);
                if (definition.ConfigureDamageEvents)
                {
                    ConfigureAttackAnimationEvents(clip, definition.HasComboWindow);
                }
            }
        }

        private static void ConfigureAttackAnimationEvents(
            AnimationClip clip,
            bool hasComboWindow)
        {
            float clipLength = Mathf.Max(0.01f, clip.length);
            List<AnimationEvent> events = new List<AnimationEvent>
            {
                CreateAnimationEvent("DisableCanRotate", clipLength, 0.05f),
                CreateAnimationEvent("DrainStaminaBasedOnAttack", clipLength, 0.12f),
                CreateAnimationEvent("ActivateMainHandWeaponTrail", clipLength, 0.16f),
                CreateAnimationEvent("OpenDamageCollider", clipLength, 0.2f),
                CreateAnimationEvent("CloseDamageCollider", clipLength, 0.58f),
                CreateAnimationEvent("DeactivateMainHandWeaponTrail", clipLength, 0.6f),
                CreateAnimationEvent("EnableCanRotate", clipLength, 0.72f),
                CreateAnimationEvent("EnableCanRoll", clipLength, 0.78f),
                CreateAnimationEvent("EnableCanMoveCancel", clipLength, 0.86f)
            };
            if (hasComboWindow)
            {
                events.Add(CreateAnimationEvent("EnableCanDoCombo", clipLength, 0.5f));
                events.Add(CreateAnimationEvent("DisableCanDoCombo", clipLength, 0.76f));
            }

            AnimationUtility.SetAnimationEvents(
                clip,
                events.OrderBy(animationEvent => animationEvent.time).ToArray());
            EditorUtility.SetDirty(clip);
        }

        private static void ValidateInputActions()
        {
            InputActionAsset inputActions = LoadRequiredAsset<InputActionAsset>(
                k_InputActionsPath);
            InputActionMap movementMap = inputActions.FindActionMap("Player Movement", true);
            InputAction modifier = movementMap.FindAction("Two Hand Weapon", true);
            InputAction right = movementMap.FindAction("Two Hand Right Weapon", true);
            InputAction left = movementMap.FindAction("Two Hand Left Weapon", true);
            if (modifier.type != InputActionType.PassThrough ||
                !HasBinding(modifier, "<Gamepad>/buttonNorth", "hold") ||
                !HasBinding(modifier, "<Keyboard>/y", "hold") ||
                !HasBinding(right, "<Gamepad>/rightShoulder", null) ||
                !HasBinding(left, "<Gamepad>/leftShoulder", null))
            {
                throw new InvalidOperationException(
                    "Two-hand modifier and side inputs are not configured correctly.");
            }
        }

        private static void ValidateWeaponData()
        {
            WeaponItemBasedAction lightAction =
                LoadRequiredAsset<WeaponItemBasedAction>(k_LightActionPath);
            WeaponItemBasedAction heavyAction =
                LoadRequiredAsset<WeaponItemBasedAction>(k_HeavyActionPath);
            foreach (WeaponDefinition definition in s_weaponDefinitions)
            {
                WeaponItem weapon = LoadRequiredAsset<WeaponItem>(definition.Path);
                if (weapon.WeaponClass != definition.WeaponClass ||
                    (definition.SupportsTwoHanding &&
                        (weapon.TwoHandRightAction != lightAction ||
                            weapon.TwoHandRightHeavyAction != heavyAction)))
                {
                    throw new InvalidOperationException(
                        $"Two-hand data is invalid on {definition.Path}.");
                }
            }

            TwoHandingEffect effect = LoadRequiredAsset<TwoHandingEffect>(
                k_TwoHandingEffectPath);
            if (effect.StaticEffectID != k_TwoHandingEffectID)
            {
                throw new InvalidOperationException(
                    "Two Handing Effect needs its stable static effect identifier.");
            }
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerNetworkManager networkManager =
                    GetRequiredComponent<PlayerNetworkManager>(playerRoot);
                SerializedObject serializedNetwork = new SerializedObject(networkManager);
                if (GetRequiredProperty(serializedNetwork, "m_twoHandingEffect")
                    .objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        "PlayerNetworkManager needs the Two Handing Effect asset.");
                }

                HashSet<WeaponModelSlot> slots = playerRoot
                    .GetComponentsInChildren<WeaponModelInstantiationSlot>(true)
                    .Select(slot => slot.WeaponModelSlot)
                    .ToHashSet();
                if (!slots.Contains(WeaponModelSlot.BackSlot) ||
                    !slots.Contains(WeaponModelSlot.HipSlot))
                {
                    throw new InvalidOperationException(
                        "Player prefab needs both BackSlot and HipSlot.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            if (!controller.parameters.Any(parameter =>
                    parameter.name == k_IsTwoHandingParameter &&
                    parameter.type == AnimatorControllerParameterType.Bool))
            {
                throw new InvalidOperationException(
                    "Animator needs the isTwoHandingWeapon Bool parameter.");
            }

            AnimatorStateMachine baseStateMachine = GetRequiredLayer(
                controller,
                k_BaseLayerName).stateMachine;
            AnimatorState twoHandLocomotion = GetRequiredState(
                baseStateMachine,
                k_TwoHandLocomotionStateName);
            if (twoHandLocomotion.motion is not BlendTree blendTree ||
                blendTree.children.Length != 18)
            {
                throw new InvalidOperationException(
                    "Two-hand locomotion needs idle plus 8-direction walk/run and sprint.");
            }

            AnimatorStateMachine actionStateMachine = GetRequiredLayer(
                controller,
                k_ActionLayerName).stateMachine;
            foreach (AttackStateDefinition definition in s_attackStates)
            {
                AnimatorState state = GetRequiredState(
                    actionStateMachine,
                    definition.StateName);
                if (state.motion == null)
                {
                    throw new InvalidOperationException(
                        $"Animator state {definition.StateName} needs a motion.");
                }

                if (definition.ConfigureDamageEvents)
                {
                    ValidateAttackAnimationEvents((AnimationClip)state.motion);
                }
            }
        }

        private static void ValidateAttackAnimationEvents(AnimationClip clip)
        {
            HashSet<string> eventNames = AnimationUtility.GetAnimationEvents(clip)
                .Select(animationEvent => animationEvent.functionName)
                .ToHashSet();
            string[] requiredEvents =
            {
                "DrainStaminaBasedOnAttack",
                "OpenDamageCollider",
                "CloseDamageCollider",
                "EnableCanRotate",
                "DisableCanRotate"
            };
            if (requiredEvents.Any(requiredEvent => !eventNames.Contains(requiredEvent)))
            {
                throw new InvalidOperationException(
                    $"Two-hand attack clip {clip.name} is missing gameplay events.");
            }
        }

        private static void ValidateRuntimeArchitecture()
        {
            AssertOwnerWrittenNetworkVariable("m_isTwoHandingWeapon", typeof(bool));
            AssertOwnerWrittenNetworkVariable("m_isTwoHandingRightWeapon", typeof(bool));
            AssertOwnerWrittenNetworkVariable("m_isTwoHandingLeftWeapon", typeof(bool));
            AssertOwnerWrittenNetworkVariable("m_currentWeaponBeingTwoHanded", typeof(int));
            AssertMethod(typeof(PlayerNetworkManager), "ToggleTwoHandWeapon", typeof(bool));
            AssertMethod(typeof(PlayerEquipmentManager), "TwoHandRightWeapon");
            AssertMethod(typeof(PlayerEquipmentManager), "TwoHandLeftWeapon");
            AssertMethod(typeof(PlayerEquipmentManager), "UnTwoHandWeapon");
            AssertMethod(
                typeof(PlayerEquipmentManager),
                "PlaceWeaponModelInUnequippedSlot",
                typeof(WeaponItem));
            AssertMethod(
                typeof(CharacterEffectsManager),
                "ProcessStaticEffect",
                typeof(StaticCharacterEffect));
            AssertMethod(typeof(CharacterEffectsManager), "RemoveStaticEffect", typeof(int));
        }

        private static void ValidateStrengthFormula()
        {
            if (TwoHandingEffect.CalculateStrengthBonus(10) != 5 ||
                TwoHandingEffect.CalculateStrengthBonus(11) != 6 ||
                TwoHandingEffect.CalculateStrengthBonus(-2) != 0)
            {
                throw new InvalidOperationException(
                    "Two-handing must grant rounded half of the base Strength level.");
            }
        }

        private static void AssertOwnerWrittenNetworkVariable(
            string fieldName,
            Type valueType)
        {
            FieldInfo field = typeof(PlayerNetworkManager).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(NetworkVariable<>).MakeGenericType(valueType))
            {
                throw new InvalidOperationException(
                    $"PlayerNetworkManager.{fieldName} has an invalid NetworkVariable type.");
            }

            PlayerNetworkManager manager = new GameObject("Two-Hand Network Contract")
                .AddComponent<PlayerNetworkManager>();
            try
            {
                object variable = field.GetValue(manager);
                PropertyInfo permission = variable?.GetType().GetProperty("WritePerm");
                if (permission?.GetValue(variable) is not NetworkVariableWritePermission.Owner)
                {
                    throw new InvalidOperationException(
                        $"PlayerNetworkManager.{fieldName} must be owner-written.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            }
        }

        private static void AssertMethod(Type type, string methodName, params Type[] arguments)
        {
            if (type.GetMethod(methodName, arguments) == null)
            {
                throw new InvalidOperationException(
                    $"{type.Name}.{methodName} is required by the two-hand runtime contract.");
            }
        }

        private static InputAction GetOrCreateAction(
            InputActionMap map,
            string actionName,
            InputActionType actionType)
        {
            InputAction action = map.FindAction(actionName);
            return action ?? map.AddAction(
                actionName,
                actionType,
                expectedControlLayout: "Button");
        }

        private static void EnsureBinding(
            InputAction action,
            string path,
            string groups,
            string interactions)
        {
            for (int bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
            {
                InputBinding binding = action.bindings[bindingIndex];
                if (binding.path != path)
                {
                    continue;
                }

                action.ChangeBinding(bindingIndex)
                    .WithGroups(groups)
                    .WithInteractions(interactions);
                return;
            }

            action.AddBinding(path, groups: groups, interactions: interactions);
        }

        private static bool HasBinding(
            InputAction action,
            string path,
            string interactions)
        {
            return action.bindings.Any(binding =>
                binding.path == path &&
                (interactions == null || binding.interactions.Contains(interactions)));
        }

        private static void ConfigureWeaponSlot(
            Transform bone,
            string slotName,
            WeaponModelSlot slotType)
        {
            if (bone == null)
            {
                throw new InvalidOperationException(
                    $"Player avatar is missing the bone required for {slotType}.");
            }

            Transform slotTransform = bone.Find(slotName);
            if (slotTransform == null)
            {
                slotTransform = new GameObject(slotName).transform;
                slotTransform.SetParent(bone, false);
            }

            slotTransform.localPosition = Vector3.zero;
            slotTransform.localRotation = Quaternion.identity;
            slotTransform.localScale = Vector3.one;
            WeaponModelInstantiationSlot slot =
                slotTransform.GetComponent<WeaponModelInstantiationSlot>() ??
                slotTransform.gameObject.AddComponent<WeaponModelInstantiationSlot>();
            SerializedObject serializedSlot = new SerializedObject(slot);
            SetEnum(serializedSlot, "m_weaponModelSlot", (int)slotType);
            serializedSlot.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ChildMotion CreateChildMotion(string clipName, Vector2 position)
        {
            return new ChildMotion
            {
                motion = LoadRequiredAsset<AnimationClip>(k_LocomotionFolder + clipName),
                position = position,
                timeScale = 1f
            };
        }

        private static string ResolveAttackClipPath(string clipName)
        {
            const string k_ParentLocomotionPrefix = "../Locomotion/";
            return clipName.StartsWith(k_ParentLocomotionPrefix, StringComparison.Ordinal)
                ? k_LocomotionFolder + clipName.Substring(k_ParentLocomotionPrefix.Length)
                : k_CombatFolder + clipName;
        }

        private static AnimationEvent CreateAnimationEvent(
            string functionName,
            float clipLength,
            float normalizedTime)
        {
            return new AnimationEvent
            {
                functionName = functionName,
                time = clipLength * normalizedTime
            };
        }

        private static void ConfigureBoolTransition(
            AnimatorState source,
            AnimatorState destination,
            string parameter,
            bool expectedValue)
        {
            AnimatorStateTransition transition = source.transitions.FirstOrDefault(candidate =>
                candidate.destinationState == destination &&
                candidate.conditions.Any(condition => condition.parameter == parameter)) ??
                source.AddTransition(destination);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.15f;
            transition.canTransitionToSelf = false;
            transition.conditions = Array.Empty<AnimatorCondition>();
            transition.AddCondition(
                expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                parameter);
            EditorUtility.SetDirty(transition);
        }

        private static void ConfigureExitTransition(
            AnimatorState source,
            AnimatorState destination)
        {
            AnimatorStateTransition transition = source.transitions
                .FirstOrDefault(candidate => candidate.destinationState == destination) ??
                source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = 0.9f;
            transition.hasFixedDuration = true;
            transition.duration = 0.15f;
            transition.canTransitionToSelf = false;
            transition.conditions = Array.Empty<AnimatorCondition>();
            EditorUtility.SetDirty(transition);
        }

        private static AnimatorControllerLayer GetRequiredLayer(
            AnimatorController controller,
            string layerName)
        {
            return controller.layers.FirstOrDefault(layer => layer.name == layerName) ??
                throw new InvalidOperationException($"Animator layer {layerName} is missing.");
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

            throw new InvalidOperationException($"Animator state {stateName} is missing.");
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            if (!controller.parameters.Any(parameter => parameter.name == parameterName))
            {
                controller.AddParameter(parameterName, parameterType);
            }
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separatorIndex = path.LastIndexOf('/');
            string parentPath = path.Substring(0, separatorIndex);
            string folderName = path.Substring(separatorIndex + 1);
            EnsureAssetFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static T GetRequiredComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
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

        private static void SetInt(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).intValue = value;
        }

        private static void SetEnum(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).enumValueIndex = value;
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private readonly struct WeaponDefinition
        {
            public WeaponDefinition(
                string path,
                WeaponClass weaponClass,
                bool supportsTwoHanding)
            {
                Path = path;
                WeaponClass = weaponClass;
                SupportsTwoHanding = supportsTwoHanding;
            }

            public string Path { get; }
            public WeaponClass WeaponClass { get; }
            public bool SupportsTwoHanding { get; }
        }

        private readonly struct AttackStateDefinition
        {
            public AttackStateDefinition(
                string stateName,
                string clipName,
                bool hasComboWindow,
                bool configureDamageEvents = true)
            {
                StateName = stateName;
                ClipName = clipName;
                HasComboWindow = hasComboWindow;
                ConfigureDamageEvents = configureDamageEvents;
            }

            public string StateName { get; }
            public string ClipName { get; }
            public bool HasComboWindow { get; }
            public bool ConfigureDamageEvents { get; }
        }
    }
}
