using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP71 Stance Break pipeline.</summary>
    public static class StanceBreakingSystemSetup
    {
        private const string k_AIControllerPath =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Undead AI Animator.controller";
        private const string k_StanceBreakClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Actions/" +
            "core_main_stance_broken_f_01.anim";
        private const string k_StanceBreakSoundPath =
            "Assets/Art/Audio/SFX/Environment/SFX_Guard_Break_01.wav";
        private const string k_UndeadPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";
        private const string k_MainMenuScenePath =
            WorldScenePathLayout.MainMenuScenePath;
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_StanceBreakStateName = "Stance_Break_01";
        private const string k_PlaySoundEventName =
            "PlayStanceBrokenSoundEffect";
        private const string k_EnableRiposteEventName =
            "EnableIsRipostable";

        private static readonly string[] s_legacyEventNames =
        {
            "EnableCanBeRiposted",
            "PlayStanceBrokenSoundFX"
        };

        [MenuItem("Tools/Elden/Configure Stance Breaking System")]
        public static void ConfigureStanceBreakingSystem()
        {
            ConfigureAnimatorController();
            ConfigureAnimationEvents();
            ConfigureAIPrefab(k_UndeadPrefabPath, 80, 15, 3f);
            ConfigureAIPrefab(k_BossPrefabPath, 150, 25, 3f);
            ConfigureWorldManagers();
            AssetDatabase.SaveAssets();
            ValidateStanceBreakingSystem();
            Debug.Log(
                "[StanceBreakingSystemSetup] Configured owner Stance, instant " +
                "animation sync, Riposte window events, and shared sound.");
        }

        [MenuItem("Tools/Elden/Validate Stance Breaking System")]
        public static void ValidateStanceBreakingSystem()
        {
            ValidateRuntimeContracts();
            ValidateAnimatorController();
            ValidateAnimationEvents();
            ValidateAIPrefab(k_UndeadPrefabPath, 80);
            ValidateAIPrefab(k_BossPrefabPath, 150);
            ValidateWorldManagers();
            Debug.Log(
                "[StanceBreakingSystemValidation] Stance damage, recovery, " +
                "break priority, events, sound, and network state are valid.");
        }

        private static void ConfigureAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AIControllerPath);
            AnimatorControllerLayer actionLayer = GetRequiredLayer(
                controller,
                k_ActionLayerName);
            AnimatorStateMachine stateMachine = actionLayer.stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            AnimatorState stanceBreakState = stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == k_StanceBreakStateName) ??
                stateMachine.AddState(
                    k_StanceBreakStateName,
                    new Vector3(1080f, 310f, 0f));
            stanceBreakState.motion = LoadRequiredAsset<AnimationClip>(
                k_StanceBreakClipPath);

            AnimatorStateTransition transition = stanceBreakState.transitions
                .FirstOrDefault(candidate =>
                    candidate.destinationState == emptyState) ??
                stanceBreakState.AddTransition(emptyState);
            transition.hasExitTime = true;
            transition.exitTime = 0.95f;
            transition.hasFixedDuration = true;
            transition.duration = 0.05f;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.canTransitionToSelf = false;
            transition.conditions = Array.Empty<AnimatorCondition>();
            EditorUtility.SetDirty(transition);
            EditorUtility.SetDirty(stanceBreakState);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureAnimationEvents()
        {
            AnimationClip clip = LoadRequiredAsset<AnimationClip>(
                k_StanceBreakClipPath);
            AnimationEvent[] preservedEvents = AnimationUtility
                .GetAnimationEvents(clip)
                .Where(animationEvent =>
                    animationEvent.functionName != k_PlaySoundEventName &&
                    animationEvent.functionName != k_EnableRiposteEventName &&
                    !s_legacyEventNames.Contains(animationEvent.functionName))
                .ToArray();
            AnimationEvent[] configuredEvents = preservedEvents
                .Concat(new[]
                {
                    new AnimationEvent
                    {
                        functionName = k_PlaySoundEventName,
                        time = Mathf.Min(0.15f, clip.length * 0.1f),
                        messageOptions = SendMessageOptions.RequireReceiver
                    },
                    new AnimationEvent
                    {
                        functionName = k_EnableRiposteEventName,
                        time = Mathf.Min(0.7f, clip.length * 0.35f),
                        messageOptions = SendMessageOptions.RequireReceiver
                    }
                })
                .OrderBy(animationEvent => animationEvent.time)
                .ToArray();
            AnimationUtility.SetAnimationEvents(clip, configuredEvents);
            EditorUtility.SetDirty(clip);
        }

        private static void ConfigureAIPrefab(
            string prefabPath,
            int maximumStance,
            int regenerationPerSecond,
            float regenerationDelay)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                AICharacterCombatManager combatManager =
                    root.GetComponent<AICharacterCombatManager>();
                if (combatManager == null)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} is missing AICharacterCombatManager.");
                }

                SerializedObject serializedManager =
                    new SerializedObject(combatManager);
                serializedManager.FindProperty("m_maximumStance").intValue =
                    maximumStance;
                serializedManager
                    .FindProperty("m_stanceRegeneratedPerSecond").intValue =
                    regenerationPerSecond;
                serializedManager
                    .FindProperty(
                        "m_defaultTimeUntilStanceRegenerationBegins")
                    .floatValue = regenerationDelay;
                serializedManager.FindProperty("m_ignoreStanceBreak").boolValue =
                    false;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(combatManager);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureWorldManagers()
        {
            Scene scene = GetSceneForEditing(k_MainMenuScenePath, out bool wasLoaded);
            try
            {
                WorldSoundFXManager soundManager = FindComponentInScene<WorldSoundFXManager>(
                    scene) ??
                    throw new InvalidOperationException(
                        "The Main Menu scene is missing WorldSoundFXManager.");
                if (soundManager.GetComponent<WorldUtilityManager>() == null)
                {
                    soundManager.gameObject.AddComponent<WorldUtilityManager>();
                }

                SerializedObject serializedSoundManager =
                    new SerializedObject(soundManager);
                serializedSoundManager
                    .FindProperty("m_stanceBreakSoundEffect")
                    .objectReferenceValue = LoadRequiredAsset<AudioClip>(
                        k_StanceBreakSoundPath);
                serializedSoundManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(soundManager.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!wasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateRuntimeContracts()
        {
            const BindingFlags k_PublicInstance =
                BindingFlags.Public | BindingFlags.Instance;
            if (typeof(AICharacterCombatManager).GetMethod(
                    nameof(AICharacterCombatManager.DamageStance),
                    k_PublicInstance) == null ||
                typeof(CharacterCombatManager).GetMethod(
                    nameof(CharacterCombatManager.EnableIsRipostable),
                    k_PublicInstance) == null ||
                typeof(CharacterAnimatorManager).GetMethod(
                    nameof(CharacterAnimatorManager
                        .PlayTargetActionAnimationInstantly),
                    k_PublicInstance) == null ||
                typeof(CharacterSoundFXManager).GetMethod(
                    nameof(CharacterSoundFXManager
                        .PlayStanceBrokenSoundEffect),
                    k_PublicInstance) == null ||
                typeof(CharacterNetworkManager).GetField(
                    nameof(CharacterNetworkManager.IsRipostable)) == null ||
                typeof(CharacterNetworkManager).GetField(
                    nameof(CharacterNetworkManager
                        .IsBeingCriticallyDamaged)) == null)
            {
                throw new InvalidOperationException(
                    "The EP71 runtime Stance and network contracts are incomplete.");
            }

            if (WorldUtilityManager.GetDamageIntensityBasedOnPoiseDamage(120f) !=
                DamageIntensity.Colossal)
            {
                throw new InvalidOperationException(
                    "The shared poise-damage classifier is invalid.");
            }
        }

        private static void ValidateAnimatorController()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AIControllerPath);
            AnimatorStateMachine stateMachine = GetRequiredLayer(
                    controller,
                    k_ActionLayerName)
                .stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            AnimatorState stanceBreakState = GetRequiredState(
                stateMachine,
                k_StanceBreakStateName);
            if (stanceBreakState.motion !=
                    LoadRequiredAsset<AnimationClip>(k_StanceBreakClipPath) ||
                !stanceBreakState.transitions.Any(transition =>
                    transition.destinationState == emptyState &&
                    transition.hasExitTime &&
                    transition.exitTime >= 0.9f))
            {
                throw new InvalidOperationException(
                    "Stance_Break_01 must use its clip and return to Empty.");
            }
        }

        private static void ValidateAnimationEvents()
        {
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(
                LoadRequiredAsset<AnimationClip>(k_StanceBreakClipPath));
            AnimationEvent soundEvent = events.SingleOrDefault(animationEvent =>
                animationEvent.functionName == k_PlaySoundEventName);
            AnimationEvent riposteEvent = events.SingleOrDefault(animationEvent =>
                animationEvent.functionName == k_EnableRiposteEventName);
            if (soundEvent == null ||
                riposteEvent == null ||
                soundEvent.time <= 0f ||
                soundEvent.time >= riposteEvent.time)
            {
                throw new InvalidOperationException(
                    "The Stance Break clip needs ordered sound and Riposte events.");
            }

            const BindingFlags k_PublicInstance =
                BindingFlags.Public | BindingFlags.Instance;
            if (typeof(AICharacterAnimatorManager).GetMethod(
                    k_PlaySoundEventName,
                    k_PublicInstance) == null ||
                typeof(AICharacterAnimatorManager).GetMethod(
                    k_EnableRiposteEventName,
                    k_PublicInstance) == null)
            {
                throw new InvalidOperationException(
                    "AICharacterAnimatorManager is missing Stance event receivers.");
            }
        }

        private static void ValidateAIPrefab(
            string prefabPath,
            int expectedMaximumStance)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                AICharacterCombatManager combatManager =
                    root.GetComponent<AICharacterCombatManager>();
                SerializedObject serializedManager = combatManager != null
                    ? new SerializedObject(combatManager)
                    : null;
                if (serializedManager == null ||
                    serializedManager.FindProperty("m_maximumStance").intValue !=
                        expectedMaximumStance ||
                    serializedManager
                        .FindProperty("m_stanceRegeneratedPerSecond").intValue <= 0 ||
                    serializedManager
                        .FindProperty(
                            "m_defaultTimeUntilStanceRegenerationBegins")
                        .floatValue <= 0f)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} has invalid Stance tuning.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateWorldManagers()
        {
            Scene scene = GetSceneForEditing(k_MainMenuScenePath, out bool wasLoaded);
            try
            {
                WorldSoundFXManager soundManager =
                    FindComponentInScene<WorldSoundFXManager>(scene);
                SerializedObject serializedSoundManager = soundManager != null
                    ? new SerializedObject(soundManager)
                    : null;
                if (soundManager == null ||
                    soundManager.GetComponent<WorldUtilityManager>() == null ||
                    serializedSoundManager
                        .FindProperty("m_stanceBreakSoundEffect")
                        .objectReferenceValue !=
                    LoadRequiredAsset<AudioClip>(k_StanceBreakSoundPath))
                {
                    throw new InvalidOperationException(
                        "Persistent world managers are missing Stance Break data.");
                }
            }
            finally
            {
                if (!wasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static AnimatorControllerLayer GetRequiredLayer(
            AnimatorController controller,
            string layerName)
        {
            return controller.layers.FirstOrDefault(layer =>
                    layer.name == layerName) ??
                throw new InvalidOperationException(
                    $"Animator Controller is missing {layerName}.");
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

        private static T FindComponentInScene<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault();
        }

        private static Scene GetSceneForEditing(
            string scenePath,
            out bool wasLoaded)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            wasLoaded = scene.IsValid() && scene.isLoaded;
            return wasLoaded
                ? scene
                : EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
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
