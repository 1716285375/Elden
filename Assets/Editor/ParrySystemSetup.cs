using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP74-75 Ash of War and Parry pipeline.</summary>
    public static class ParrySystemSetup
    {
        private const string k_PlayerControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_AIControllerPath =
            "Assets/Data/Animations/AI/Undead AI Animator.controller";
        private const string k_FastParryClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/Shield/" +
            "shield_off_parry_01_fast_start.anim";
        private const string k_MediumParryClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/Shield/" +
            "shield_off_parry_01_start.anim";
        private const string k_SlowParryClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/Shield/" +
            "shield_off_parry_01_slow_start.anim";
        private const string k_ParryLandClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Locomotion/" +
            "shield_off_parry_01_land.anim";
        private const string k_ParriedClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/General/" +
            "core_main_parry_victim_01.anim";
        private const string k_ParryAssetFolder =
            "Assets/Data/Items/Ashes Of War";
        private const string k_ParryAssetPath =
            k_ParryAssetFolder + "/Parry Slow.asset";
        private const string k_MediumShieldPath =
            "Assets/Data/Items/Weapons/Melee Weapons/Medium Shield.asset";
        private const string k_ItemDatabasePath =
            "Assets/Data/Prefabs/Word Managers/World Item Database.prefab";
        private const string k_InputActionsPath = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_BossAttackFolder =
            "Assets/Data/AI/Boss/Fallen Watcher";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_ParriedStateName = "Parried_01";
        private const float k_ParryStaminaCost = 12f;

        private static readonly ParryClipConfiguration[] s_parryClips =
        {
            new ParryClipConfiguration(
                k_FastParryClipPath,
                "Parry_Fast_01",
                0.1333333f,
                0.3666667f),
            new ParryClipConfiguration(
                k_MediumParryClipPath,
                "Parry_Medium_01",
                0.1666667f,
                0.3666667f),
            new ParryClipConfiguration(
                k_SlowParryClipPath,
                "Parry_Slow_01",
                0.2666667f,
                0.4333333f)
        };

        [MenuItem("Tools/Elden/Configure Ash of War and Parry System")]
        public static void ConfigureParrySystem()
        {
            ParryAshOfWar parry = ConfigureParryAsset();
            ConfigureShield(parry);
            ConfigureItemDatabase(parry);
            ConfigureAnimatorController(k_PlayerControllerPath, true);
            ConfigureAnimatorController(k_AIControllerPath, false);
            ConfigureAnimationEvents();
            ConfigureBossAttackData();
            AssetDatabase.SaveAssets();
            ValidateParrySystem();
            Debug.Log(
                "[ParrySystemSetup] Configured LT Ash of War input, Parry " +
                "windows, server validation, Parried recovery, and Riposte opening.");
        }

        [MenuItem("Tools/Elden/Validate Ash of War and Parry System")]
        public static void ValidateParrySystem()
        {
            ValidateRuntimeContracts();
            ValidateInput();
            ValidateItemData();
            ValidateAnimatorController(k_PlayerControllerPath, true);
            ValidateAnimatorController(k_AIControllerPath, false);
            ValidateAnimationEvents();
            ValidateBossAttackData();
            ValidateDefaultEnemyAttack();
            Debug.Log(
                "[ParrySystemValidation] Ash catalog, LT bindings, timing " +
                "windows, network authority, collider priority, and Riposte chain are valid.");
        }

        private static ParryAshOfWar ConfigureParryAsset()
        {
            EnsureFolder(k_ParryAssetFolder);
            ParryAshOfWar parry = AssetDatabase.LoadAssetAtPath<ParryAshOfWar>(
                k_ParryAssetPath);
            if (parry == null)
            {
                parry = ScriptableObject.CreateInstance<ParryAshOfWar>();
                parry.name = "Parry Slow";
                AssetDatabase.CreateAsset(parry, k_ParryAssetPath);
            }

            MeleeWeaponItem shield = LoadRequiredAsset<MeleeWeaponItem>(
                k_MediumShieldPath);
            SerializedObject serializedParry = new SerializedObject(parry);
            serializedParry.FindProperty("m_itemName").stringValue =
                "Parry (Slow)";
            serializedParry.FindProperty("m_itemIcon").objectReferenceValue =
                shield.ItemIcon;
            serializedParry.FindProperty("m_itemDescription").stringValue =
                "Deflect an incoming parryable attack with a measured shield motion.";
            serializedParry.FindProperty("m_focusPointsCost").intValue = 0;
            serializedParry.FindProperty("m_staminaCost").floatValue =
                k_ParryStaminaCost;
            SerializedProperty weaponClasses = serializedParry.FindProperty(
                "m_usableWeaponClasses");
            weaponClasses.arraySize = 1;
            weaponClasses.GetArrayElementAtIndex(0).enumValueIndex =
                (int)WeaponClass.Shield;
            serializedParry.FindProperty("m_parrySpeed").enumValueIndex =
                (int)ParryAnimationSpeed.Slow;
            serializedParry.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(parry);
            return parry;
        }

        private static void ConfigureShield(ParryAshOfWar parry)
        {
            MeleeWeaponItem shield = LoadRequiredAsset<MeleeWeaponItem>(
                k_MediumShieldPath);
            SerializedObject serializedShield = new SerializedObject(shield);
            SerializedProperty ashProperty = serializedShield.FindProperty(
                "m_ashOfWarAction");
            if (ashProperty.objectReferenceValue == parry)
            {
                return;
            }

            ashProperty.objectReferenceValue = parry;
            serializedShield.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shield);
        }

        private static void ConfigureItemDatabase(ParryAshOfWar parry)
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(k_ItemDatabasePath);
            WorldItemDatabase database =
                prefab.GetComponent<WorldItemDatabase>() ??
                throw new InvalidOperationException(
                    "World Item Database prefab is missing its component.");
            SerializedObject serializedDatabase = new SerializedObject(database);
            SerializedProperty items = serializedDatabase.FindProperty("m_items");
            int previousItemCount = items.arraySize;
            SerializedProperty ashes = serializedDatabase.FindProperty(
                "m_ashesOfWar");
            int previousAshCount = ashes.arraySize;
            int itemIndex = AppendUniqueReference(items, parry);
            AppendUniqueReference(ashes, parry);
            if (items.arraySize != previousItemCount ||
                ashes.arraySize != previousAshCount)
            {
                serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(database);
            }

            SerializedObject serializedParry = new SerializedObject(parry);
            serializedParry.FindProperty("m_itemID").intValue = itemIndex;
            serializedParry.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(parry);
        }

        private static void ConfigureAnimatorController(
            string controllerPath,
            bool includeParryActions)
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                controllerPath);
            AnimatorStateMachine stateMachine = GetRequiredLayer(controller)
                .stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            if (includeParryActions)
            {
                for (int index = 0; index < s_parryClips.Length; index++)
                {
                    ParryClipConfiguration configuration = s_parryClips[index];
                    AnimatorState parryState = EnsureState(
                        stateMachine,
                        configuration.StateName,
                        LoadRequiredAsset<AnimationClip>(configuration.ClipPath),
                        new Vector3(1260f, 760f + index * 70f));
                    ConfigureTransition(parryState, emptyState);
                }

                AnimatorState parryLandState = EnsureState(
                    stateMachine,
                    "Parry_Land_01",
                    LoadRequiredAsset<AnimationClip>(k_ParryLandClipPath),
                    new Vector3(1510f, 760f));
                ConfigureTransition(parryLandState, emptyState);
            }

            AnimatorState parriedState = EnsureState(
                stateMachine,
                k_ParriedStateName,
                LoadRequiredAsset<AnimationClip>(k_ParriedClipPath),
                new Vector3(1510f, 900f));
            ConfigureTransition(parriedState, emptyState);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureAnimationEvents()
        {
            foreach (ParryClipConfiguration configuration in s_parryClips)
            {
                AnimationClip clip = LoadRequiredAsset<AnimationClip>(
                    configuration.ClipPath);
                AnimationUtility.SetAnimationEvents(
                    clip,
                    new[]
                    {
                        CreateAnimationEvent(
                            "EnableIsParrying",
                            configuration.WindowStart),
                        CreateAnimationEvent(
                            "DisableIsParrying",
                            configuration.WindowEnd)
                    });
                EditorUtility.SetDirty(clip);
            }

            AnimationClip parryLand = LoadRequiredAsset<AnimationClip>(
                k_ParryLandClipPath);
            AnimationUtility.SetAnimationEvents(
                parryLand,
                Array.Empty<AnimationEvent>());
            EditorUtility.SetDirty(parryLand);

            AnimationClip parried = LoadRequiredAsset<AnimationClip>(
                k_ParriedClipPath);
            AnimationUtility.SetAnimationEvents(
                parried,
                new[] { CreateAnimationEvent("EnableIsRipostable", 0.7f) });
            EditorUtility.SetDirty(parried);
        }

        private static AnimationEvent CreateAnimationEvent(
            string functionName,
            float time)
        {
            return new AnimationEvent
            {
                functionName = functionName,
                time = time,
                messageOptions = SendMessageOptions.RequireReceiver
            };
        }

        private static void ConfigureBossAttackData()
        {
            ConfigureBossAttack("Watcher Claw.asset", true);
            ConfigureBossAttack("Watcher Frenzy.asset", true);
            ConfigureBossAttack("Watcher Sweep.asset", false);
        }

        private static void ConfigureBossAttack(
            string fileName,
            bool isParryable)
        {
            BossAttackData attack = LoadRequiredAsset<BossAttackData>(
                $"{k_BossAttackFolder}/{fileName}");
            SerializedObject serializedAttack = new SerializedObject(attack);
            SerializedProperty parryableProperty = serializedAttack.FindProperty(
                "m_isParryable");
            if (parryableProperty.boolValue == isParryable)
            {
                return;
            }

            parryableProperty.boolValue = isParryable;
            serializedAttack.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
        }

        private static void ValidateRuntimeContracts()
        {
            const BindingFlags k_PublicInstance =
                BindingFlags.Public | BindingFlags.Instance;
            MethodInfo parryRpc = typeof(CharacterNetworkManager).GetMethod(
                nameof(CharacterNetworkManager.NotifyServerOfParryServerRpc),
                k_PublicInstance);
            object serverRpcAttribute = parryRpc?.GetCustomAttributes(false)
                .FirstOrDefault(attribute =>
                    attribute.GetType().Name == "ServerRpcAttribute");
            FieldInfo requireOwnership = serverRpcAttribute?.GetType()
                .GetField("RequireOwnership");
            bool? requiresOwnership = requireOwnership?.GetValue(
                serverRpcAttribute) as bool?;
            if (!typeof(Item).IsAssignableFrom(typeof(AshOfWar)) ||
                typeof(WeaponItem).GetProperty(
                    nameof(WeaponItem.AshOfWarAction)) == null ||
                typeof(WorldItemDatabase).GetMethod(
                    nameof(WorldItemDatabase.GetAshOfWarByID),
                    k_PublicInstance) == null ||
                typeof(CharacterCombatManager).GetMethod(
                    nameof(CharacterCombatManager.ProcessParryFromServer),
                    k_PublicInstance) == null ||
                typeof(CharacterCombatManager).GetMethod(
                    nameof(CharacterCombatManager.CloseAllDamageColliders),
                    k_PublicInstance) == null ||
                parryRpc == null ||
                serverRpcAttribute == null ||
                requiresOwnership != false)
            {
                throw new InvalidOperationException(
                    "The EP74-75 Ash of War and Parry contracts are incomplete.");
            }
        }

        private static void ValidateInput()
        {
            InputActionAsset inputAsset = LoadRequiredAsset<InputActionAsset>(
                k_InputActionsPath);
            InputAction action = inputAsset.FindAction("Player Movement/LT");
            bool hasGamepad = action?.bindings.Any(binding =>
                binding.path == "<Gamepad>/leftTrigger" &&
                binding.groups.Contains("Gamepad")) == true;
            bool hasKeyboard = action?.bindings.Any(binding =>
                binding.path == "<Keyboard>/c" &&
                binding.groups.Contains("Keyboard&Mouse")) == true;
            if (!hasGamepad || !hasKeyboard)
            {
                throw new InvalidOperationException(
                    "LT must bind the left trigger and keyboard C.");
            }
        }

        private static void ValidateItemData()
        {
            ParryAshOfWar parry = LoadRequiredAsset<ParryAshOfWar>(
                k_ParryAssetPath);
            MeleeWeaponItem shield = LoadRequiredAsset<MeleeWeaponItem>(
                k_MediumShieldPath);
            GameObject prefab = LoadRequiredAsset<GameObject>(k_ItemDatabasePath);
            WorldItemDatabase database = prefab.GetComponent<WorldItemDatabase>();
            if (parry.ParrySpeed != ParryAnimationSpeed.Slow ||
                parry.StaminaCost != k_ParryStaminaCost ||
                !parry.CanUseWithWeapon(shield) ||
                shield.AshOfWarAction != parry ||
                database == null ||
                database.GetAshOfWarByID(parry.ItemID) != parry)
            {
                throw new InvalidOperationException(
                    "Parry Slow is not registered and equipped on Medium Shield.");
            }
        }

        private static void ValidateAnimatorController(
            string controllerPath,
            bool includeParryActions)
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                controllerPath);
            AnimatorStateMachine stateMachine = GetRequiredLayer(controller)
                .stateMachine;
            AnimatorState parriedState = GetRequiredState(
                stateMachine,
                k_ParriedStateName);
            if (parriedState.motion !=
                LoadRequiredAsset<AnimationClip>(k_ParriedClipPath))
            {
                throw new InvalidOperationException(
                    $"{controllerPath} has an invalid Parried state.");
            }

            if (!includeParryActions)
            {
                return;
            }

            foreach (ParryClipConfiguration configuration in s_parryClips)
            {
                if (GetRequiredState(stateMachine, configuration.StateName).motion !=
                    LoadRequiredAsset<AnimationClip>(configuration.ClipPath))
                {
                    throw new InvalidOperationException(
                        $"{configuration.StateName} has an invalid motion.");
                }
            }

            if (GetRequiredState(stateMachine, "Parry_Land_01").motion !=
                LoadRequiredAsset<AnimationClip>(k_ParryLandClipPath))
            {
                throw new InvalidOperationException(
                    "Parry_Land_01 has an invalid motion.");
            }
        }

        private static void ValidateAnimationEvents()
        {
            foreach (ParryClipConfiguration configuration in s_parryClips)
            {
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(
                    LoadRequiredAsset<AnimationClip>(configuration.ClipPath));
                if (events.Length != 2 ||
                    events[0].functionName != "EnableIsParrying" ||
                    events[1].functionName != "DisableIsParrying" ||
                    events[0].time >= events[1].time)
                {
                    throw new InvalidOperationException(
                        $"{configuration.StateName} has an invalid Parry window.");
                }
            }

            AnimationEvent[] parriedEvents = AnimationUtility.GetAnimationEvents(
                LoadRequiredAsset<AnimationClip>(k_ParriedClipPath));
            if (parriedEvents.Length != 1 ||
                parriedEvents[0].functionName != "EnableIsRipostable" ||
                typeof(CharacterAnimatorManager).GetMethod(
                    "EnableIsRipostable",
                    BindingFlags.Public | BindingFlags.Instance) == null)
            {
                throw new InvalidOperationException(
                    "Parried must open exactly one Riposte window.");
            }
        }

        private static void ValidateBossAttackData()
        {
            BossAttackData claw = LoadRequiredAsset<BossAttackData>(
                $"{k_BossAttackFolder}/Watcher Claw.asset");
            BossAttackData frenzy = LoadRequiredAsset<BossAttackData>(
                $"{k_BossAttackFolder}/Watcher Frenzy.asset");
            BossAttackData sweep = LoadRequiredAsset<BossAttackData>(
                $"{k_BossAttackFolder}/Watcher Sweep.asset");
            if (!claw.IsParryable || !frenzy.IsParryable || sweep.IsParryable)
            {
                throw new InvalidOperationException(
                    "Boss attacks do not preserve the parryable/non-parryable split.");
            }
        }

        private static void ValidateDefaultEnemyAttack()
        {
            GameObject undeadPrefab = LoadRequiredAsset<GameObject>(
                "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab");
            AICharacterCombatManager combatManager =
                undeadPrefab.GetComponent<AICharacterCombatManager>();
            SerializedProperty defaultParryable = combatManager != null
                ? new SerializedObject(combatManager).FindProperty(
                    "m_defaultAttackIsParryable")
                : null;
            if (defaultParryable?.boolValue != true)
            {
                throw new InvalidOperationException(
                    "The standard Undead attack must remain Parryable.");
            }
        }

        private static int AppendUniqueReference(
            SerializedProperty array,
            UnityEngine.Object reference)
        {
            for (int index = 0; index < array.arraySize; index++)
            {
                if (array.GetArrayElementAtIndex(index).objectReferenceValue ==
                    reference)
                {
                    return index;
                }
            }

            int newIndex = array.arraySize;
            array.InsertArrayElementAtIndex(newIndex);
            array.GetArrayElementAtIndex(newIndex).objectReferenceValue = reference;
            return newIndex;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string childPath = $"{currentPath}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(childPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]);
                }

                currentPath = childPath;
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
            AnimatorState destinationState)
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
            transition.conditions = Array.Empty<AnimatorCondition>();
            EditorUtility.SetDirty(transition);
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private readonly struct ParryClipConfiguration
        {
            public ParryClipConfiguration(
                string clipPath,
                string stateName,
                float windowStart,
                float windowEnd)
            {
                ClipPath = clipPath;
                StateName = stateName;
                WindowStart = windowStart;
                WindowEnd = windowEnd;
            }

            public string ClipPath { get; }
            public string StateName { get; }
            public float WindowStart { get; }
            public float WindowEnd { get; }
        }
    }
}
