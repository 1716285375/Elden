using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP114-116 AI combat decision pipeline.</summary>
    public static class AICombatDecisionSystemSetup
    {
        private const string k_AIControllerPath =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Undead AI Animator.controller";
        private const string k_UndeadPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";
        private const string k_AttackFolder = "Assets/_Game/Data/AI/Combat";
        private const string k_UndeadAttack01Path =
            k_AttackFolder + "/Undead Swipe 01.asset";
        private const string k_UndeadAttack02Path =
            k_AttackFolder + "/Undead Swipe 02.asset";
        private const string k_WatcherClawPath =
            "Assets/_Game/Data/AI/Boss/Fallen Watcher/Watcher Claw.asset";
        private const string k_WatcherFrenzyPath =
            "Assets/_Game/Data/AI/Boss/Fallen Watcher/Watcher Frenzy.asset";
        private const string k_ZombieAttack01Path =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Combat/General/" +
            "zombie_light_attack_01.anim";
        private const string k_ZombieAttack02Path =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Combat/General/" +
            "zombie_swipe_attack_02.anim";
        private const string k_ZombieHeavyAttackPath =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Combat/General/" +
            "zombie_swipe_attack_01.anim";
        private const string k_NormalLeftPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Locomotion/" +
            "core_main_walk_L_01.anim";
        private const string k_NormalRightPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Locomotion/" +
            "core_main_walk_R_01.anim";
        private const string k_BlockIdlePath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Locomotion/" +
            "shield_off_guard_idle_01.anim";
        private const string k_BlockForwardPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Locomotion/" +
            "shield_off_guard_walk_F_01.anim";
        private const string k_BlockLeftPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Locomotion/" +
            "shield_off_guard_walk_L_01.anim";
        private const string k_BlockRightPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Locomotion/" +
            "shield_off_guard_walk_R_01.anim";
        private const string k_BlockSound01Path =
            "Assets/_Game/Audio/SFX/Combat/SFX_Metal_Shield_Medium_Impact_01.wav";
        private const string k_BlockSound02Path =
            "Assets/_Game/Audio/SFX/Combat/SFX_Metal_Shield_Medium_Impact_02.wav";
        private const string k_BlockSound03Path =
            "Assets/_Game/Audio/SFX/Combat/SFX_Metal_Shield_Medium_Impact_03.wav";
        private const string k_LocomotionStateName = "Locomotion";
        private const string k_BlockingStateName = "Blocking";
        private const string k_BlockingParameterName = "isBlocking";
        private const string k_EnableComboEventName = "EnableCanDoCombo";
        private const string k_DisableComboEventName = "DisableCanDoCombo";

        private static readonly string[] s_comboClipPaths =
        {
            k_ZombieAttack01Path,
            k_ZombieAttack02Path,
            k_ZombieHeavyAttackPath
        };

        private static readonly string[] s_blockSoundPaths =
        {
            k_BlockSound01Path,
            k_BlockSound02Path,
            k_BlockSound03Path
        };

        [MenuItem("Tools/Elden/Configure AI Combat Decision System")]
        public static void ConfigureAICombatDecisionSystem()
        {
            ConfigureAttackActions();
            ConfigureAnimatorController();
            ConfigureComboAnimationEvents();
            ConfigureAIPrefab(k_UndeadPrefabPath, false);
            ConfigureAIPrefab(k_BossPrefabPath, true);
            AssetDatabase.SaveAssets();
            ValidateAICombatDecisionSystem();
            Debug.Log(
                "[AICombatDecisionSystemSetup] Configured Strafe, Block, Combo, " +
                "hit confirmation, sounds, and server-authored movement.");
        }

        [MenuItem("Tools/Elden/Validate AI Combat Decision System")]
        public static void ValidateAICombatDecisionSystem()
        {
            ValidateRuntimeContracts();
            ValidateAttackActions();
            ValidateAnimatorController();
            ValidateComboAnimationEvents();
            ValidateAIPrefab(k_UndeadPrefabPath, false);
            ValidateAIPrefab(k_BossPrefabPath, true);
            Debug.Log(
                "[AICombatDecisionSystemValidation] EP114-116 AI combat " +
                "configuration is valid.");
        }

        private static void ConfigureAttackActions()
        {
            EnsureFolder(k_AttackFolder);
            AICharacterAttackAction followUp = ConfigureAttackAction(
                k_UndeadAttack02Path,
                AttackType.LightAttack02,
                0.45f,
                2.75f,
                1.55f,
                30f,
                20f,
                null);
            ConfigureAttackAction(
                k_UndeadAttack01Path,
                AttackType.LightAttack01,
                0f,
                2.1f,
                2.6f,
                25f,
                15f,
                followUp);

            BossAttackData claw = LoadRequiredAsset<BossAttackData>(
                k_WatcherClawPath);
            BossAttackData frenzy = LoadRequiredAsset<BossAttackData>(
                k_WatcherFrenzyPath);
            SetObjectReference(claw, "m_comboAction", frenzy);
        }

        private static AICharacterAttackAction ConfigureAttackAction(
            string assetPath,
            AttackType attackType,
            float minimumRange,
            float maximumRange,
            float recoveryTime,
            float physicalDamage,
            float poiseDamage,
            AICharacterAttackAction comboAction)
        {
            AICharacterAttackAction action =
                AssetDatabase.LoadAssetAtPath<AICharacterAttackAction>(assetPath);
            if (action == null)
            {
                action = ScriptableObject.CreateInstance<AICharacterAttackAction>();
                AssetDatabase.CreateAsset(action, assetPath);
            }

            SerializedObject serializedAction = new SerializedObject(action);
            serializedAction.FindProperty("m_attackType").enumValueIndex =
                (int)attackType;
            serializedAction.FindProperty("m_isParryable").boolValue = true;
            serializedAction.FindProperty("m_minimumRange").floatValue =
                minimumRange;
            serializedAction.FindProperty("m_maximumRange").floatValue =
                maximumRange;
            serializedAction.FindProperty("m_selectionWeight").floatValue = 1f;
            serializedAction.FindProperty("m_recoveryTime").floatValue =
                recoveryTime;
            serializedAction.FindProperty("m_comboAction").objectReferenceValue =
                comboAction;
            serializedAction.FindProperty("m_physicalDamage").floatValue =
                physicalDamage;
            serializedAction.FindProperty("m_magicDamage").floatValue = 0f;
            serializedAction.FindProperty("m_fireDamage").floatValue = 0f;
            serializedAction.FindProperty("m_lightningDamage").floatValue = 0f;
            serializedAction.FindProperty("m_holyDamage").floatValue = 0f;
            serializedAction.FindProperty("m_poiseDamage").floatValue = poiseDamage;
            serializedAction.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
            return action;
        }

        private static void ConfigureAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AIControllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotionState = GetRequiredState(
                stateMachine,
                k_LocomotionStateName);
            if (locomotionState.motion is not BlendTree locomotionTree)
            {
                throw new InvalidOperationException(
                    "AI Locomotion must remain a Blend Tree.");
            }

            Motion idleMotion = locomotionTree.children[0].motion;
            Motion forwardMotion = locomotionTree.children[1].motion;
            ConfigureDirectionalBlendTree(
                locomotionTree,
                idleMotion,
                forwardMotion,
                LoadRequiredAsset<AnimationClip>(k_NormalLeftPath),
                LoadRequiredAsset<AnimationClip>(k_NormalRightPath));

            AnimatorState blockingState = stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == k_BlockingStateName) ??
                stateMachine.AddState(
                    k_BlockingStateName,
                    new Vector3(280f, 260f, 0f));
            BlendTree blockingTree = blockingState.motion as BlendTree;
            if (blockingTree == null)
            {
                blockingTree = new BlendTree
                {
                    name = "AI Blocking Blend Tree",
                    hideFlags = HideFlags.HideInHierarchy
                };
                AssetDatabase.AddObjectToAsset(blockingTree, controller);
                blockingState.motion = blockingTree;
            }

            ConfigureDirectionalBlendTree(
                blockingTree,
                LoadRequiredAsset<AnimationClip>(k_BlockIdlePath),
                LoadRequiredAsset<AnimationClip>(k_BlockForwardPath),
                LoadRequiredAsset<AnimationClip>(k_BlockLeftPath),
                LoadRequiredAsset<AnimationClip>(k_BlockRightPath));
            ConfigureBooleanTransition(
                locomotionState,
                blockingState,
                AnimatorConditionMode.If);
            ConfigureBooleanTransition(
                blockingState,
                locomotionState,
                AnimatorConditionMode.IfNot);
            EditorUtility.SetDirty(locomotionTree);
            EditorUtility.SetDirty(blockingTree);
            EditorUtility.SetDirty(blockingState);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureDirectionalBlendTree(
            BlendTree blendTree,
            Motion idleMotion,
            Motion forwardMotion,
            Motion leftMotion,
            Motion rightMotion)
        {
            blendTree.blendType = BlendTreeType.FreeformCartesian2D;
            blendTree.blendParameter = "Horizontal";
            blendTree.blendParameterY = "Vertical";
            blendTree.useAutomaticThresholds = false;
            blendTree.children = new[]
            {
                CreateChildMotion(idleMotion, Vector2.zero),
                CreateChildMotion(forwardMotion, new Vector2(0f, 0.5f)),
                CreateChildMotion(leftMotion, new Vector2(-0.5f, 0f)),
                CreateChildMotion(rightMotion, new Vector2(0.5f, 0f))
            };
        }

        private static ChildMotion CreateChildMotion(
            Motion motion,
            Vector2 position)
        {
            return new ChildMotion
            {
                motion = motion,
                position = position,
                timeScale = 1f
            };
        }

        private static void ConfigureBooleanTransition(
            AnimatorState source,
            AnimatorState destination,
            AnimatorConditionMode conditionMode)
        {
            AnimatorStateTransition transition = source.transitions
                .FirstOrDefault(candidate =>
                    candidate.destinationState == destination) ??
                source.AddTransition(destination);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.1f;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.canTransitionToSelf = false;
            transition.conditions = Array.Empty<AnimatorCondition>();
            transition.AddCondition(
                conditionMode,
                0f,
                k_BlockingParameterName);
            EditorUtility.SetDirty(transition);
        }

        private static void ConfigureComboAnimationEvents()
        {
            foreach (string clipPath in s_comboClipPaths)
            {
                AnimationClip clip = LoadRequiredAsset<AnimationClip>(clipPath);
                AnimationEvent[] existingEvents =
                    AnimationUtility.GetAnimationEvents(clip);
                AnimationEvent enableEvent = existingEvents
                    .Where(animationEvent =>
                        animationEvent.functionName == k_EnableComboEventName)
                    .OrderBy(animationEvent => animationEvent.time)
                    .FirstOrDefault();
                if (enableEvent == null)
                {
                    throw new InvalidOperationException(
                        $"{clipPath} is missing {k_EnableComboEventName}.");
                }

                float closeTime = Mathf.Clamp(
                    Mathf.Max(enableEvent.time + 0.05f, clip.length - 0.08f),
                    enableEvent.time + 0.01f,
                    clip.length);
                AnimationEvent[] configuredEvents = existingEvents
                    .Where(animationEvent =>
                        animationEvent.functionName != k_DisableComboEventName)
                    .Concat(new[]
                    {
                        new AnimationEvent
                        {
                            functionName = k_DisableComboEventName,
                            time = closeTime,
                            messageOptions = SendMessageOptions.RequireReceiver
                        }
                    })
                    .OrderBy(animationEvent => animationEvent.time)
                    .ToArray();
                AnimationUtility.SetAnimationEvents(clip, configuredEvents);
                EditorUtility.SetDirty(clip);
            }
        }

        private static void ConfigureAIPrefab(
            string prefabPath,
            bool enableCombatDecisions)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                AICharacterManager manager =
                    root.GetComponent<AICharacterManager>() ??
                    throw new InvalidOperationException(
                        $"{prefabPath} is missing AICharacterManager.");
                SerializedObject serializedManager = new SerializedObject(manager);
                serializedManager.FindProperty("m_defaultAttackAction")
                    .objectReferenceValue = enableCombatDecisions
                        ? null
                        : LoadRequiredAsset<AICharacterAttackAction>(
                            k_UndeadAttack01Path);
                serializedManager.FindProperty("m_willCircleTarget").boolValue =
                    enableCombatDecisions;
                serializedManager.FindProperty("m_combatStrafeAnimationAmount")
                    .floatValue = 0.5f;
                serializedManager.FindProperty("m_combatStrafeSpeed").floatValue =
                    1.35f;
                serializedManager.FindProperty("m_strafeObstacleBuffer").floatValue =
                    0.15f;
                serializedManager.FindProperty("m_strafeObstacleLayers")
                    .intValue = 1;
                serializedManager.FindProperty("m_canBlock").boolValue =
                    enableCombatDecisions;
                serializedManager.FindProperty("m_percentageOfTimeWillBlock")
                    .floatValue = 55f;
                serializedManager.FindProperty("m_canPerformCombo").boolValue =
                    enableCombatDecisions;
                serializedManager.FindProperty("m_chanceToPerformCombo")
                    .floatValue = 65f;
                serializedManager
                    .FindProperty("m_onlyPerformComboIfInitialAttackHits")
                    .boolValue = true;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();

                UnityEngine.AI.NavMeshAgent agent =
                    root.GetComponent<UnityEngine.AI.NavMeshAgent>();
                agent.angularSpeed = 720f;
                agent.acceleration = Mathf.Max(16f, agent.acceleration);
                ConfigureAISoundManager(root);
                EditorUtility.SetDirty(manager);
                EditorUtility.SetDirty(agent);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureAISoundManager(GameObject root)
        {
            CharacterSoundFXManager existing =
                root.GetComponentInChildren<CharacterSoundFXManager>(true);
            if (existing == null)
            {
                throw new InvalidOperationException(
                    $"{root.name} is missing CharacterSoundFXManager.");
            }

            AudioSource audioSource = existing.GetComponent<AudioSource>();
            AudioClip[] damageGrunts = ReadAudioArray(existing, "m_damageGrunts");
            AudioClip[] footstepSounds = ReadAudioArray(
                existing,
                "m_footstepSounds");
            GameObject soundObject = existing.gameObject;
            AICharacterSoundFXManager aiSoundManager =
                existing as AICharacterSoundFXManager;
            if (aiSoundManager == null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
                aiSoundManager = soundObject.AddComponent<AICharacterSoundFXManager>();
            }

            SerializedObject serializedSound = new SerializedObject(aiSoundManager);
            serializedSound.FindProperty("m_audioSource").objectReferenceValue =
                audioSource;
            WriteAudioArray(
                serializedSound.FindProperty("m_damageGrunts"),
                damageGrunts);
            WriteAudioArray(
                serializedSound.FindProperty("m_footstepSounds"),
                footstepSounds);
            WriteAudioArray(
                serializedSound.FindProperty("m_blockingSoundEffects"),
                s_blockSoundPaths
                    .Select(LoadRequiredAsset<AudioClip>)
                    .ToArray());
            serializedSound.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(aiSoundManager);
        }

        private static AudioClip[] ReadAudioArray(
            CharacterSoundFXManager manager,
            string propertyName)
        {
            SerializedProperty property = new SerializedObject(manager)
                .FindProperty(propertyName);
            return Enumerable.Range(0, property.arraySize)
                .Select(index =>
                    property.GetArrayElementAtIndex(index).objectReferenceValue)
                .OfType<AudioClip>()
                .ToArray();
        }

        private static void WriteAudioArray(
            SerializedProperty property,
            AudioClip[] clips)
        {
            property.arraySize = clips.Length;
            for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
            {
                property.GetArrayElementAtIndex(clipIndex).objectReferenceValue =
                    clips[clipIndex];
            }
        }

        private static void ValidateRuntimeContracts()
        {
            const BindingFlags k_PublicInstance =
                BindingFlags.Public | BindingFlags.Instance;
            if (typeof(AICharacterAttackAction).GetProperty(
                    nameof(AICharacterAttackAction.ComboAction)) == null ||
                typeof(AICharacterCombatManager).GetMethod(
                    nameof(AICharacterCombatManager.EnableCanDoCombo),
                    k_PublicInstance) == null ||
                typeof(AICharacterCombatManager).GetMethod(
                    nameof(AICharacterCombatManager.DisableCanDoCombo),
                    k_PublicInstance) == null ||
                typeof(AICharacterAnimatorManager).GetMethod(
                    k_EnableComboEventName,
                    k_PublicInstance) == null ||
                typeof(AICharacterAnimatorManager).GetMethod(
                    k_DisableComboEventName,
                    k_PublicInstance) == null ||
                typeof(AICharacterSpawner).GetField(
                    "m_manuallySetStats",
                    BindingFlags.NonPublic | BindingFlags.Instance) == null)
            {
                throw new InvalidOperationException(
                    "EP114-116 runtime contracts are incomplete.");
            }
        }

        private static void ValidateAttackActions()
        {
            AICharacterAttackAction initial =
                LoadRequiredAsset<AICharacterAttackAction>(k_UndeadAttack01Path);
            AICharacterAttackAction followUp =
                LoadRequiredAsset<AICharacterAttackAction>(k_UndeadAttack02Path);
            BossAttackData claw = LoadRequiredAsset<BossAttackData>(
                k_WatcherClawPath);
            BossAttackData frenzy = LoadRequiredAsset<BossAttackData>(
                k_WatcherFrenzyPath);
            if (initial.ComboAction != followUp ||
                initial.AttackType != AttackType.LightAttack01 ||
                followUp.AttackType != AttackType.LightAttack02 ||
                claw.ComboAction != frenzy)
            {
                throw new InvalidOperationException(
                    "AI attack assets do not contain the expected combo links.");
            }
        }

        private static void ValidateAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AIControllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotionState = GetRequiredState(
                stateMachine,
                k_LocomotionStateName);
            AnimatorState blockingState = GetRequiredState(
                stateMachine,
                k_BlockingStateName);
            if (locomotionState.motion is not BlendTree locomotionTree ||
                locomotionTree.blendType != BlendTreeType.FreeformCartesian2D ||
                locomotionTree.children.Length < 4 ||
                blockingState.motion is not BlendTree blockingTree ||
                blockingTree.blendType != BlendTreeType.FreeformCartesian2D ||
                blockingTree.children.Length < 4 ||
                !HasBooleanTransition(
                    locomotionState,
                    blockingState,
                    AnimatorConditionMode.If) ||
                !HasBooleanTransition(
                    blockingState,
                    locomotionState,
                    AnimatorConditionMode.IfNot))
            {
                throw new InvalidOperationException(
                    "AI locomotion and Blocking Blend Trees are incomplete.");
            }
        }

        private static bool HasBooleanTransition(
            AnimatorState source,
            AnimatorState destination,
            AnimatorConditionMode conditionMode)
        {
            return source.transitions.Any(transition =>
                transition.destinationState == destination &&
                !transition.hasExitTime &&
                transition.conditions.Any(condition =>
                    condition.mode == conditionMode &&
                    condition.parameter == k_BlockingParameterName));
        }

        private static void ValidateComboAnimationEvents()
        {
            foreach (string clipPath in s_comboClipPaths)
            {
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(
                    LoadRequiredAsset<AnimationClip>(clipPath));
                AnimationEvent enableEvent = events
                    .Where(animationEvent =>
                        animationEvent.functionName == k_EnableComboEventName)
                    .OrderBy(animationEvent => animationEvent.time)
                    .FirstOrDefault();
                AnimationEvent disableEvent = events
                    .SingleOrDefault(animationEvent =>
                        animationEvent.functionName == k_DisableComboEventName);
                if (enableEvent == null ||
                    disableEvent == null ||
                    disableEvent.time <= enableEvent.time)
                {
                    throw new InvalidOperationException(
                        $"{clipPath} has an invalid Combo Window.");
                }
            }
        }

        private static void ValidateAIPrefab(
            string prefabPath,
            bool shouldEnableCombatDecisions)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                AICharacterManager manager =
                    root.GetComponent<AICharacterManager>();
                SerializedObject serializedManager = manager != null
                    ? new SerializedObject(manager)
                    : null;
                CharacterStatsManager statsManager =
                    root.GetComponent<CharacterStatsManager>();
                SerializedObject serializedStats = statsManager != null
                    ? new SerializedObject(statsManager)
                    : null;
                UnityEngine.AI.NavMeshAgent agent =
                    root.GetComponent<UnityEngine.AI.NavMeshAgent>();
                AICharacterSoundFXManager soundManager =
                    root.GetComponentInChildren<AICharacterSoundFXManager>(true);
                SerializedObject serializedSound = soundManager != null
                    ? new SerializedObject(soundManager)
                    : null;
                if (serializedManager == null ||
                    serializedManager.FindProperty("m_willCircleTarget")
                        .boolValue != shouldEnableCombatDecisions ||
                    serializedManager.FindProperty("m_canBlock").boolValue !=
                        shouldEnableCombatDecisions ||
                    serializedManager.FindProperty("m_canPerformCombo").boolValue !=
                        shouldEnableCombatDecisions ||
                    serializedManager.FindProperty("m_strafeObstacleLayers")
                        .intValue == 0 ||
                    serializedStats == null ||
                    serializedStats.FindProperty("m_blockingPhysicalAbsorption")
                        .floatValue <= 0f ||
                    agent == null ||
                    agent.angularSpeed < 540f ||
                    agent.acceleration < 12f ||
                    serializedSound == null ||
                    serializedSound.FindProperty("m_blockingSoundEffects")
                        .arraySize < 3)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} has invalid AI combat decision tuning.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property =
                serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{target.name} is missing {propertyName}.");
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static AnimatorState GetRequiredState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName) ??
                throw new InvalidOperationException(
                    $"Animator Controller is missing {stateName}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int segmentIndex = 1;
                segmentIndex < segments.Length;
                segmentIndex++)
            {
                string childPath = currentPath + "/" + segments[segmentIndex];
                if (!AssetDatabase.IsValidFolder(childPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[segmentIndex]);
                }

                currentPath = childPath;
            }
        }
    }
}
