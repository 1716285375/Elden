using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP59 Poise and Ping reaction system.</summary>
    public static class PoiseSystemSetup
    {
        private const string k_BaseControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Base/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_UndeadControllerPath =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Undead AI Animator.controller";
        private const string k_AvatarMaskFolder = "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Masks";
        private const string k_AvatarMaskPath =
            k_AvatarMaskFolder + "/Head And Chest.mask";
        private const string k_PingLayerName = "Ping Damage Override";
        private const string k_EmptyStateName = "Ping Damage Empty";
        private const float k_LayerWeight = 0.84f;
        private const float k_ReactionExitTime = 0.9f;
        private const float k_ReactionTransitionDuration = 0.1f;

        private static readonly CharacterDefinition[] s_characterDefinitions =
        {
            new CharacterDefinition("Assets/_Game/Prefabs/Characters/Player/Player.prefab", 50f),
            new CharacterDefinition(
                "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab",
                30f),
            new CharacterDefinition(
                "Assets/_Game/Prefabs/Characters/AI/Fallen Watcher Boss.prefab",
                80f)
        };

        private static readonly ReactionDefinition[] s_reactionDefinitions =
        {
            new ReactionDefinition(
                DamageDirection.Front,
                "m_pingForwardAnimations",
                new[]
                {
                    ReactionPath("core_main_hit_reaction_light_F_01.anim"),
                    ReactionPath("core_main_hit_reaction_light_F_02.anim"),
                    ReactionPath("core_main_hit_reaction_light_F_03.anim")
                }),
            new ReactionDefinition(
                DamageDirection.Back,
                "m_pingBackwardAnimations",
                new[]
                {
                    ReactionPath("core_main_hit_reaction_light_B_01.anim"),
                    ReactionPath("core_main_hit_reaction_light_B_02.anim"),
                    ReactionPath("core_main_hit_reaction_light_B_03.anim")
                }),
            new ReactionDefinition(
                DamageDirection.Left,
                "m_pingLeftAnimations",
                new[]
                {
                    ReactionPath("core_main_hit_reaction_light_L_01.anim"),
                    ReactionPath("core_main_hit_reaction_light_L_02.anim"),
                    ReactionPath("core_main_hit_reaction_light_L_03.anim")
                }),
            new ReactionDefinition(
                DamageDirection.Right,
                "m_pingRightAnimations",
                new[]
                {
                    ReactionPath("core_main_hit_reaction_light_R_01.anim"),
                    ReactionPath("core_main_hit_reaction_light_R_02.anim"),
                    ReactionPath("core_main_hit_reaction_light_R_03.anim")
                })
        };

        [MenuItem("Tools/Elden/Configure Poise System")]
        public static void ConfigurePoiseSystem()
        {
            AvatarMask avatarMask = ConfigureAvatarMask();
            HashSet<AnimatorController> controllers = CollectCharacterControllers();
            controllers.Add(LoadRequiredAsset<AnimatorController>(k_BaseControllerPath));
            AnimatorController undeadController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(k_UndeadControllerPath);
            if (undeadController != null)
            {
                controllers.Add(undeadController);
            }

            foreach (AnimatorController controller in controllers)
            {
                ConfigureAnimatorController(controller, avatarMask);
            }

            foreach (CharacterDefinition character in s_characterDefinitions)
            {
                ConfigureCharacterPrefab(character);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidatePoiseSystem();
            Debug.Log(
                "[PoiseSystemSetup] Configured cumulative Poise, eight-second recovery, " +
                "full breaks, and non-locking directional Ping reactions.");
        }

        [MenuItem("Tools/Elden/Validate Poise System")]
        public static void ValidatePoiseSystem()
        {
            AvatarMask avatarMask = LoadRequiredAsset<AvatarMask>(k_AvatarMaskPath);
            ValidateAvatarMask(avatarMask);

            HashSet<AnimatorController> controllers = CollectCharacterControllers();
            controllers.Add(LoadRequiredAsset<AnimatorController>(k_BaseControllerPath));
            AnimatorController undeadController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(k_UndeadControllerPath);
            if (undeadController != null)
            {
                controllers.Add(undeadController);
            }

            foreach (AnimatorController controller in controllers)
            {
                ValidateAnimatorController(controller, avatarMask);
            }

            foreach (CharacterDefinition character in s_characterDefinitions)
            {
                ValidateCharacterPrefab(character);
            }

            ValidatePoiseRules();
            ValidateRuntimeContract();
            Debug.Log(
                "[PoiseSystemValidation] Formula, timer, character data, head/chest mask, " +
                "Ping pools, layers, and full-reaction branching are valid.");
        }

        private static AvatarMask ConfigureAvatarMask()
        {
            EnsureFolder(k_AvatarMaskFolder);
            AvatarMask avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                k_AvatarMaskPath);
            if (avatarMask == null)
            {
                avatarMask = new AvatarMask
                {
                    name = "Head And Chest"
                };
                AssetDatabase.CreateAsset(avatarMask, k_AvatarMaskPath);
            }

            for (int bodyPartIndex = 0;
                bodyPartIndex < (int)AvatarMaskBodyPart.LastBodyPart;
                bodyPartIndex++)
            {
                avatarMask.SetHumanoidBodyPartActive(
                    (AvatarMaskBodyPart)bodyPartIndex,
                    false);
            }

            avatarMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            avatarMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            EditorUtility.SetDirty(avatarMask);
            return avatarMask;
        }

        private static HashSet<AnimatorController> CollectCharacterControllers()
        {
            HashSet<AnimatorController> controllers = new HashSet<AnimatorController>();
            foreach (CharacterDefinition character in s_characterDefinitions)
            {
                GameObject characterRoot = PrefabUtility.LoadPrefabContents(
                    character.PrefabPath);
                try
                {
                    Animator animator = GetRequiredComponentInChildren<Animator>(
                        characterRoot);
                    AnimatorController controller = ResolveAnimatorController(
                        animator.runtimeAnimatorController);
                    if (controller == null)
                    {
                        throw new InvalidOperationException(
                            $"{character.PrefabPath} needs an Animator Controller.");
                    }

                    controllers.Add(controller);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(characterRoot);
                }
            }

            return controllers;
        }

        private static AnimatorController ResolveAnimatorController(
            RuntimeAnimatorController runtimeController)
        {
            while (runtimeController is AnimatorOverrideController overrideController)
            {
                runtimeController = overrideController.runtimeAnimatorController;
            }

            return runtimeController as AnimatorController;
        }

        private static void ConfigureAnimatorController(
            AnimatorController controller,
            AvatarMask avatarMask)
        {
            AnimatorControllerLayer layer = GetOrCreateLayer(controller);
            layer.defaultWeight = k_LayerWeight;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.avatarMask = avatarMask;

            AnimatorStateMachine stateMachine = layer.stateMachine;
            AnimatorState emptyState = GetOrCreateState(
                stateMachine,
                k_EmptyStateName,
                new Vector3(300f, 100f, 0f));
            emptyState.motion = null;
            stateMachine.defaultState = emptyState;

            int reactionIndex = 0;
            foreach (ReactionDefinition definition in s_reactionDefinitions)
            {
                foreach (string animationPath in definition.AnimationPaths)
                {
                    AnimationClip animation = LoadRequiredAsset<AnimationClip>(
                        animationPath);
                    AnimatorState reactionState = GetOrCreateState(
                        stateMachine,
                        animation.name,
                        GetStatePosition(reactionIndex));
                    reactionState.motion = animation;
                    ConfigureExitTransition(reactionState, emptyState);
                    reactionIndex++;
                }
            }

            AnimatorControllerLayer[] layers = controller.layers;
            for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
            {
                if (layers[layerIndex].name == k_PingLayerName)
                {
                    layers[layerIndex] = layer;
                    break;
                }
            }

            controller.layers = layers;
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static AnimatorControllerLayer GetOrCreateLayer(
            AnimatorController controller)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name == k_PingLayerName)
                {
                    return layer;
                }
            }

            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = k_PingLayerName
            };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);
            AnimatorControllerLayer newLayer = new AnimatorControllerLayer
            {
                name = k_PingLayerName,
                stateMachine = stateMachine,
                defaultWeight = k_LayerWeight
            };
            List<AnimatorControllerLayer> layers = new List<AnimatorControllerLayer>(
                controller.layers)
            {
                newLayer
            };
            controller.layers = layers.ToArray();
            return controller.layers[controller.layers.Length - 1];
        }

        private static void ConfigureCharacterPrefab(CharacterDefinition character)
        {
            GameObject characterRoot = PrefabUtility.LoadPrefabContents(
                character.PrefabPath);
            try
            {
                CharacterStatsManager statsManager =
                    GetRequiredComponentInChildren<CharacterStatsManager>(characterRoot);
                SerializedObject serializedStats = new SerializedObject(statsManager);
                SetFloat(serializedStats, "m_totalPoiseDamage", 0f);
                SetFloat(serializedStats, "m_basePoiseDefense", character.BasePoise);
                SetFloat(serializedStats, "m_offensivePoiseBonus", 0f);
                SetFloat(serializedStats, "m_defaultPoiseResetTime", 8f);
                SetFloat(serializedStats, "m_poiseResetTimer", 0f);
                serializedStats.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(statsManager);

                CharacterAnimatorManager animatorManager =
                    GetRequiredComponentInChildren<CharacterAnimatorManager>(characterRoot);
                SerializedObject serializedAnimator = new SerializedObject(animatorManager);
                foreach (ReactionDefinition definition in s_reactionDefinitions)
                {
                    SerializedProperty animations = GetRequiredProperty(
                        serializedAnimator,
                        definition.SerializedPropertyName);
                    animations.arraySize = definition.AnimationPaths.Length;
                    for (int animationIndex = 0;
                        animationIndex < definition.AnimationPaths.Length;
                        animationIndex++)
                    {
                        animations.GetArrayElementAtIndex(animationIndex)
                            .objectReferenceValue = LoadRequiredAsset<AnimationClip>(
                                definition.AnimationPaths[animationIndex]);
                    }
                }

                serializedAnimator.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(animatorManager);
                PrefabUtility.SaveAsPrefabAsset(characterRoot, character.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(characterRoot);
            }
        }

        private static void ConfigureExitTransition(
            AnimatorState reactionState,
            AnimatorState emptyState)
        {
            AnimatorStateTransition exitTransition = null;
            foreach (AnimatorStateTransition transition in reactionState.transitions)
            {
                if (transition.destinationState == emptyState)
                {
                    exitTransition = transition;
                    break;
                }
            }

            exitTransition ??= reactionState.AddTransition(emptyState);
            exitTransition.hasExitTime = true;
            exitTransition.exitTime = k_ReactionExitTime;
            exitTransition.hasFixedDuration = true;
            exitTransition.duration = k_ReactionTransitionDuration;
            exitTransition.conditions = Array.Empty<AnimatorCondition>();
        }

        private static void ValidateAvatarMask(AvatarMask avatarMask)
        {
            for (int bodyPartIndex = 0;
                bodyPartIndex < (int)AvatarMaskBodyPart.LastBodyPart;
                bodyPartIndex++)
            {
                AvatarMaskBodyPart bodyPart = (AvatarMaskBodyPart)bodyPartIndex;
                bool expectedActive = bodyPart == AvatarMaskBodyPart.Body ||
                    bodyPart == AvatarMaskBodyPart.Head;
                if (avatarMask.GetHumanoidBodyPartActive(bodyPart) != expectedActive)
                {
                    throw new InvalidOperationException(
                        $"Head And Chest mask has an invalid {bodyPart} flag.");
                }
            }
        }

        private static void ValidateAnimatorController(
            AnimatorController controller,
            AvatarMask avatarMask)
        {
            AnimatorControllerLayer layer = GetRequiredLayer(controller);
            if (!Mathf.Approximately(layer.defaultWeight, k_LayerWeight) ||
                layer.blendingMode != AnimatorLayerBlendingMode.Override ||
                layer.avatarMask != avatarMask)
            {
                throw new InvalidOperationException(
                    $"{controller.name}.{k_PingLayerName} has invalid blend settings.");
            }

            AnimatorStateMachine stateMachine = layer.stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            if (stateMachine.defaultState != emptyState || emptyState.motion != null)
            {
                throw new InvalidOperationException(
                    $"{controller.name} needs {k_EmptyStateName} as the Ping default.");
            }

            foreach (ReactionDefinition definition in s_reactionDefinitions)
            {
                foreach (string animationPath in definition.AnimationPaths)
                {
                    AnimationClip animation = LoadRequiredAsset<AnimationClip>(
                        animationPath);
                    AnimatorState reactionState = GetRequiredState(
                        stateMachine,
                        animation.name);
                    if (reactionState.motion != animation ||
                        !HasConfiguredExitTransition(reactionState, emptyState))
                    {
                        throw new InvalidOperationException(
                            $"{controller.name} Ping state {animation.name} is invalid.");
                    }
                }
            }
        }

        private static void ValidateCharacterPrefab(CharacterDefinition character)
        {
            GameObject characterRoot = PrefabUtility.LoadPrefabContents(
                character.PrefabPath);
            try
            {
                CharacterStatsManager statsManager =
                    GetRequiredComponentInChildren<CharacterStatsManager>(characterRoot);
                SerializedObject serializedStats = new SerializedObject(statsManager);
                AssertFloat(serializedStats, "m_totalPoiseDamage", 0f);
                AssertFloat(
                    serializedStats,
                    "m_basePoiseDefense",
                    character.BasePoise);
                AssertFloat(serializedStats, "m_offensivePoiseBonus", 0f);
                AssertFloat(serializedStats, "m_defaultPoiseResetTime", 8f);
                AssertFloat(serializedStats, "m_poiseResetTimer", 0f);

                CharacterAnimatorManager animatorManager =
                    GetRequiredComponentInChildren<CharacterAnimatorManager>(characterRoot);
                SerializedObject serializedAnimator = new SerializedObject(animatorManager);
                foreach (ReactionDefinition definition in s_reactionDefinitions)
                {
                    SerializedProperty animations = GetRequiredProperty(
                        serializedAnimator,
                        definition.SerializedPropertyName);
                    if (animations.arraySize != definition.AnimationPaths.Length)
                    {
                        throw new InvalidOperationException(
                            $"{character.PrefabPath} needs three {definition.Direction} " +
                            "Ping clips.");
                    }

                    for (int animationIndex = 0;
                        animationIndex < definition.AnimationPaths.Length;
                        animationIndex++)
                    {
                        AnimationClip expected = LoadRequiredAsset<AnimationClip>(
                            definition.AnimationPaths[animationIndex]);
                        if (animations.GetArrayElementAtIndex(animationIndex)
                            .objectReferenceValue != expected)
                        {
                            throw new InvalidOperationException(
                                $"{character.PrefabPath} has invalid " +
                                $"{definition.Direction} Ping clip {animationIndex}.");
                        }
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(characterRoot);
            }
        }

        private static void ValidatePoiseRules()
        {
            float remainingPoise = CharacterStatsManager.CalculateRemainingPoise(
                50f,
                10f,
                -25f);
            if (!Mathf.Approximately(remainingPoise, 35f) ||
                CharacterStatsManager.IsPoiseBroken(remainingPoise) ||
                !CharacterStatsManager.IsPoiseBroken(0f) ||
                !CharacterStatsManager.IsPoiseBroken(-1f))
            {
                throw new InvalidOperationException(
                    "Remaining Poise or break threshold is invalid.");
            }

            GameObject testObject = new GameObject("Poise Rule Test");
            try
            {
                CharacterStatsManager statsManager =
                    testObject.AddComponent<CharacterStatsManager>();
                statsManager.SetBasePoiseDefense(50f);
                statsManager.SetOffensivePoiseBonus(10f);
                if (statsManager.ApplyPoiseDamage(25f) ||
                    !Mathf.Approximately(statsManager.TotalPoiseDamage, -25f) ||
                    !Mathf.Approximately(statsManager.RemainingPoise, 35f) ||
                    !Mathf.Approximately(statsManager.PoiseResetTimer, 8f) ||
                    !statsManager.ApplyPoiseDamage(35f))
                {
                    throw new InvalidOperationException(
                        "Poise buildup, recovery timer, or break accumulation is invalid.");
                }

                MethodInfo advanceTimerMethod = typeof(CharacterStatsManager).GetMethod(
                    "AdvancePoiseResetTimer",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (advanceTimerMethod == null)
                {
                    throw new InvalidOperationException(
                        "Poise timer needs a deterministic advance path.");
                }

                advanceTimerMethod.Invoke(statsManager, new object[] { 7.99f });
                if (Mathf.Approximately(statsManager.TotalPoiseDamage, 0f))
                {
                    throw new InvalidOperationException(
                        "Poise damage reset before the recovery delay elapsed.");
                }

                advanceTimerMethod.Invoke(statsManager, new object[] { 0.01f });
                if (!Mathf.Approximately(statsManager.TotalPoiseDamage, 0f) ||
                    !Mathf.Approximately(statsManager.PoiseResetTimer, 0f))
                {
                    throw new InvalidOperationException(
                        "Poise recovery must clear damage and its timer.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testObject);
            }
        }

        private static void ValidateRuntimeContract()
        {
            MethodInfo pingMethod = typeof(CharacterAnimatorManager).GetMethod(
                nameof(CharacterAnimatorManager.PlayDirectionalPingDamageAnimation),
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo poiseMethod = typeof(CharacterStatsManager).GetMethod(
                nameof(CharacterStatsManager.ApplyPoiseDamage),
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo timerMethod = typeof(CharacterStatsManager).GetMethod(
                nameof(CharacterStatsManager.HandlePoiseResetTimer),
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo branchMethod = typeof(TakeDamageEffect).GetMethod(
                nameof(TakeDamageEffect.PlayDirectionalBasedDamageAnimation),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(CharacterManager), typeof(bool) },
                null);
            if (pingMethod == null ||
                poiseMethod == null ||
                timerMethod == null ||
                branchMethod == null)
            {
                throw new InvalidOperationException(
                    "Poise runtime branching and timer methods are incomplete.");
            }
        }

        private static AnimatorControllerLayer GetRequiredLayer(
            AnimatorController controller)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name == k_PingLayerName)
                {
                    return layer;
                }
            }

            throw new InvalidOperationException(
                $"{controller.name} needs the {k_PingLayerName} layer.");
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
                $"Animator state {stateName} is missing from {k_PingLayerName}.");
        }

        private static bool HasConfiguredExitTransition(
            AnimatorState reactionState,
            AnimatorState emptyState)
        {
            foreach (AnimatorStateTransition transition in reactionState.transitions)
            {
                if (transition.destinationState == emptyState &&
                    transition.hasExitTime &&
                    Mathf.Approximately(transition.exitTime, k_ReactionExitTime) &&
                    transition.hasFixedDuration &&
                    Mathf.Approximately(
                        transition.duration,
                        k_ReactionTransitionDuration) &&
                    transition.conditions.Length == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 GetStatePosition(int reactionIndex)
        {
            const float k_ColumnSpacing = 260f;
            const float k_RowSpacing = 90f;
            return new Vector3(
                600f + (reactionIndex / 3) * k_ColumnSpacing,
                20f + (reactionIndex % 3) * k_RowSpacing,
                0f);
        }

        private static string ReactionPath(string animationName)
        {
            return "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Reactions/" +
                animationName;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            int separatorIndex = folderPath.LastIndexOf('/');
            string parentPath = folderPath.Substring(0, separatorIndex);
            string folderName = folderPath.Substring(separatorIndex + 1);
            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
        }

        private static void AssertFloat(
            SerializedObject serializedObject,
            string propertyName,
            float expectedValue)
        {
            float actualValue = GetRequiredProperty(serializedObject, propertyName)
                .floatValue;
            if (!Mathf.Approximately(actualValue, expectedValue))
            {
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.name}.{propertyName} is " +
                    $"{actualValue}, not {expectedValue}.");
            }
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

        private static T GetRequiredComponentInChildren<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponentInChildren<T>(true);
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{gameObject.name} needs a {typeof(T).Name}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private readonly struct CharacterDefinition
        {
            public CharacterDefinition(string prefabPath, float basePoise)
            {
                PrefabPath = prefabPath;
                BasePoise = basePoise;
            }

            public string PrefabPath { get; }
            public float BasePoise { get; }
        }

        private readonly struct ReactionDefinition
        {
            public ReactionDefinition(
                DamageDirection direction,
                string serializedPropertyName,
                string[] animationPaths)
            {
                Direction = direction;
                SerializedPropertyName = serializedPropertyName;
                AnimationPaths = animationPaths;
            }

            public DamageDirection Direction { get; }
            public string SerializedPropertyName { get; }
            public string[] AnimationPaths { get; }
        }
    }
}
