using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    public static class ChargedAttackSystemSetup
    {
        private const string k_ControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Base/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_PlayerControlsPath = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_MainMenuScenePath = WorldScenePathLayout.MainMenuScenePath;
        private const string k_LightAttackPath = "Assets/_Game/Data/Actions/Light Attack.asset";
        private const string k_HeavyAttackPath = "Assets/_Game/Data/Actions/Heavy Attack.asset";
        private const string k_ChargedAttackPath = "Assets/_Game/Data/Actions/Charged Attack.asset";
        private const string k_UnarmedPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Unarmed.asset";
        private const string k_StraightSwordPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset";
        private const string k_BroadswordPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Broadsword.asset";
        private const string k_ChargeClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Sword/" +
            "straight_sword_main_charged_attack_01_charge.anim";
        private const string k_HoldClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Sword/" +
            "straight_sword_main_charged_attack_01_hold.anim";
        private const string k_HeavyReleaseClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Sword/" +
            "straight_sword_main_charged_attack_01_release.anim";
        private const string k_ChargedReleaseClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Sword/" +
            "straight_sword_main_charged_attack_01_release_full.anim";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_ChargeStateName = "Attack_Charge_01";
        private const string k_HoldStateName = "Attack_Charge_Hold_01";
        private const string k_HeavyAttackStateName = "Attack_02";
        private const string k_ChargedAttackStateName = "Attack_Charged_01";
        private const string k_ChargingParameterName = "isChargingAttack";
        private const float k_HeavyDamageModifier = 1.25f;
        private const float k_ChargedDamageModifier = 1.75f;
        private const float k_HeavyStaminaModifier = 1.25f;
        private const float k_ChargedStaminaModifier = 1.5f;

        private static readonly string[] s_weaponPaths =
        {
            k_UnarmedPath,
            k_StraightSwordPath,
            k_BroadswordPath
        };

        [MenuItem("Tools/Elden/Configure Charged Attack System")]
        public static void ConfigureChargedAttackSystem()
        {
            LightAttackWeaponItemAction lightAttack =
                LoadRequiredAsset<LightAttackWeaponItemAction>(k_LightAttackPath);
            HeavyAttackWeaponItemAction heavyAttack =
                LoadRequiredAsset<HeavyAttackWeaponItemAction>(k_HeavyAttackPath);
            ChargedAttackWeaponItemAction chargedAttack = ConfigureChargedAttackAsset();
            ConfigureActionIdentifiers(lightAttack, heavyAttack, chargedAttack);
            ConfigureWeaponItems(chargedAttack);
            ConfigureActionCatalog(lightAttack, heavyAttack, chargedAttack);
            ConfigureAnimatorController();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateChargedAttackSystem();
            Debug.Log(
                "[ChargedAttackSystemSetup] Configured short heavy attacks, charged " +
                "release, owner-written charge state, and remote Animator presentation.");
        }

        [MenuItem("Tools/Elden/Validate Charged Attack System")]
        public static void ValidateChargedAttackSystem()
        {
            LightAttackWeaponItemAction lightAttack =
                LoadRequiredAsset<LightAttackWeaponItemAction>(k_LightAttackPath);
            HeavyAttackWeaponItemAction heavyAttack =
                LoadRequiredAsset<HeavyAttackWeaponItemAction>(k_HeavyAttackPath);
            ChargedAttackWeaponItemAction chargedAttack =
                LoadRequiredAsset<ChargedAttackWeaponItemAction>(k_ChargedAttackPath);
            ValidateActionAssets(lightAttack, heavyAttack, chargedAttack);
            ValidateWeaponItems(chargedAttack);
            ValidateActionCatalog(lightAttack, heavyAttack, chargedAttack);
            ValidateAnimatorController();
            ValidateNetworkContract();
            ValidateInputContract();
            ValidateChargeThreshold();
            Debug.Log(
                "[ChargedAttackSystemValidation] Actions, modifiers, hold threshold, " +
                "network permissions, input phases, and Animator states are valid.");
        }

        private static ChargedAttackWeaponItemAction ConfigureChargedAttackAsset()
        {
            ChargedAttackWeaponItemAction chargedAttack =
                AssetDatabase.LoadAssetAtPath<ChargedAttackWeaponItemAction>(
                    k_ChargedAttackPath);
            if (chargedAttack == null)
            {
                chargedAttack = ScriptableObject.CreateInstance<
                    ChargedAttackWeaponItemAction>();
                AssetDatabase.CreateAsset(chargedAttack, k_ChargedAttackPath);
            }

            SerializedObject serializedAction = new SerializedObject(chargedAttack);
            GetRequiredProperty(serializedAction, "m_attackType").enumValueIndex =
                (int)AttackType.ChargedAttack01;
            serializedAction.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chargedAttack);
            return chargedAttack;
        }

        private static void ConfigureActionIdentifiers(
            WeaponItemBasedAction lightAttack,
            WeaponItemBasedAction heavyAttack,
            WeaponItemBasedAction chargedAttack)
        {
            SetActionIdentifier(lightAttack, 0);
            SetActionIdentifier(heavyAttack, 1);
            SetActionIdentifier(chargedAttack, 2);
        }

        private static void SetActionIdentifier(
            WeaponItemBasedAction action,
            int actionIdentifier)
        {
            SerializedObject serializedAction = new SerializedObject(action);
            GetRequiredProperty(serializedAction, "m_actionID").intValue =
                actionIdentifier;
            serializedAction.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
        }

        private static void ConfigureWeaponItems(
            ChargedAttackWeaponItemAction chargedAttack)
        {
            foreach (string weaponPath in s_weaponPaths)
            {
                MeleeWeaponItem weapon = LoadRequiredAsset<MeleeWeaponItem>(weaponPath);
                SerializedObject serializedWeapon = new SerializedObject(weapon);
                GetRequiredProperty(serializedWeapon, "m_rightHandChargedAction")
                    .objectReferenceValue = chargedAttack;
                GetRequiredProperty(serializedWeapon, "m_lightAttack01DamageModifier")
                    .floatValue = 1f;
                GetRequiredProperty(serializedWeapon, "m_heavyAttack01DamageModifier")
                    .floatValue = k_HeavyDamageModifier;
                GetRequiredProperty(serializedWeapon, "m_chargedAttack01DamageModifier")
                    .floatValue = k_ChargedDamageModifier;
                GetRequiredProperty(
                    serializedWeapon,
                    "m_lightAttack01StaminaCostMultiplier").floatValue = 1f;
                GetRequiredProperty(
                    serializedWeapon,
                    "m_heavyAttack01StaminaCostMultiplier").floatValue =
                        k_HeavyStaminaModifier;
                GetRequiredProperty(
                    serializedWeapon,
                    "m_chargedAttack01StaminaCostMultiplier").floatValue =
                        k_ChargedStaminaModifier;
                serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
            }
        }

        private static void ConfigureActionCatalog(
            WeaponItemBasedAction lightAttack,
            WeaponItemBasedAction heavyAttack,
            WeaponItemBasedAction chargedAttack)
        {
            ExecuteWithScene(k_MainMenuScenePath, scene =>
            {
                WorldActionManager actionManager =
                    FindComponentInScene<WorldActionManager>(scene) ??
                    throw new InvalidOperationException(
                        "The Main Menu scene needs a WorldActionManager.");
                SerializedObject serializedManager = new SerializedObject(actionManager);
                SerializedProperty weaponActions = GetRequiredProperty(
                    serializedManager,
                    "m_weaponActions");
                weaponActions.arraySize = 3;
                weaponActions.GetArrayElementAtIndex(0).objectReferenceValue = lightAttack;
                weaponActions.GetArrayElementAtIndex(1).objectReferenceValue = heavyAttack;
                weaponActions.GetArrayElementAtIndex(2).objectReferenceValue = chargedAttack;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(actionManager);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            });
        }

        private static void ConfigureAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            if (!controller.parameters.Any(
                    parameter => parameter.name == k_ChargingParameterName))
            {
                controller.AddParameter(
                    k_ChargingParameterName,
                    AnimatorControllerParameterType.Bool);
            }

            AnimatorStateMachine stateMachine = GetRequiredLayer(controller).stateMachine;
            AnimatorState emptyState = GetRequiredState(stateMachine, k_EmptyStateName);
            AnimatorState chargeState = GetOrCreateState(
                stateMachine,
                k_ChargeStateName,
                new Vector3(750f, 550f, 0f));
            AnimatorState holdState = GetOrCreateState(
                stateMachine,
                k_HoldStateName,
                new Vector3(980f, 550f, 0f));
            AnimatorState heavyAttackState = GetRequiredState(
                stateMachine,
                k_HeavyAttackStateName);
            AnimatorState chargedAttackState = GetOrCreateState(
                stateMachine,
                k_ChargedAttackStateName,
                new Vector3(1210f, 550f, 0f));

            chargeState.motion = LoadRequiredAsset<AnimationClip>(k_ChargeClipPath);
            holdState.motion = LoadRequiredAsset<AnimationClip>(k_HoldClipPath);
            heavyAttackState.motion = LoadRequiredAsset<AnimationClip>(
                k_HeavyReleaseClipPath);
            chargedAttackState.motion = LoadRequiredAsset<AnimationClip>(
                k_ChargedReleaseClipPath);
            ConfigureConditionalTransition(
                emptyState,
                chargeState,
                AnimatorConditionMode.If);
            ConfigureExitTransition(chargeState, holdState, 0.9f, 0.05f);
            ConfigureConditionalTransition(
                chargeState,
                emptyState,
                AnimatorConditionMode.IfNot);
            ConfigureConditionalTransition(
                holdState,
                emptyState,
                AnimatorConditionMode.IfNot);
            ConfigureExitTransition(heavyAttackState, emptyState, 0.9f, 0.25f);
            ConfigureExitTransition(chargedAttackState, emptyState, 0.9f, 0.25f);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureConditionalTransition(
            AnimatorState sourceState,
            AnimatorState destinationState,
            AnimatorConditionMode conditionMode)
        {
            AnimatorStateTransition transition = GetOrCreateTransition(
                sourceState,
                destinationState,
                conditionMode);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.1f;
            transition.conditions = Array.Empty<AnimatorCondition>();
            transition.AddCondition(
                conditionMode,
                0f,
                k_ChargingParameterName);
        }

        private static void ConfigureExitTransition(
            AnimatorState sourceState,
            AnimatorState destinationState,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition transition = GetOrCreateTransition(
                sourceState,
                destinationState,
                null);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.conditions = Array.Empty<AnimatorCondition>();
        }

        private static AnimatorStateTransition GetOrCreateTransition(
            AnimatorState sourceState,
            AnimatorState destinationState,
            AnimatorConditionMode? conditionMode)
        {
            foreach (AnimatorStateTransition transition in sourceState.transitions)
            {
                if (transition.destinationState != destinationState)
                {
                    continue;
                }

                if (!conditionMode.HasValue ||
                    transition.conditions.Any(condition =>
                        condition.parameter == k_ChargingParameterName &&
                        condition.mode == conditionMode.Value))
                {
                    return transition;
                }
            }

            return sourceState.AddTransition(destinationState);
        }

        private static void ValidateActionAssets(
            WeaponItemBasedAction lightAttack,
            WeaponItemBasedAction heavyAttack,
            WeaponItemBasedAction chargedAttack)
        {
            if (lightAttack.ActionID != 0 ||
                heavyAttack.ActionID != 1 ||
                chargedAttack.ActionID != 2)
            {
                throw new InvalidOperationException(
                    "Light, heavy, and charged actions need stable IDs 0, 1, and 2.");
            }
        }

        private static void ValidateWeaponItems(
            ChargedAttackWeaponItemAction chargedAttack)
        {
            foreach (string weaponPath in s_weaponPaths)
            {
                MeleeWeaponItem weapon = LoadRequiredAsset<MeleeWeaponItem>(weaponPath);
                if (weapon.RightHandChargedAction != chargedAttack ||
                    !Mathf.Approximately(
                        weapon.GetAttackDamageModifier(AttackType.HeavyAttack01),
                        k_HeavyDamageModifier) ||
                    !Mathf.Approximately(
                        weapon.GetAttackDamageModifier(AttackType.ChargedAttack01),
                        k_ChargedDamageModifier) ||
                    !Mathf.Approximately(
                        weapon.GetStaminaCostMultiplier(AttackType.HeavyAttack01),
                        k_HeavyStaminaModifier) ||
                    !Mathf.Approximately(
                        weapon.GetStaminaCostMultiplier(AttackType.ChargedAttack01),
                        k_ChargedStaminaModifier))
                {
                    throw new InvalidOperationException(
                        $"Weapon {weapon.name} is missing charged attack data.");
                }
            }
        }

        private static void ValidateActionCatalog(
            WeaponItemBasedAction lightAttack,
            WeaponItemBasedAction heavyAttack,
            WeaponItemBasedAction chargedAttack)
        {
            ExecuteWithScene(k_MainMenuScenePath, scene =>
            {
                WorldActionManager actionManager =
                    FindComponentInScene<WorldActionManager>(scene);
                if (actionManager == null ||
                    actionManager.WeaponActions.Count != 3 ||
                    actionManager.WeaponActions[0] != lightAttack ||
                    actionManager.WeaponActions[1] != heavyAttack ||
                    actionManager.WeaponActions[2] != chargedAttack)
                {
                    throw new InvalidOperationException(
                        "WorldActionManager needs ordered light, heavy, and charged actions.");
                }
            });
        }

        private static void ValidateAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_ControllerPath);
            AnimatorControllerParameter chargingParameter = controller.parameters
                .FirstOrDefault(parameter => parameter.name == k_ChargingParameterName);
            AnimatorStateMachine stateMachine = GetRequiredLayer(controller).stateMachine;
            AnimatorState emptyState = GetRequiredState(stateMachine, k_EmptyStateName);
            AnimatorState chargeState = GetRequiredState(stateMachine, k_ChargeStateName);
            AnimatorState holdState = GetRequiredState(stateMachine, k_HoldStateName);
            AnimatorState heavyAttackState = GetRequiredState(
                stateMachine,
                k_HeavyAttackStateName);
            AnimatorState chargedAttackState = GetRequiredState(
                stateMachine,
                k_ChargedAttackStateName);
            if (chargingParameter == null ||
                chargingParameter.type != AnimatorControllerParameterType.Bool ||
                chargeState.motion != LoadRequiredAsset<AnimationClip>(k_ChargeClipPath) ||
                holdState.motion != LoadRequiredAsset<AnimationClip>(k_HoldClipPath) ||
                heavyAttackState.motion !=
                    LoadRequiredAsset<AnimationClip>(k_HeavyReleaseClipPath) ||
                chargedAttackState.motion !=
                    LoadRequiredAsset<AnimationClip>(k_ChargedReleaseClipPath) ||
                !HasConditionalTransition(
                    emptyState,
                    chargeState,
                    AnimatorConditionMode.If) ||
                !HasExitTransition(chargeState, holdState) ||
                !HasConditionalTransition(
                    holdState,
                    emptyState,
                    AnimatorConditionMode.IfNot) ||
                !HasExitTransition(heavyAttackState, emptyState) ||
                !HasExitTransition(chargedAttackState, emptyState))
            {
                throw new InvalidOperationException(
                    "Charged attack Animator parameters, clips, or transitions are invalid.");
            }
        }

        private static void ValidateNetworkContract()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                CharacterNetworkManager networkManager =
                    playerRoot.GetComponent<CharacterNetworkManager>();
                if (networkManager == null ||
                    networkManager.IsChargingAttack.ReadPerm !=
                        Unity.Netcode.NetworkVariableReadPermission.Everyone ||
                    networkManager.IsChargingAttack.WritePerm !=
                        Unity.Netcode.NetworkVariableWritePermission.Owner ||
                    networkManager.IsChargingAttack.Value)
                {
                    throw new InvalidOperationException(
                        "Charge state must be false by default, owner-written, and public.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateInputContract()
        {
            InputActionAsset playerControls = LoadRequiredAsset<InputActionAsset>(
                k_PlayerControlsPath);
            InputAction rightTrigger = playerControls.FindActionMap(
                "Player Movement",
                true).FindAction("RT", true);
            bool hasGamepadTrigger = rightTrigger.bindings.Any(
                binding => binding.path == "<Gamepad>/rightTrigger");
            bool hasMouseButton = rightTrigger.bindings.Any(
                binding => binding.path == "<Mouse>/rightButton");
            if (rightTrigger.type != InputActionType.Button ||
                !hasGamepadTrigger ||
                !hasMouseButton)
            {
                throw new InvalidOperationException(
                    "RT must remain a Button with gamepad and mouse press/release phases.");
            }
        }

        private static void ValidateChargeThreshold()
        {
            MethodInfo thresholdMethod = typeof(PlayerCombatManager).GetMethod(
                "ShouldUseChargedAttack",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (thresholdMethod == null ||
                (bool)thresholdMethod.Invoke(null, new object[] { 0.79f, 0.8f }) ||
                !(bool)thresholdMethod.Invoke(null, new object[] { 0.8f, 0.8f }))
            {
                throw new InvalidOperationException(
                    "Charge duration must select heavy below the threshold and charged at it.");
            }
        }

        private static bool HasConditionalTransition(
            AnimatorState sourceState,
            AnimatorState destinationState,
            AnimatorConditionMode conditionMode)
        {
            return sourceState.transitions.Any(transition =>
                transition.destinationState == destinationState &&
                !transition.hasExitTime &&
                transition.conditions.Any(condition =>
                    condition.parameter == k_ChargingParameterName &&
                    condition.mode == conditionMode));
        }

        private static bool HasExitTransition(
            AnimatorState sourceState,
            AnimatorState destinationState)
        {
            return sourceState.transitions.Any(transition =>
                transition.destinationState == destinationState &&
                transition.hasExitTime);
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

        private static void ExecuteWithScene(string scenePath, Action<Scene> action)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                action(scene);
            }
            finally
            {
                if (!wasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
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

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
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
    }
}
