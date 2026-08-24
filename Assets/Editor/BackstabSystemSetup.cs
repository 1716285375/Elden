using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP73 Backstab pipeline.</summary>
    public static class BackstabSystemSetup
    {
        private const string k_PlayerControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_AIControllerPath =
            "Assets/Data/Animations/AI/Undead AI Animator.controller";
        private const string k_BackstabClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Actions/" +
            "core_main_backstab_01.anim";
        private const string k_UnarmedBackstabClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Actions/" +
            "unarmed_main_backstab_01.anim";
        private const string k_MaceBackstabClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Actions/" +
            "mace_main_backstab_01.anim";
        private const string k_BackstabbedClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
            "core_main_backstab_victim_01.anim";
        private const string k_BackstabGetUpClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
            "core_main_facedown_getup_180_01.anim";
        private const string k_BackstabDeathClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
            "core_down_death_01.anim";
        private const string k_RiposteDeathClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
            "core_up_death_01.anim";
        private const string k_UnarmedOverridePath =
            "Assets/Data/Animator Overrides/Weapons/" +
            "Unarmed Animator.overrideController";
        private const string k_WeaponFolderPath =
            "Assets/Data/Items/Weapons/Melee Weapons";
        private const string k_UndeadPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_BackstabStateName = "Backstab_01";
        private const string k_BackstabbedStateName = "Backstabbed_01";
        private const string k_BackstabGetUpStateName =
            "Backstabbed_Get_Up_01";
        private const string k_BackstabDeathStateName =
            "Backstab_Critical_Death_01";
        private const string k_RipostedStateName = "Riposted_01";
        private const string k_RiposteDeathStateName =
            "Riposte_Critical_Death_01";
        private const string k_ApplyDamageEventName = "ApplyCriticalDamage";
        private const float k_CriticalDamageEventTime = 0.6416665f;
        private const float k_BackstabDamageModifier = 2.8f;

        private static readonly string[] s_attackerClipPaths =
        {
            k_BackstabClipPath,
            k_UnarmedBackstabClipPath,
            k_MaceBackstabClipPath
        };

        [MenuItem("Tools/Elden/Configure Backstab System")]
        public static void ConfigureBackstabSystem()
        {
            ConfigureAnimatorController(k_PlayerControllerPath, true);
            ConfigureAnimatorController(k_AIControllerPath, false);
            ConfigureAnimationEvents();
            ConfigureUnarmedOverride();
            ConfigureWeaponModifiers();
            ConfigureBackstabTargets();
            AssetDatabase.SaveAssets();
            ValidateBackstabSystem();
            Debug.Log(
                "[BackstabSystemSetup] Configured rear detection, server " +
                "reservation, paired animations, alignment, and death branches.");
        }

        [MenuItem("Tools/Elden/Validate Backstab System")]
        public static void ValidateBackstabSystem()
        {
            ValidateRuntimeContracts();
            ValidateAnimatorController(k_PlayerControllerPath, true);
            ValidateAnimatorController(k_AIControllerPath, false);
            ValidateAnimationEvents();
            ValidateUnarmedOverride();
            ValidateWeaponModifiers();
            ValidateBackstabTarget(k_UndeadPrefabPath, true);
            ValidateBackstabTarget(k_BossPrefabPath, false);
            Debug.Log(
                "[BackstabSystemValidation] Priority, network payload, " +
                "target policy, hit frame, and Critical death branches are valid.");
        }

        private static void ConfigureAnimatorController(
            string controllerPath,
            bool includeAttackerState)
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                controllerPath);
            AnimatorStateMachine stateMachine = GetRequiredLayer(controller)
                .stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            EnsureBoolParameter(controller, "isDead");

            if (includeAttackerState)
            {
                AnimatorState backstabState = EnsureState(
                    stateMachine,
                    k_BackstabStateName,
                    LoadRequiredAsset<AnimationClip>(k_BackstabClipPath),
                    new Vector3(1260f, 430f));
                ConfigureTransition(
                    backstabState,
                    emptyState,
                    Array.Empty<AnimatorCondition>());
            }

            AnimatorState backstabbedState = EnsureState(
                stateMachine,
                k_BackstabbedStateName,
                LoadRequiredAsset<AnimationClip>(k_BackstabbedClipPath),
                new Vector3(1260f, 520f));
            AnimatorState getUpState = EnsureState(
                stateMachine,
                k_BackstabGetUpStateName,
                LoadRequiredAsset<AnimationClip>(k_BackstabGetUpClipPath),
                new Vector3(1510f, 480f));
            AnimatorState backstabDeathState = EnsureState(
                stateMachine,
                k_BackstabDeathStateName,
                LoadRequiredAsset<AnimationClip>(k_BackstabDeathClipPath),
                new Vector3(1510f, 570f));
            ConfigureTransition(
                backstabbedState,
                getUpState,
                CreateDeadCondition(AnimatorConditionMode.IfNot));
            ConfigureTransition(
                backstabbedState,
                backstabDeathState,
                CreateDeadCondition(AnimatorConditionMode.If));
            ConfigureTransition(
                getUpState,
                emptyState,
                Array.Empty<AnimatorCondition>());

            AnimatorState ripostedState = GetRequiredState(
                stateMachine,
                k_RipostedStateName);
            AnimatorState riposteDeathState = EnsureState(
                stateMachine,
                k_RiposteDeathStateName,
                LoadRequiredAsset<AnimationClip>(k_RiposteDeathClipPath),
                new Vector3(1510f, 670f));
            ConfigureTransition(
                ripostedState,
                riposteDeathState,
                CreateDeadCondition(AnimatorConditionMode.If));
            EditorUtility.SetDirty(controller);
        }

        private static AnimatorCondition[] CreateDeadCondition(
            AnimatorConditionMode mode)
        {
            return new[]
            {
                new AnimatorCondition
                {
                    mode = mode,
                    parameter = "isDead",
                    threshold = 0f
                }
            };
        }

        private static void ConfigureAnimationEvents()
        {
            foreach (string attackerClipPath in s_attackerClipPaths)
            {
                AnimationClip attackerClip =
                    LoadRequiredAsset<AnimationClip>(attackerClipPath);
                AnimationUtility.SetAnimationEvents(
                    attackerClip,
                    Array.Empty<AnimationEvent>());
                EditorUtility.SetDirty(attackerClip);
            }

            AnimationClip victimClip = LoadRequiredAsset<AnimationClip>(
                k_BackstabbedClipPath);
            AnimationUtility.SetAnimationEvents(
                victimClip,
                new[]
                {
                    new AnimationEvent
                    {
                        functionName = k_ApplyDamageEventName,
                        time = Mathf.Min(
                            k_CriticalDamageEventTime,
                            victimClip.length * 0.5f),
                        messageOptions = SendMessageOptions.RequireReceiver
                    }
                });
            EditorUtility.SetDirty(victimClip);
        }

        private static void ConfigureUnarmedOverride()
        {
            AnimatorOverrideController unarmedController =
                LoadRequiredAsset<AnimatorOverrideController>(
                    k_UnarmedOverridePath);
            AnimationClip baseBackstab = LoadRequiredAsset<AnimationClip>(
                k_BackstabClipPath);
            unarmedController[baseBackstab] =
                LoadRequiredAsset<AnimationClip>(k_UnarmedBackstabClipPath);
            EditorUtility.SetDirty(unarmedController);
        }

        private static void ConfigureWeaponModifiers()
        {
            string[] weaponGuids = AssetDatabase.FindAssets(
                "t:MeleeWeaponItem",
                new[] { k_WeaponFolderPath });
            foreach (string weaponGuid in weaponGuids)
            {
                MeleeWeaponItem weapon =
                    AssetDatabase.LoadAssetAtPath<MeleeWeaponItem>(
                        AssetDatabase.GUIDToAssetPath(weaponGuid));
                SerializedObject serializedWeapon = new SerializedObject(weapon);
                SerializedProperty modifier = serializedWeapon.FindProperty(
                    "m_backstabAttack01Modifier");
                if (modifier == null)
                {
                    throw new InvalidOperationException(
                        $"{weapon.name} is missing the Backstab modifier.");
                }

                if (Mathf.Approximately(
                        modifier.floatValue,
                        k_BackstabDamageModifier))
                {
                    continue;
                }

                modifier.floatValue = k_BackstabDamageModifier;
                serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
            }
        }

        private static void ConfigureBackstabTargets()
        {
            ConfigureBackstabTarget(k_UndeadPrefabPath, true);
            ConfigureBackstabTarget(k_BossPrefabPath, false);
        }

        private static void ConfigureBackstabTarget(
            string prefabPath,
            bool canBeBackstabbed)
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(prefabPath);
            CharacterCombatManager combatManager =
                prefab.GetComponentInChildren<CharacterCombatManager>(true) ??
                throw new InvalidOperationException(
                    $"{prefabPath} is missing CharacterCombatManager.");
            SerializedObject serializedCombat = new SerializedObject(
                combatManager);
            SerializedProperty property = serializedCombat.FindProperty(
                "m_canBeBackstabbed");
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{prefabPath} is missing the Backstab policy.");
            }

            if (property.boolValue == canBeBackstabbed)
            {
                return;
            }

            property.boolValue = canBeBackstabbed;
            serializedCombat.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(combatManager);
        }

        private static void ValidateRuntimeContracts()
        {
            const BindingFlags k_PublicInstance =
                BindingFlags.Public | BindingFlags.Instance;
            MethodInfo backstabRpc = typeof(CharacterNetworkManager).GetMethod(
                nameof(CharacterNetworkManager
                    .NotifyTheServerOfBackstabServerRpc),
                k_PublicInstance);
            string[] parameterNames = backstabRpc?.GetParameters()
                .Take(10)
                .Select(parameter => parameter.Name)
                .ToArray();
            string[] expectedNames =
            {
                "targetNetworkObjectId",
                "attackerNetworkObjectId",
                "weaponID",
                "criticalDamageAnimation",
                "physicalDamage",
                "magicDamage",
                "fireDamage",
                "lightningDamage",
                "holyDamage",
                "poiseDamage"
            };
            if (typeof(CharacterCombatManager).GetMethod(
                    nameof(CharacterCombatManager.AttemptBackstab),
                    k_PublicInstance) == null ||
                typeof(CharacterCombatManager).GetMethod(
                    nameof(CharacterCombatManager.ProcessBackstabFromServer),
                    k_PublicInstance) == null ||
                typeof(CharacterCombatManager).GetMethod(
                    nameof(CharacterCombatManager
                        .ForceMoveCharacterToBackstabPosition),
                    k_PublicInstance) == null ||
                typeof(WorldUtilityManager).GetMethod(
                    nameof(WorldUtilityManager
                        .GetBackstabPositionBasedOnWeaponClass),
                    BindingFlags.Public | BindingFlags.Static) == null ||
                parameterNames == null ||
                !parameterNames.SequenceEqual(expectedNames))
            {
                throw new InvalidOperationException(
                    "The EP73 Backstab runtime and RPC contracts are incomplete.");
            }
        }

        private static void ValidateAnimatorController(
            string controllerPath,
            bool includeAttackerState)
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                controllerPath);
            AnimatorStateMachine stateMachine = GetRequiredLayer(controller)
                .stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            AnimatorState backstabbedState = GetRequiredState(
                stateMachine,
                k_BackstabbedStateName);
            AnimatorState getUpState = GetRequiredState(
                stateMachine,
                k_BackstabGetUpStateName);
            AnimatorState backstabDeathState = GetRequiredState(
                stateMachine,
                k_BackstabDeathStateName);
            AnimatorState ripostedState = GetRequiredState(
                stateMachine,
                k_RipostedStateName);
            AnimatorState riposteDeathState = GetRequiredState(
                stateMachine,
                k_RiposteDeathStateName);
            bool hasAliveBranch = HasConditionalTransition(
                backstabbedState,
                getUpState,
                AnimatorConditionMode.IfNot);
            bool hasBackstabDeathBranch = HasConditionalTransition(
                backstabbedState,
                backstabDeathState,
                AnimatorConditionMode.If);
            bool hasRiposteDeathBranch = HasConditionalTransition(
                ripostedState,
                riposteDeathState,
                AnimatorConditionMode.If);
            bool getUpReturnsToEmpty = getUpState.transitions.Any(transition =>
                transition.destinationState == emptyState &&
                transition.hasExitTime);
            if (!hasAliveBranch ||
                !hasBackstabDeathBranch ||
                !hasRiposteDeathBranch ||
                !getUpReturnsToEmpty)
            {
                throw new InvalidOperationException(
                    $"{controllerPath} has invalid Critical death branches.");
            }

            if (includeAttackerState)
            {
                AnimatorState backstabState = GetRequiredState(
                    stateMachine,
                    k_BackstabStateName);
                if (backstabState.motion !=
                    LoadRequiredAsset<AnimationClip>(k_BackstabClipPath))
                {
                    throw new InvalidOperationException(
                        "Backstab_01 must use the shared overridable clip.");
                }
            }
        }

        private static bool HasConditionalTransition(
            AnimatorState source,
            AnimatorState destination,
            AnimatorConditionMode mode)
        {
            return source.transitions.Any(transition =>
                transition.destinationState == destination &&
                transition.hasExitTime &&
                transition.conditions.Length == 1 &&
                transition.conditions[0].parameter == "isDead" &&
                transition.conditions[0].mode == mode);
        }

        private static void ValidateAnimationEvents()
        {
            foreach (string attackerClipPath in s_attackerClipPaths)
            {
                if (AnimationUtility.GetAnimationEvents(
                        LoadRequiredAsset<AnimationClip>(attackerClipPath))
                    .Length != 0)
                {
                    throw new InvalidOperationException(
                        $"{attackerClipPath} contains obsolete events.");
                }
            }

            AnimationEvent[] victimEvents = AnimationUtility.GetAnimationEvents(
                LoadRequiredAsset<AnimationClip>(k_BackstabbedClipPath));
            if (victimEvents.Length != 1 ||
                victimEvents[0].functionName != k_ApplyDamageEventName ||
                victimEvents[0].time <= 0f ||
                typeof(CharacterAnimatorManager).GetMethod(
                    k_ApplyDamageEventName,
                    BindingFlags.Public | BindingFlags.Instance) == null)
            {
                throw new InvalidOperationException(
                    "Backstabbed must settle damage once on its hit frame.");
            }
        }

        private static void ValidateUnarmedOverride()
        {
            AnimatorOverrideController unarmedController =
                LoadRequiredAsset<AnimatorOverrideController>(
                    k_UnarmedOverridePath);
            AnimationClip baseBackstab = LoadRequiredAsset<AnimationClip>(
                k_BackstabClipPath);
            if (unarmedController[baseBackstab] !=
                LoadRequiredAsset<AnimationClip>(k_UnarmedBackstabClipPath))
            {
                throw new InvalidOperationException(
                    "The Unarmed controller does not override Backstab_01.");
            }
        }

        private static void ValidateWeaponModifiers()
        {
            string[] weaponGuids = AssetDatabase.FindAssets(
                "t:MeleeWeaponItem",
                new[] { k_WeaponFolderPath });
            if (weaponGuids.Length == 0)
            {
                throw new InvalidOperationException("No melee weapons were found.");
            }

            foreach (string weaponGuid in weaponGuids)
            {
                MeleeWeaponItem weapon =
                    AssetDatabase.LoadAssetAtPath<MeleeWeaponItem>(
                        AssetDatabase.GUIDToAssetPath(weaponGuid));
                if (weapon == null ||
                    !Mathf.Approximately(
                        weapon.BackstabAttack01Modifier,
                        k_BackstabDamageModifier))
                {
                    throw new InvalidOperationException(
                        "Every melee weapon needs the EP73 Backstab modifier.");
                }
            }
        }

        private static void ValidateBackstabTarget(
            string prefabPath,
            bool expectedValue)
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(prefabPath);
            CharacterCombatManager combatManager =
                prefab.GetComponentInChildren<CharacterCombatManager>(true) ??
                throw new InvalidOperationException(
                    $"{prefabPath} is missing CharacterCombatManager.");
            if (combatManager.CanBeBackstabbed != expectedValue)
            {
                throw new InvalidOperationException(
                    $"{prefabPath} has an invalid Backstab policy.");
            }
        }

        private static AnimatorControllerLayer GetRequiredLayer(
            AnimatorController controller)
        {
            return controller.layers.FirstOrDefault(layer =>
                    layer.name == k_ActionLayerName) ??
                throw new InvalidOperationException(
                    $"Animator Controller is missing {k_ActionLayerName}.");
        }

        private static AnimatorState EnsureState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Motion motion,
            Vector3 position)
        {
            AnimatorState state = stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(candidate => candidate.name == stateName) ??
                stateMachine.AddState(stateName, position);
            state.motion = motion;
            EditorUtility.SetDirty(state);
            return state;
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

        private static void ConfigureTransition(
            AnimatorState sourceState,
            AnimatorState destinationState,
            AnimatorCondition[] conditions)
        {
            AnimatorStateTransition transition = sourceState.transitions
                .FirstOrDefault(candidate =>
                    candidate.destinationState == destinationState) ??
                sourceState.AddTransition(destinationState);
            foreach (AnimatorStateTransition duplicate in sourceState.transitions
                .Where(candidate =>
                    candidate != transition &&
                    candidate.destinationState == destinationState)
                .ToArray())
            {
                sourceState.RemoveTransition(duplicate);
            }

            transition.hasExitTime = true;
            transition.exitTime = 0.95f;
            transition.hasFixedDuration = true;
            transition.duration = 0.05f;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.canTransitionToSelf = false;
            transition.conditions = conditions;
            EditorUtility.SetDirty(transition);
        }

        private static void EnsureBoolParameter(
            AnimatorController controller,
            string parameterName)
        {
            if (!controller.parameters.Any(parameter =>
                    parameter.name == parameterName))
            {
                controller.AddParameter(
                    parameterName,
                    AnimatorControllerParameterType.Bool);
            }
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }
    }
}
