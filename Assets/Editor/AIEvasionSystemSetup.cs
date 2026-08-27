using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP118 AI Evasion pipeline.</summary>
    public static class AIEvasionSystemSetup
    {
        private const string k_AIControllerPath =
            "Assets/Data/Animations/AI/Undead AI Animator.controller";
        private const string k_RollClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Locomotion/" +
            "core_main_roll_med_to_idle_F_01.anim";
        private const string k_UndeadPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_RollStateName = "Roll_Forward_01";

        [MenuItem("Tools/Elden/Configure AI Evasion System")]
        public static void ConfigureAIEvasionSystem()
        {
            ConfigureAnimator();
            ConfigurePrefab(k_UndeadPrefabPath, false);
            ConfigurePrefab(k_BossPrefabPath, true);
            AssetDatabase.SaveAssets();
            ValidateAIEvasionSystem();
            Debug.Log(
                "[AIEvasionSystemSetup] Configured attack-aware AI Roll " +
                "decisions, root motion, and Boss evasion tuning.");
        }

        [MenuItem("Tools/Elden/Validate AI Evasion System")]
        public static void ValidateAIEvasionSystem()
        {
            ValidateRuntimeContracts();
            ValidateAnimator();
            ValidatePrefab(k_UndeadPrefabPath, false);
            ValidatePrefab(k_BossPrefabPath, true);
            Debug.Log(
                "[AIEvasionSystemValidation] EP118 AI Evasion configuration " +
                "is valid.");
        }

        private static void ConfigureAnimator()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AIControllerPath);
            AnimatorStateMachine stateMachine = GetActionStateMachine(controller);
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            AnimatorState rollState = stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == k_RollStateName) ??
                stateMachine.AddState(
                    k_RollStateName,
                    new Vector3(520f, 570f, 0f));
            rollState.motion = LoadRequiredAsset<AnimationClip>(k_RollClipPath);
            rollState.speed = 1f;
            rollState.writeDefaultValues = true;

            AnimatorStateTransition transition = rollState.transitions
                .FirstOrDefault(candidate =>
                    candidate.destinationState == emptyState) ??
                rollState.AddTransition(emptyState);
            transition.hasExitTime = true;
            transition.exitTime = 0.9f;
            transition.hasFixedDuration = true;
            transition.duration = 0.1f;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.canTransitionToSelf = false;
            transition.conditions = Array.Empty<AnimatorCondition>();

            EditorUtility.SetDirty(transition);
            EditorUtility.SetDirty(rollState);
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigurePrefab(
            string prefabPath,
            bool enableEvasion)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                AICharacterManager manager =
                    root.GetComponent<AICharacterManager>() ??
                    throw new InvalidOperationException(
                        $"{prefabPath} is missing AICharacterManager.");
                AICharacterCombatManager combatManager =
                    root.GetComponent<AICharacterCombatManager>() ??
                    throw new InvalidOperationException(
                        $"{prefabPath} is missing AICharacterCombatManager.");
                SerializedObject serializedManager = new SerializedObject(manager);
                serializedManager.FindProperty("m_canEvade").boolValue =
                    enableEvasion;
                serializedManager.FindProperty("m_percentageOfTimeWillEvade")
                    .floatValue = enableEvasion ? 35f : 0f;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedCombat =
                    new SerializedObject(combatManager);
                serializedCombat.FindProperty("m_maximumEvasionDistance")
                    .floatValue = 5f;
                serializedCombat.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
                EditorUtility.SetDirty(combatManager);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateRuntimeContracts()
        {
            const BindingFlags k_PublicInstance =
                BindingFlags.Public | BindingFlags.Instance;
            MethodInfo activateMethod = typeof(AICharacterManager).GetMethod(
                nameof(AICharacterManager.ActivateCharacter),
                k_PublicInstance);
            MethodInfo deactivateMethod = typeof(AICharacterManager).GetMethod(
                nameof(AICharacterManager.DeactivateCharacter),
                k_PublicInstance);
            MethodInfo evasionMethod = typeof(AICharacterCombatManager).GetMethod(
                nameof(AICharacterCombatManager.PerformEvasion),
                k_PublicInstance);
            if (activateMethod?.IsVirtual != true ||
                deactivateMethod?.IsVirtual != true ||
                evasionMethod?.IsVirtual != true ||
                typeof(CharacterSoundFXManager).GetMethod(
                    nameof(CharacterSoundFXManager.PlayRollingSoundFX)) == null)
            {
                throw new InvalidOperationException(
                    "AI Evasion runtime extension points are incomplete.");
            }
        }

        private static void ValidateAnimator()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_AIControllerPath);
            AnimatorStateMachine stateMachine = GetActionStateMachine(controller);
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            AnimatorState rollState = GetRequiredState(
                stateMachine,
                k_RollStateName);
            AnimationClip rollClip = LoadRequiredAsset<AnimationClip>(
                k_RollClipPath);
            if (rollState.motion != rollClip ||
                !rollState.transitions.Any(transition =>
                    transition.destinationState == emptyState &&
                    transition.hasExitTime))
            {
                throw new InvalidOperationException(
                    "AI Roll state or its Empty transition is incomplete.");
            }
        }

        private static void ValidatePrefab(
            string prefabPath,
            bool shouldEnableEvasion)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                AICharacterManager manager = root.GetComponent<AICharacterManager>();
                AICharacterCombatManager combatManager =
                    root.GetComponent<AICharacterCombatManager>();
                SerializedObject serializedManager = manager != null
                    ? new SerializedObject(manager)
                    : null;
                SerializedObject serializedCombat = combatManager != null
                    ? new SerializedObject(combatManager)
                    : null;
                if (serializedManager == null ||
                    serializedCombat == null ||
                    serializedManager.FindProperty("m_canEvade").boolValue !=
                        shouldEnableEvasion ||
                    serializedManager.FindProperty(
                        "m_percentageOfTimeWillEvade").floatValue < 0f ||
                    serializedCombat.FindProperty("m_maximumEvasionDistance")
                        .floatValue != 5f)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} has invalid AI Evasion tuning.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static AnimatorStateMachine GetActionStateMachine(
            AnimatorController controller)
        {
            return controller.layers
                .FirstOrDefault(layer => layer.name == k_ActionLayerName)
                ?.stateMachine ??
                throw new InvalidOperationException(
                    $"Animator is missing {k_ActionLayerName}.");
        }

        private static AnimatorState GetRequiredState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName) ??
                throw new InvalidOperationException(
                    $"Animator is missing {stateName}.");
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
