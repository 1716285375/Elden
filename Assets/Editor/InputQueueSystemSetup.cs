using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP50 timestamped attack input queue.</summary>
    public static class InputQueueSystemSetup
    {
        private const float k_InputBufferDuration = 0.3f;
        private const string k_PlayerInputManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player Input Manager.prefab";
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

        private static readonly string[] s_bufferedAttackClipPaths =
        {
            k_LightAttack01ClipPath,
            k_LightAttack02ClipPath,
            k_HeavyAttack01ClipPath,
            k_ChargedAttack01ClipPath,
            k_HeavyAttack02ClipPath
        };

        [MenuItem("Tools/Elden/Configure Input Queue System")]
        public static void ConfigureInputQueueSystem()
        {
            ConfigureInputManagerPrefab();
            AssetDatabase.SaveAssets();
            ValidateInputQueueSystem();
            Debug.Log(
                "[InputQueueSystemSetup] Configured a timestamped 0.3-second Light/Heavy " +
                "queue consumed by authored combo-window close events.");
        }

        [MenuItem("Tools/Elden/Validate Input Queue System")]
        public static void ValidateInputQueueSystem()
        {
            ValidateAttackInputData();
            ValidateInputManagerPrefab();
            ValidateQueueArchitecture();
            ValidateBufferExpirationRule();
            ValidateAnimationWindows();
            ValidateAnimationEventFlow();
            Debug.Log(
                "[InputQueueSystemValidation] Queue ownership, timestamps, expiration, " +
                "window control, consumption, and reset behavior are valid.");
        }

        private static void ConfigureInputManagerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerInputManagerPrefabPath);
            try
            {
                PlayerInputManager inputManager =
                    root.GetComponent<PlayerInputManager>() ??
                    throw new InvalidOperationException(
                        "Player Input Manager prefab is missing PlayerInputManager.");
                SerializedObject serializedManager = new SerializedObject(inputManager);
                SerializedProperty bufferDuration = serializedManager.FindProperty(
                    "m_inputBufferDuration") ??
                    throw new InvalidOperationException(
                        "PlayerInputManager is missing m_inputBufferDuration.");
                bufferDuration.floatValue = k_InputBufferDuration;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(inputManager);

                if (PrefabUtility.SaveAsPrefabAsset(
                        root,
                        k_PlayerInputManagerPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the Player Input Manager prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateAttackInputData()
        {
            AttackInput lightInput = new AttackInput(AttackInputType.Light, 4.5f);
            AttackInput heavyInput = new AttackInput(AttackInputType.Heavy, 8f);
            if (lightInput.InputType != AttackInputType.Light ||
                !Mathf.Approximately(lightInput.Timestamp, 4.5f) ||
                heavyInput.InputType != AttackInputType.Heavy ||
                !Mathf.Approximately(heavyInput.Timestamp, 8f))
            {
                throw new InvalidOperationException(
                    "AttackInput must preserve its semantic and timestamp.");
            }
        }

        private static void ValidateInputManagerPrefab()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_PlayerInputManagerPrefabPath);
            PlayerInputManager inputManager =
                prefab.GetComponent<PlayerInputManager>() ??
                throw new InvalidOperationException(
                    "Player Input Manager prefab is missing PlayerInputManager.");
            SerializedProperty bufferDuration = new SerializedObject(inputManager)
                .FindProperty("m_inputBufferDuration") ??
                throw new InvalidOperationException(
                    "PlayerInputManager is missing its buffer duration.");
            if (!Mathf.Approximately(
                    bufferDuration.floatValue,
                    k_InputBufferDuration))
            {
                throw new InvalidOperationException(
                    "The attack input buffer duration must be 0.3 seconds.");
            }
        }

        private static void ValidateQueueArchitecture()
        {
            BindingFlags instanceFlags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo queueField = typeof(PlayerInputManager).GetField(
                "m_attackInputQueue",
                instanceFlags) ??
                throw new InvalidOperationException(
                    "PlayerInputManager is missing its attack input queue.");
            if (queueField.FieldType != typeof(Queue<AttackInput>) ||
                typeof(PlayerInputManager).GetMethod(
                    "TryQueueAttackInput",
                    instanceFlags) == null ||
                typeof(PlayerInputManager).GetMethod(
                    "TryDequeueAttackInput",
                    instanceFlags) == null ||
                typeof(PlayerInputManager).GetMethod(
                    "ClearAttackInputQueue",
                    instanceFlags) == null ||
                typeof(PlayerCombatManager).GetProperty(
                    "CanQueueNextAttack",
                    instanceFlags) == null ||
                typeof(PlayerCombatManager).GetMethod(
                    "CloseAttackInputQueueWindow",
                    instanceFlags) == null)
            {
                throw new InvalidOperationException(
                    "The input and combat layers do not expose the required queue flow.");
            }
        }

        private static void ValidateBufferExpirationRule()
        {
            MethodInfo expirationMethod = typeof(PlayerInputManager).GetMethod(
                "IsAttackInputExpired",
                BindingFlags.Static | BindingFlags.NonPublic) ??
                throw new InvalidOperationException(
                    "PlayerInputManager is missing timestamp expiration.");
            AttackInput attackInput = new AttackInput(AttackInputType.Light, 10f);
            bool validBeforeBoundary = (bool)expirationMethod.Invoke(
                null,
                new object[] { attackInput, 10.29f, k_InputBufferDuration });
            bool expiredAfterBoundary = (bool)expirationMethod.Invoke(
                null,
                new object[] { attackInput, 10.31f, k_InputBufferDuration });
            if (validBeforeBoundary || !expiredAfterBoundary)
            {
                throw new InvalidOperationException(
                    "Attack input expiration must discard only inputs older than 0.3 seconds.");
            }
        }

        private static void ValidateAnimationWindows()
        {
            foreach (string clipPath in s_bufferedAttackClipPaths)
            {
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(
                    LoadRequiredAsset<AnimationClip>(clipPath));
                AnimationEvent enableEvent = events.FirstOrDefault(animationEvent =>
                    animationEvent.functionName == "EnableCanDoCombo");
                AnimationEvent disableEvent = events.FirstOrDefault(animationEvent =>
                    animationEvent.functionName == "DisableCanDoCombo");
                if (enableEvent == null ||
                    disableEvent == null ||
                    enableEvent.time >= disableEvent.time)
                {
                    throw new InvalidOperationException(
                        $"{clipPath} has an invalid input queue window.");
                }
            }
        }

        private static void ValidateAnimationEventFlow()
        {
            BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;
            if (typeof(PlayerAnimatorManager).GetMethod(
                    "EnableCanDoCombo",
                    publicInstance) == null ||
                typeof(PlayerAnimatorManager).GetMethod(
                    "DisableCanDoCombo",
                    publicInstance) == null ||
                typeof(PlayerCombatManager).GetMethod(
                    "EnableCanCombo",
                    publicInstance) == null ||
                typeof(PlayerCombatManager).GetMethod(
                    "CloseAttackInputQueueWindow",
                    publicInstance) == null)
            {
                throw new InvalidOperationException(
                    "Animation events cannot open and consume the attack input queue.");
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
