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
    /// <summary>Configures and validates the complete EP57-58 blocking system.</summary>
    public static class CompleteBlockingSystemSetup
    {
        private const int k_ShieldItemID = 3;
        private const string k_ControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_DatabasePrefabPath =
            "Assets/Data/Prefabs/Word Managers/World Item Database.prefab";
        private const string k_InputActionsPath = "Assets/PlayerControls.inputactions";
        private const string k_ActionPath = "Assets/Data/Actions/Off Hand Block.asset";
        private const string k_UnarmedPath =
            "Assets/Data/Items/Weapons/Melee Weapons/Unarmed.asset";
        private const string k_StraightSwordPath =
            "Assets/Data/Items/Weapons/Melee Weapons/Straight Sword.asset";
        private const string k_BroadswordPath =
            "Assets/Data/Items/Weapons/Melee Weapons/Broadsword.asset";
        private const string k_ShieldPath =
            "Assets/Data/Items/Weapons/Melee Weapons/Medium Shield.asset";
        private const string k_ShieldPrefabPath =
            "Assets/Data/Prefabs/Weapons/Melee Weapons/Medium Shield.prefab";
        private const string k_ShieldControllerPath =
            "Assets/Data/Animator Overrides/Weapons/Medium Shield Animator.overrideController";
        private const string k_ShieldModelPath =
            "Assets/Art/Models/Equipment/Weapons/Shield/SM_Wep_Shield_02.obj";
        private const string k_ShieldMaterialPath =
            "Assets/Art/Materials/Equipment/Weapons/Shield/Iron_Shield_Material_01.mat";
        private const string k_DamageLayerName = "Damage Collider";
        private const string k_BaseLayerName = "Base Layer";
        private const string k_ActionLayerName = "Action Override";
        private const string k_BaseLocomotionStateName = "Locomotion One Handed";
        private const string k_BlockingIdleOneHandedStateName =
            "Blocking Idle One Handed";
        private const string k_BlockingLocomotionOneHandedStateName =
            "Blocking Locomotion One Handed";
        private const string k_BlockingIdleTwoHandedStateName =
            "Blocking Idle Two Handed";
        private const string k_BlockingLocomotionTwoHandedStateName =
            "Blocking Locomotion Two Handed";
        private const string k_GuardBreakStateName = "Guard_Break_01";
        private const string k_IsBlockingParameter = "isBlocking";
        private const string k_IsMovingParameter = "isMoving";
        private const string k_HorizontalParameter = "Horizontal";
        private const string k_VerticalParameter = "Vertical";
        private const string k_EmptyStateName = "Empty";
        private const string k_LocomotionFolder =
            "Assets/Art/Animations/Characters/Humanoid/Locomotion/";
        private const string k_GuardBreakClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Actions/" +
            "shield_off_guard_break_01.anim";

        private static readonly string[] s_blockSoundPaths =
        {
            "Assets/Art/Audio/SFX/Combat/SFX_Metal_Shield_Medium_Impact_01.wav",
            "Assets/Art/Audio/SFX/Combat/SFX_Metal_Shield_Medium_Impact_02.wav",
            "Assets/Art/Audio/SFX/Combat/SFX_Metal_Shield_Medium_Impact_03.wav"
        };

        private static readonly WeaponBlockingDefinition[] s_weaponDefinitions =
        {
            new WeaponBlockingDefinition(
                k_UnarmedPath,
                WeaponModelType.Weapon,
                35f,
                15f,
                15f,
                10f,
                15f,
                15f),
            new WeaponBlockingDefinition(
                k_StraightSwordPath,
                WeaponModelType.Weapon,
                70f,
                30f,
                25f,
                20f,
                25f,
                35f),
            new WeaponBlockingDefinition(
                k_BroadswordPath,
                WeaponModelType.Weapon,
                75f,
                30f,
                25f,
                20f,
                25f,
                40f),
            new WeaponBlockingDefinition(
                k_ShieldPath,
                WeaponModelType.Shield,
                100f,
                60f,
                55f,
                45f,
                55f,
                65f)
        };

        [MenuItem("Tools/Elden/Configure Complete Blocking System")]
        public static void ConfigureCompleteBlockingSystem()
        {
            ConfigureInputAction();
            OffHandMeleeAction blockingAction = ConfigureBlockingAction();
            AnimatorOverrideController shieldController = ConfigureShieldController();
            GameObject shieldPrefab = ConfigureShieldPrefab();
            WeaponItem shield = ConfigureShieldItem(
                shieldPrefab,
                shieldController,
                blockingAction);
            ConfigureWeaponBlockingData(blockingAction);
            ConfigureWorldItemDatabase(shield);
            ConfigurePlayerPrefab(shield);
            ConfigureAnimatorController();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                k_InputActionsPath,
                ImportAssetOptions.ForceSynchronousImport);
            ValidateCompleteBlockingSystem();
            Debug.Log(
                "[CompleteBlockingSystemSetup] Configured LB hold blocking, directional " +
                "locomotion, shield slots, weapon defense, guard stamina, and Guard Break.");
        }

        [MenuItem("Tools/Elden/Validate Complete Blocking System")]
        public static void ValidateCompleteBlockingSystem()
        {
            ValidateInputAction();
            ValidateAnimatorController();
            ValidateWeaponData();
            ValidateShieldPrefab();
            ValidatePlayerPrefab();
            ValidateWorldItemDatabase();
            ValidateNetworkContract();
            ValidateRuntimeArchitecture();
            ValidateStaminaFormula();
            Debug.Log(
                "[CompleteBlockingSystemValidation] Animator, input, late join, data, " +
                "slot routing, owner stamina, audio, and Guard Break are valid.");
        }

        private static void ConfigureInputAction()
        {
            InputActionAsset inputActions =
                LoadRequiredAsset<InputActionAsset>(k_InputActionsPath);
            InputActionMap movementMap = inputActions.FindActionMap(
                "Player Movement",
                true);
            InputAction blockAction = movementMap.FindAction("LB");
            if (blockAction == null)
            {
                blockAction = movementMap.AddAction(
                    "LB",
                    InputActionType.Button,
                    expectedControlLayout: "Button");
            }

            EnsureBinding(blockAction, "<Gamepad>/leftShoulder", "Gamepad");
            EnsureBinding(blockAction, "<Keyboard>/leftCtrl", "Keyboard&Mouse");
            EditorUtility.SetDirty(inputActions);
        }

        private static OffHandMeleeAction ConfigureBlockingAction()
        {
            OffHandMeleeAction action =
                AssetDatabase.LoadAssetAtPath<OffHandMeleeAction>(k_ActionPath);
            if (action == null)
            {
                action = ScriptableObject.CreateInstance<OffHandMeleeAction>();
                AssetDatabase.CreateAsset(action, k_ActionPath);
            }

            SerializedObject serializedAction = new SerializedObject(action);
            GetRequiredProperty(serializedAction, "m_actionID").intValue = 3;
            serializedAction.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
            return action;
        }

        private static AnimatorOverrideController ConfigureShieldController()
        {
            RuntimeAnimatorController baseController =
                LoadRequiredAsset<RuntimeAnimatorController>(k_ControllerPath);
            AnimatorOverrideController shieldController =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                    k_ShieldControllerPath);
            if (shieldController == null)
            {
                shieldController = new AnimatorOverrideController(baseController);
                AssetDatabase.CreateAsset(shieldController, k_ShieldControllerPath);
            }
            else
            {
                shieldController.runtimeAnimatorController = baseController;
            }

            EditorUtility.SetDirty(shieldController);
            return shieldController;
        }

        private static GameObject ConfigureShieldPrefab()
        {
            GameObject shieldRoot = new GameObject("Medium Shield");
            try
            {
                WeaponManager weaponManager = shieldRoot.AddComponent<WeaponManager>();
                GameObject pivotObject = new GameObject("Weapon Pivot");
                pivotObject.transform.SetParent(shieldRoot.transform, false);
                GameObject sourceModel = LoadRequiredAsset<GameObject>(k_ShieldModelPath);
                GameObject model = UnityEngine.Object.Instantiate(
                    sourceModel,
                    pivotObject.transform);
                model.name = "Weapon Mesh";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                Material material = LoadRequiredAsset<Material>(k_ShieldMaterialPath);
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterial = material;
                }

                Bounds bounds = CalculateLocalBounds(pivotObject.transform, model);
                GameObject colliderObject = new GameObject("Damage Collider");
                colliderObject.layer = LayerMask.NameToLayer(k_DamageLayerName);
                colliderObject.transform.SetParent(pivotObject.transform, false);
                BoxCollider boxCollider = colliderObject.AddComponent<BoxCollider>();
                boxCollider.isTrigger = true;
                boxCollider.enabled = false;
                boxCollider.center = bounds.center;
                boxCollider.size = bounds.size;
                MeleeWeaponDamageCollider damageCollider =
                    colliderObject.AddComponent<MeleeWeaponDamageCollider>();
                SetObjectReference(
                    weaponManager,
                    "m_meleeDamageCollider",
                    damageCollider);

                GameObject shieldPrefab = PrefabUtility.SaveAsPrefabAsset(
                    shieldRoot,
                    k_ShieldPrefabPath);
                return shieldPrefab != null
                    ? shieldPrefab
                    : throw new InvalidOperationException(
                        "Could not save the Medium Shield prefab.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shieldRoot);
            }
        }

        private static WeaponItem ConfigureShieldItem(
            GameObject shieldPrefab,
            AnimatorOverrideController shieldController,
            OffHandMeleeAction blockingAction)
        {
            MeleeWeaponItem shield =
                AssetDatabase.LoadAssetAtPath<MeleeWeaponItem>(k_ShieldPath);
            if (shield == null)
            {
                shield = ScriptableObject.CreateInstance<MeleeWeaponItem>();
                AssetDatabase.CreateAsset(shield, k_ShieldPath);
            }

            SerializedObject serializedShield = new SerializedObject(shield);
            SetString(serializedShield, "m_itemName", "Medium Shield");
            SetString(
                serializedShield,
                "m_itemDescription",
                "A balanced shield with full Physical guard and dependable Stability.");
            SetInt(serializedShield, "m_itemID", k_ShieldItemID);
            SetObjectReference(serializedShield, "m_weaponModel", shieldPrefab);
            SetBool(serializedShield, "m_isUnarmed", false);
            SetEnum(
                serializedShield,
                "m_weaponModelType",
                (int)WeaponModelType.Shield);
            SetObjectReference(
                serializedShield,
                "m_weaponAnimator",
                shieldController);
            SetVector3(serializedShield, "m_weaponPivotPosition", Vector3.zero);
            SetVector3(serializedShield, "m_weaponPivotRotation", Vector3.zero);
            SetVector3(serializedShield, "m_weaponPivotScale", Vector3.one);
            SetFloat(serializedShield, "m_physicalDamage", 5f);
            SetFloat(serializedShield, "m_baseStaminaCost", 10f);
            SetFloat(serializedShield, "m_basePoiseDamage", 10f);
            SetObjectReference(
                serializedShield,
                "m_leftHandAction",
                blockingAction);
            serializedShield.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shield);
            return shield;
        }

        private static void ConfigureWeaponBlockingData(
            OffHandMeleeAction blockingAction)
        {
            AudioClip[] blockSounds = s_blockSoundPaths
                .Select(LoadRequiredAsset<AudioClip>)
                .ToArray();
            foreach (WeaponBlockingDefinition definition in s_weaponDefinitions)
            {
                WeaponItem weapon = LoadRequiredAsset<WeaponItem>(definition.Path);
                SerializedObject serializedWeapon = new SerializedObject(weapon);
                SetEnum(
                    serializedWeapon,
                    "m_weaponModelType",
                    (int)definition.ModelType);
                SetFloat(
                    serializedWeapon,
                    "m_blockingPhysicalAbsorption",
                    definition.PhysicalAbsorption);
                SetFloat(
                    serializedWeapon,
                    "m_blockingMagicAbsorption",
                    definition.MagicAbsorption);
                SetFloat(
                    serializedWeapon,
                    "m_blockingFireAbsorption",
                    definition.FireAbsorption);
                SetFloat(
                    serializedWeapon,
                    "m_blockingLightningAbsorption",
                    definition.LightningAbsorption);
                SetFloat(
                    serializedWeapon,
                    "m_blockingHolyAbsorption",
                    definition.HolyAbsorption);
                SetFloat(
                    serializedWeapon,
                    "m_blockingStability",
                    definition.Stability);
                SetObjectReference(
                    serializedWeapon,
                    "m_leftHandAction",
                    blockingAction);
                SetObjectArray(
                    serializedWeapon,
                    "m_blockingSoundEffects",
                    blockSounds);
                serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
            }
        }

        private static void ConfigureWorldItemDatabase(WeaponItem shield)
        {
            GameObject databaseRoot = PrefabUtility.LoadPrefabContents(
                k_DatabasePrefabPath);
            try
            {
                WorldItemDatabase database =
                    GetRequiredComponent<WorldItemDatabase>(databaseRoot);
                SerializedObject serializedDatabase = new SerializedObject(database);
                SerializedProperty currentItems = GetRequiredProperty(
                    serializedDatabase,
                    "m_items");
                List<UnityEngine.Object> items = new List<UnityEngine.Object>
                {
                    LoadRequiredAsset<WeaponItem>(k_UnarmedPath),
                    LoadRequiredAsset<WeaponItem>(k_StraightSwordPath),
                    LoadRequiredAsset<WeaponItem>(k_BroadswordPath),
                    shield
                };
                for (int itemIndex = 4; itemIndex < currentItems.arraySize; itemIndex++)
                {
                    items.Add(
                        currentItems.GetArrayElementAtIndex(itemIndex).objectReferenceValue);
                }

                SetObjectArray(
                    serializedDatabase,
                    "m_items",
                    items.ToArray());
                if (PrefabUtility.SaveAsPrefabAsset(
                        databaseRoot,
                        k_DatabasePrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the World Item Database prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(databaseRoot);
            }
        }

        private static void ConfigurePlayerPrefab(WeaponItem shield)
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerInventoryManager inventory =
                    GetRequiredComponent<PlayerInventoryManager>(playerRoot);
                SetObjectArray(
                    new SerializedObject(inventory),
                    "m_weaponsInLeftHandSlots",
                    new UnityEngine.Object[]
                    {
                        shield,
                        LoadRequiredAsset<WeaponItem>(k_BroadswordPath),
                        LoadRequiredAsset<WeaponItem>(k_UnarmedPath)
                    });
                PlayerCombatManager combatManager =
                    GetRequiredComponent<PlayerCombatManager>(playerRoot);
                SetBool(new SerializedObject(combatManager), "m_canBlock", true);
                CharacterStatsManager statsManager =
                    GetRequiredComponent<CharacterStatsManager>(playerRoot);
                SerializedObject serializedStats = new SerializedObject(statsManager);
                SetFloat(serializedStats, "m_blockingStability", 50f);
                serializedStats.ApplyModifiedPropertiesWithoutUndo();

                Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman)
                {
                    throw new InvalidOperationException(
                        "Player prefab needs a Humanoid Animator for shield slots.");
                }

                ConfigureWeaponSlot(
                    animator.GetBoneTransform(HumanBodyBones.LeftHand),
                    "Left Hand Shield Slot",
                    WeaponModelSlot.LeftHandShieldSlot);
                if (PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the Player blocking configuration.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigureWeaponSlot(
            Transform hand,
            string slotName,
            WeaponModelSlot slotType)
        {
            if (hand == null)
            {
                throw new InvalidOperationException(
                    $"Player avatar is missing the {slotType} bone.");
            }

            Transform slotTransform = hand.Find(slotName);
            if (slotTransform == null)
            {
                GameObject slotObject = new GameObject(slotName);
                slotTransform = slotObject.transform;
                slotTransform.SetParent(hand, false);
            }

            slotTransform.localPosition = Vector3.zero;
            slotTransform.localRotation = Quaternion.identity;
            slotTransform.localScale = Vector3.one;
            WeaponModelInstantiationSlot slot =
                GetOrAddComponent<WeaponModelInstantiationSlot>(
                    slotTransform.gameObject);
            SerializedObject serializedSlot = new SerializedObject(slot);
            SetEnum(serializedSlot, "m_weaponModelSlot", (int)slotType);
            serializedSlot.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAnimatorController()
        {
            AnimatorController controller =
                LoadRequiredAsset<AnimatorController>(k_ControllerPath);
            EnsureParameter(controller, k_IsBlockingParameter, AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, k_IsMovingParameter, AnimatorControllerParameterType.Bool);
            AnimatorStateMachine baseStateMachine = controller.layers
                .First(layer => layer.name == k_BaseLayerName)
                .stateMachine;
            AnimatorState baseLocomotion = GetRequiredState(
                baseStateMachine,
                k_BaseLocomotionStateName);
            AnimatorState oneHandedIdle = GetOrCreateState(
                baseStateMachine,
                k_BlockingIdleOneHandedStateName,
                new Vector3(540f, 150f, 0f));
            AnimatorState oneHandedLocomotion = GetOrCreateState(
                baseStateMachine,
                k_BlockingLocomotionOneHandedStateName,
                new Vector3(770f, 150f, 0f));
            AnimatorState twoHandedIdle = GetOrCreateState(
                baseStateMachine,
                k_BlockingIdleTwoHandedStateName,
                new Vector3(540f, 300f, 0f));
            AnimatorState twoHandedLocomotion = GetOrCreateState(
                baseStateMachine,
                k_BlockingLocomotionTwoHandedStateName,
                new Vector3(770f, 300f, 0f));

            oneHandedIdle.motion = LoadRequiredAsset<AnimationClip>(
                k_LocomotionFolder + "core_off_guard_idle_01.anim");
            oneHandedLocomotion.motion = ConfigureDirectionalBlendTree(
                controller,
                oneHandedLocomotion,
                "Blocking One Handed Blend Tree",
                "core_off_walk_guard_");
            twoHandedIdle.motion = LoadRequiredAsset<AnimationClip>(
                k_LocomotionFolder + "core_th_guard_idle_01.anim");
            twoHandedLocomotion.motion = ConfigureDirectionalBlendTree(
                controller,
                twoHandedLocomotion,
                "Blocking Two Handed Blend Tree",
                "core_th_walk_guard_");
            EnsureBlockingBehaviour(oneHandedIdle);
            EnsureBlockingBehaviour(oneHandedLocomotion);
            EnsureBlockingBehaviour(twoHandedIdle);
            EnsureBlockingBehaviour(twoHandedLocomotion);
            ConfigureBoolTransition(
                baseLocomotion,
                oneHandedIdle,
                k_IsBlockingParameter,
                true);
            ConfigureBoolTransition(
                oneHandedIdle,
                baseLocomotion,
                k_IsBlockingParameter,
                false);
            ConfigureBoolTransition(
                oneHandedLocomotion,
                baseLocomotion,
                k_IsBlockingParameter,
                false);
            ConfigureBoolTransition(
                oneHandedIdle,
                oneHandedLocomotion,
                k_IsMovingParameter,
                true);
            ConfigureBoolTransition(
                oneHandedLocomotion,
                oneHandedIdle,
                k_IsMovingParameter,
                false);

            AnimatorStateMachine actionStateMachine = controller.layers
                .First(layer => layer.name == k_ActionLayerName)
                .stateMachine;
            AnimatorState emptyState = GetRequiredState(
                actionStateMachine,
                k_EmptyStateName);
            AnimatorState guardBreakState = GetOrCreateState(
                actionStateMachine,
                k_GuardBreakStateName,
                new Vector3(1490f, 640f, 0f));
            guardBreakState.motion = LoadRequiredAsset<AnimationClip>(
                k_GuardBreakClipPath);
            ConfigureExitTransition(guardBreakState, emptyState);
            EditorUtility.SetDirty(controller);
        }

        private static BlendTree ConfigureDirectionalBlendTree(
            AnimatorController controller,
            AnimatorState state,
            string blendTreeName,
            string clipPrefix)
        {
            BlendTree blendTree = state.motion as BlendTree;
            if (blendTree == null)
            {
                blendTree = new BlendTree { name = blendTreeName };
                AssetDatabase.AddObjectToAsset(blendTree, controller);
            }

            blendTree.blendType = BlendTreeType.FreeformCartesian2D;
            blendTree.blendParameter = k_HorizontalParameter;
            blendTree.blendParameterY = k_VerticalParameter;
            blendTree.useAutomaticThresholds = false;
            blendTree.children = new[]
            {
                CreateChildMotion(clipPrefix + "F_01.anim", new Vector2(0f, 1f)),
                CreateChildMotion(clipPrefix + "B_01.anim", new Vector2(0f, -1f)),
                CreateChildMotion(clipPrefix + "L_01.anim", new Vector2(-1f, 0f)),
                CreateChildMotion(clipPrefix + "R_01.anim", new Vector2(1f, 0f))
            };
            EditorUtility.SetDirty(blendTree);
            return blendTree;
        }

        private static ChildMotion CreateChildMotion(
            string clipName,
            Vector2 position)
        {
            return new ChildMotion
            {
                motion = LoadRequiredAsset<AnimationClip>(
                    k_LocomotionFolder + clipName),
                position = position,
                timeScale = 1f
            };
        }

        private static void ConfigureBoolTransition(
            AnimatorState source,
            AnimatorState destination,
            string parameter,
            bool expectedValue)
        {
            AnimatorStateTransition transition = source.transitions
                .FirstOrDefault(candidate =>
                    candidate.destinationState == destination &&
                    candidate.conditions.Any(condition =>
                        condition.parameter == parameter)) ??
                source.AddTransition(destination);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.15f;
            transition.canTransitionToSelf = false;
            transition.conditions = Array.Empty<AnimatorCondition>();
            transition.AddCondition(
                expectedValue
                    ? AnimatorConditionMode.If
                    : AnimatorConditionMode.IfNot,
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

        private static void EnsureBlockingBehaviour(AnimatorState state)
        {
            if (!state.behaviours.Any(behaviour =>
                    behaviour is ToggleBlockingController))
            {
                state.AddStateMachineBehaviour<ToggleBlockingController>();
            }
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            if (!controller.parameters.Any(parameter =>
                    parameter.name == parameterName))
            {
                controller.AddParameter(parameterName, parameterType);
            }
        }

        private static void ValidateInputAction()
        {
            InputActionAsset inputActions =
                LoadRequiredAsset<InputActionAsset>(k_InputActionsPath);
            InputAction action = inputActions.FindActionMap("Player Movement", true)
                .FindAction("LB", true);
            if (action.type != InputActionType.Button ||
                !HasBinding(action, "<Gamepad>/leftShoulder") ||
                !HasBinding(action, "<Keyboard>/leftCtrl"))
            {
                throw new InvalidOperationException(
                    "LB blocking needs hold-capable gamepad and keyboard bindings.");
            }
        }

        private static void ValidateAnimatorController()
        {
            AnimatorController controller =
                LoadRequiredAsset<AnimatorController>(k_ControllerPath);
            if (!HasBoolParameter(controller, k_IsBlockingParameter) ||
                !HasBoolParameter(controller, k_IsMovingParameter))
            {
                throw new InvalidOperationException(
                    "Animator is missing blocking conditions.");
            }

            AnimatorStateMachine baseStateMachine = controller.layers
                .First(layer => layer.name == k_BaseLayerName)
                .stateMachine;
            AnimatorState baseLocomotion = GetRequiredState(
                baseStateMachine,
                k_BaseLocomotionStateName);
            AnimatorState oneHandedIdle = GetRequiredState(
                baseStateMachine,
                k_BlockingIdleOneHandedStateName);
            AnimatorState oneHandedLocomotion = GetRequiredState(
                baseStateMachine,
                k_BlockingLocomotionOneHandedStateName);
            AnimatorState twoHandedIdle = GetRequiredState(
                baseStateMachine,
                k_BlockingIdleTwoHandedStateName);
            AnimatorState twoHandedLocomotion = GetRequiredState(
                baseStateMachine,
                k_BlockingLocomotionTwoHandedStateName);
            ValidateBlockingState(oneHandedIdle, false);
            ValidateBlockingState(oneHandedLocomotion, true);
            ValidateBlockingState(twoHandedIdle, false);
            ValidateBlockingState(twoHandedLocomotion, true);
            ValidateBoolTransition(
                baseLocomotion,
                oneHandedIdle,
                k_IsBlockingParameter,
                true);
            ValidateBoolTransition(
                oneHandedIdle,
                baseLocomotion,
                k_IsBlockingParameter,
                false);
            ValidateBoolTransition(
                oneHandedLocomotion,
                baseLocomotion,
                k_IsBlockingParameter,
                false);
            ValidateBoolTransition(
                oneHandedIdle,
                oneHandedLocomotion,
                k_IsMovingParameter,
                true);
            ValidateBoolTransition(
                oneHandedLocomotion,
                oneHandedIdle,
                k_IsMovingParameter,
                false);

            AnimatorStateMachine actionStateMachine = controller.layers
                .First(layer => layer.name == k_ActionLayerName)
                .stateMachine;
            AnimatorState guardBreakState = GetRequiredState(
                actionStateMachine,
                k_GuardBreakStateName);
            AnimatorState emptyState = GetRequiredState(
                actionStateMachine,
                k_EmptyStateName);
            if (guardBreakState.motion !=
                    LoadRequiredAsset<AnimationClip>(k_GuardBreakClipPath) ||
                !guardBreakState.transitions.Any(transition =>
                    transition.destinationState == emptyState &&
                    transition.hasExitTime))
            {
                throw new InvalidOperationException(
                    "Guard Break needs its authored action and Empty return.");
            }
        }

        private static void ValidateBlockingState(
            AnimatorState state,
            bool expectsBlendTree)
        {
            if (!state.behaviours.Any(behaviour =>
                    behaviour is ToggleBlockingController))
            {
                throw new InvalidOperationException(
                    $"{state.name} is missing ToggleBlockingController.");
            }

            if (!expectsBlendTree)
            {
                if (state.motion == null)
                {
                    throw new InvalidOperationException(
                        $"{state.name} is missing its idle clip.");
                }

                return;
            }

            if (state.motion is not BlendTree blendTree ||
                blendTree.blendType != BlendTreeType.FreeformCartesian2D ||
                blendTree.blendParameter != k_HorizontalParameter ||
                blendTree.blendParameterY != k_VerticalParameter ||
                blendTree.children.Length != 4)
            {
                throw new InvalidOperationException(
                    $"{state.name} needs a four-way directional Blend Tree.");
            }
        }

        private static void ValidateBoolTransition(
            AnimatorState source,
            AnimatorState destination,
            string parameter,
            bool expectedValue)
        {
            AnimatorConditionMode expectedMode = expectedValue
                ? AnimatorConditionMode.If
                : AnimatorConditionMode.IfNot;
            if (!source.transitions.Any(transition =>
                    transition.destinationState == destination &&
                    !transition.hasExitTime &&
                    transition.conditions.Length == 1 &&
                    transition.conditions[0].parameter == parameter &&
                    transition.conditions[0].mode == expectedMode))
            {
                throw new InvalidOperationException(
                    $"{source.name} has an invalid transition to {destination.name}.");
            }
        }

        private static void ValidateWeaponData()
        {
            OffHandMeleeAction action =
                LoadRequiredAsset<OffHandMeleeAction>(k_ActionPath);
            foreach (WeaponBlockingDefinition definition in s_weaponDefinitions)
            {
                WeaponItem weapon = LoadRequiredAsset<WeaponItem>(definition.Path);
                if (weapon.LeftHandAction != action ||
                    weapon.WeaponModelType != definition.ModelType ||
                    !Mathf.Approximately(
                        weapon.BlockingPhysicalAbsorption,
                        definition.PhysicalAbsorption) ||
                    !Mathf.Approximately(
                        weapon.BlockingMagicAbsorption,
                        definition.MagicAbsorption) ||
                    !Mathf.Approximately(
                        weapon.BlockingFireAbsorption,
                        definition.FireAbsorption) ||
                    !Mathf.Approximately(
                        weapon.BlockingLightningAbsorption,
                        definition.LightningAbsorption) ||
                    !Mathf.Approximately(
                        weapon.BlockingHolyAbsorption,
                        definition.HolyAbsorption) ||
                    !Mathf.Approximately(
                        weapon.BlockingStability,
                        definition.Stability) ||
                    weapon.BlockingSoundEffects.Length != s_blockSoundPaths.Length ||
                    weapon.BlockingSoundEffects.Any(sound => sound == null))
                {
                    throw new InvalidOperationException(
                        $"{weapon.name} has incomplete blocking data.");
                }
            }

            WeaponItem shield = LoadRequiredAsset<WeaponItem>(k_ShieldPath);
            if (shield.ItemID != k_ShieldItemID ||
                shield.WeaponAnimator !=
                    LoadRequiredAsset<AnimatorOverrideController>(
                        k_ShieldControllerPath))
            {
                throw new InvalidOperationException(
                    "Medium Shield identity or Animator data is invalid.");
            }
        }

        private static void ValidateShieldPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_ShieldPrefabPath);
            try
            {
                Transform pivot = root.transform.Find("Weapon Pivot");
                Transform mesh = pivot?.Find("Weapon Mesh");
                Transform colliderObject = pivot?.Find("Damage Collider");
                BoxCollider boxCollider = colliderObject?.GetComponent<BoxCollider>();
                if (root.GetComponent<WeaponManager>() == null ||
                    mesh == null ||
                    colliderObject?.GetComponent<MeleeWeaponDamageCollider>() == null ||
                    boxCollider == null ||
                    !boxCollider.isTrigger ||
                    boxCollider.enabled)
                {
                    throw new InvalidOperationException(
                        "Medium Shield prefab hierarchy or collider is invalid.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerInventoryManager inventory =
                    GetRequiredComponent<PlayerInventoryManager>(root);
                SerializedProperty leftSlots = GetRequiredProperty(
                    new SerializedObject(inventory),
                    "m_weaponsInLeftHandSlots");
                WeaponModelInstantiationSlot[] modelSlots =
                    root.GetComponentsInChildren<WeaponModelInstantiationSlot>(true);
                if (leftSlots.arraySize != 3 ||
                    leftSlots.GetArrayElementAtIndex(0).objectReferenceValue !=
                        LoadRequiredAsset<WeaponItem>(k_ShieldPath) ||
                    modelSlots.Count(slot =>
                        slot.WeaponModelSlot == WeaponModelSlot.LeftHandSlot) != 1 ||
                    modelSlots.Count(slot =>
                        slot.WeaponModelSlot ==
                            WeaponModelSlot.LeftHandShieldSlot) != 1 ||
                    !GetRequiredComponent<PlayerCombatManager>(root).CanBlock)
                {
                    throw new InvalidOperationException(
                        "Player prefab needs the shield quick slot and split model slots.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateWorldItemDatabase()
        {
            GameObject root = LoadRequiredAsset<GameObject>(k_DatabasePrefabPath);
            WorldItemDatabase database = GetRequiredComponent<WorldItemDatabase>(root);
            if (database.Items.Count < 4 ||
                database.Items[k_ShieldItemID] !=
                    LoadRequiredAsset<WeaponItem>(k_ShieldPath))
            {
                throw new InvalidOperationException(
                    "World Item Database ID 3 must resolve Medium Shield.");
            }
        }

        private static void ValidateNetworkContract()
        {
            GameObject player = LoadRequiredAsset<GameObject>(k_PlayerPrefabPath);
            CharacterNetworkManager networkManager =
                GetRequiredComponent<CharacterNetworkManager>(player);
            if (networkManager.IsBlocking.ReadPerm !=
                    NetworkVariableReadPermission.Everyone ||
                networkManager.IsBlocking.WritePerm !=
                    NetworkVariableWritePermission.Owner ||
                networkManager.IsAttacking.ReadPerm !=
                    NetworkVariableReadPermission.Everyone ||
                networkManager.IsAttacking.WritePerm !=
                    NetworkVariableWritePermission.Owner ||
                typeof(CharacterNetworkManager).GetMethod(
                    "OnIsBlockingChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic) == null ||
                typeof(CharacterNetworkManager).GetMethod(
                    nameof(CharacterNetworkManager.RefreshBlockingPresentation),
                    BindingFlags.Instance | BindingFlags.Public) == null)
            {
                throw new InvalidOperationException(
                    "Blocking and attack state need owner writes, late join, and callbacks.");
            }
        }

        private static void ValidateRuntimeArchitecture()
        {
            BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;
            if (!typeof(WeaponItemBasedAction).IsAssignableFrom(
                    typeof(OffHandMeleeAction)) ||
                typeof(PlayerCombatManager).GetMethod(
                    nameof(PlayerCombatManager.SetBlocking),
                    publicInstance) == null ||
                typeof(CharacterStatsManager).GetMethod(
                    nameof(CharacterStatsManager.CheckForGuardBreak),
                    publicInstance) == null ||
                typeof(CharacterSoundFXManager).GetMethod(
                    nameof(CharacterSoundFXManager.PlayBlockingSoundEffect),
                    publicInstance)?.IsVirtual != true ||
                typeof(PlayerSoundFXManager).GetMethod(
                    nameof(PlayerSoundFXManager.PlayBlockingSoundEffect),
                    publicInstance)?.DeclaringType != typeof(PlayerSoundFXManager) ||
                (int)CharacterActionAnimation.GuardBreak != 5)
            {
                throw new InvalidOperationException(
                    "Blocking action, Guard Break, or weapon sound routing is incomplete.");
            }
        }

        private static void ValidateStaminaFormula()
        {
            if (!Mathf.Approximately(
                    CharacterStatsManager.CalculateBlockingStaminaDamage(20f, 0f),
                    20f) ||
                !Mathf.Approximately(
                    CharacterStatsManager.CalculateBlockingStaminaDamage(20f, 50f),
                    10f) ||
                !Mathf.Approximately(
                    CharacterStatsManager.CalculateBlockingStaminaDamage(20f, 100f),
                    0f))
            {
                throw new InvalidOperationException(
                    "Blocking stamina damage must apply Stability as a percentage.");
            }

            TakeBlockedDamageEffect template =
                LoadRequiredAsset<TakeBlockedDamageEffect>(
                    "Assets/Resources/Effects/Take Blocked Damage Effect.asset");
            TakeBlockedDamageEffect runtimeEffect =
                template.CreateRuntimeBlockedDamageEffect(
                    null,
                    10f,
                    0f,
                    0f,
                    0f,
                    0f,
                    Vector3.zero,
                    20f,
                    100f,
                    0f,
                    0f,
                    0f,
                    0f,
                    50f);
            try
            {
                if (!Mathf.Approximately(runtimeEffect.StaminaDamage, 10f) ||
                    !Mathf.Approximately(runtimeEffect.BlockingStability, 50f))
                {
                    throw new InvalidOperationException(
                        "Blocked damage effects do not carry Stability and stamina damage.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeEffect);
            }
        }

        private static Bounds CalculateLocalBounds(
            Transform relativeTo,
            GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one * 0.5f);
            }

            Vector3 minimum = new Vector3(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity);
            Vector3 maximum = new Vector3(
                float.NegativeInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity);
            foreach (Renderer renderer in renderers)
            {
                Bounds bounds = renderer.bounds;
                minimum = Vector3.Min(
                    minimum,
                    relativeTo.InverseTransformPoint(bounds.min));
                maximum = Vector3.Max(
                    maximum,
                    relativeTo.InverseTransformPoint(bounds.max));
            }

            return new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
        }

        private static AnimatorState GetOrCreateState(
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
                    $"Animator is missing state {stateName}.");
        }

        private static bool HasBoolParameter(
            AnimatorController controller,
            string parameterName)
        {
            return controller.parameters.Any(parameter =>
                parameter.name == parameterName &&
                parameter.type == AnimatorControllerParameterType.Bool);
        }

        private static void EnsureBinding(
            InputAction action,
            string path,
            string groups)
        {
            if (!HasBinding(action, path))
            {
                action.AddBinding(path, groups: groups);
            }
        }

        private static bool HasBinding(InputAction action, string path)
        {
            return action.bindings.Any(binding => binding.path == path);
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SetObjectReference(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetRequiredProperty(serializedObject, propertyName)
                .objectReferenceValue = value;
        }

        private static void SetObjectArray(
            SerializedObject serializedObject,
            string propertyName,
            IReadOnlyList<UnityEngine.Object> values)
        {
            SerializedProperty property = GetRequiredProperty(
                serializedObject,
                propertyName);
            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            GetRequiredProperty(serializedObject, propertyName).stringValue = value;
        }

        private static void SetInt(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).intValue = value;
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
        }

        private static void SetBool(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            GetRequiredProperty(serializedObject, propertyName).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).enumValueIndex = value;
        }

        private static void SetVector3(
            SerializedObject serializedObject,
            string propertyName,
            Vector3 value)
        {
            GetRequiredProperty(serializedObject, propertyName).vector3Value = value;
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

        private static T GetRequiredComponent<T>(GameObject gameObject)
            where T : Component
        {
            return gameObject.GetComponent<T>() ??
                throw new InvalidOperationException(
                    $"{gameObject.name} is missing {typeof(T).Name}.");
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private readonly struct WeaponBlockingDefinition
        {
            public WeaponBlockingDefinition(
                string path,
                WeaponModelType modelType,
                float physicalAbsorption,
                float magicAbsorption,
                float fireAbsorption,
                float lightningAbsorption,
                float holyAbsorption,
                float stability)
            {
                Path = path;
                ModelType = modelType;
                PhysicalAbsorption = physicalAbsorption;
                MagicAbsorption = magicAbsorption;
                FireAbsorption = fireAbsorption;
                LightningAbsorption = lightningAbsorption;
                HolyAbsorption = holyAbsorption;
                Stability = stability;
            }

            public string Path { get; }
            public WeaponModelType ModelType { get; }
            public float PhysicalAbsorption { get; }
            public float MagicAbsorption { get; }
            public float FireAbsorption { get; }
            public float LightningAbsorption { get; }
            public float HolyAbsorption { get; }
            public float Stability { get; }
        }
    }
}
