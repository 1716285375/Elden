using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP56 weapon-specific Animator controllers.</summary>
    public static class WeaponAnimatorControllerSetup
    {
        private const string k_BaseControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Base/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_OutputFolder =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides";
        private const string k_UnarmedControllerPath =
            k_OutputFolder + "/Unarmed Animator.overrideController";
        private const string k_StraightSwordControllerPath =
            k_OutputFolder + "/Straight Sword Animator.overrideController";
        private const string k_BroadswordControllerPath =
            k_OutputFolder + "/Broadsword Animator.overrideController";
        private const string k_UnarmedWeaponPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Unarmed.asset";
        private const string k_StraightSwordWeaponPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset";
        private const string k_BroadswordWeaponPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Broadsword.asset";
        private const string k_SwordCombatFolder =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Sword/";
        private const string k_UnarmedCombatFolder =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/General/";
        private const string k_LocomotionFolder =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Locomotion/";

        private static readonly ClipOverrideDefinition[] s_unarmedOverrides =
        {
            new ClipOverrideDefinition(
                k_SwordCombatFolder + "straight_sword_main_light_attack_01.anim",
                k_UnarmedCombatFolder + "unarmed_main_light_attack_01.anim"),
            new ClipOverrideDefinition(
                k_SwordCombatFolder + "straight_sword_main_light_attack_02.anim",
                k_UnarmedCombatFolder + "unarmed_main_light_attack_02.anim"),
            new ClipOverrideDefinition(
                k_SwordCombatFolder + "straight_sword_main_charged_attack_01_charge.anim",
                k_UnarmedCombatFolder + "unarmed_main_charged_attack_01_charge.anim"),
            new ClipOverrideDefinition(
                k_SwordCombatFolder + "straight_sword_main_charged_attack_01_hold.anim",
                k_UnarmedCombatFolder + "unarmed_main_charged_attack_01_hold.anim"),
            new ClipOverrideDefinition(
                k_SwordCombatFolder + "straight_sword_main_charged_attack_01_release.anim",
                k_UnarmedCombatFolder + "unarmed_main_charged_attack_01_release.anim"),
            new ClipOverrideDefinition(
                k_SwordCombatFolder + "straight_sword_main_charged_attack_01_release_full.anim",
                k_UnarmedCombatFolder + "unarmed_main_charged_attack_01_full.anim"),
            new ClipOverrideDefinition(
                k_SwordCombatFolder + "straight_sword_main_charged_attack_02_release.anim",
                k_UnarmedCombatFolder + "unarmed_main_charged_attack_02_release.anim"),
            new ClipOverrideDefinition(
                k_SwordCombatFolder + "straight_sword_main_back_step_attack_01_release.anim",
                k_UnarmedCombatFolder + "unarmed_main_back_step_attack_01_release.anim"),
            new ClipOverrideDefinition(
                k_LocomotionFolder + "straight_sword_main_run_attack_01.anim",
                k_LocomotionFolder + "unarmed_main_run_attack_01.anim"),
            new ClipOverrideDefinition(
                k_LocomotionFolder + "straight_sword_main_roll_attack_01_release.anim",
                k_LocomotionFolder + "unarmed_main_roll_attack_01_release.anim"),
            new ClipOverrideDefinition(
                k_LocomotionFolder + "straight_sword_main_idle_01.anim",
                k_LocomotionFolder + "unarmed_main_idle_01.anim",
                false)
        };

        [MenuItem("Tools/Elden/Configure Weapon Animator Controllers")]
        public static void ConfigureWeaponAnimatorControllers()
        {
            EnsureFolder(k_OutputFolder);
            RuntimeAnimatorController baseController =
                LoadRequiredAsset<RuntimeAnimatorController>(k_BaseControllerPath);
            AnimatorOverrideController unarmedController =
                GetOrCreateController(k_UnarmedControllerPath, baseController);
            AnimatorOverrideController straightSwordController =
                GetOrCreateController(k_StraightSwordControllerPath, baseController);
            AnimatorOverrideController broadswordController =
                GetOrCreateController(k_BroadswordControllerPath, baseController);

            ConfigureUnarmedOverrides(unarmedController);
            AssignWeaponController(k_UnarmedWeaponPath, unarmedController);
            AssignWeaponController(k_StraightSwordWeaponPath, straightSwordController);
            AssignWeaponController(k_BroadswordWeaponPath, broadswordController);

            AssetDatabase.SaveAssets();
            ValidateWeaponAnimatorControllers();
            Debug.Log(
                "[WeaponAnimatorControllerSetup] Configured distinct weapon controllers, " +
                "unarmed motion overrides, animation events, and hand pivots.");
        }

        [MenuItem("Tools/Elden/Validate Weapon Animator Controllers")]
        public static void ValidateWeaponAnimatorControllers()
        {
            RuntimeAnimatorController baseController =
                LoadRequiredAsset<RuntimeAnimatorController>(k_BaseControllerPath);
            AnimatorOverrideController unarmedController =
                LoadRequiredAsset<AnimatorOverrideController>(k_UnarmedControllerPath);
            AnimatorOverrideController straightSwordController =
                LoadRequiredAsset<AnimatorOverrideController>(k_StraightSwordControllerPath);
            AnimatorOverrideController broadswordController =
                LoadRequiredAsset<AnimatorOverrideController>(k_BroadswordControllerPath);

            ValidateControllerBase(unarmedController, baseController);
            ValidateControllerBase(straightSwordController, baseController);
            ValidateControllerBase(broadswordController, baseController);
            ValidateDistinctControllers(
                unarmedController,
                straightSwordController,
                broadswordController);
            ValidateUnarmedOverrides(unarmedController);
            ValidateWeaponAssignment(k_UnarmedWeaponPath, unarmedController);
            ValidateWeaponAssignment(k_StraightSwordWeaponPath, straightSwordController);
            ValidateWeaponAssignment(k_BroadswordWeaponPath, broadswordController);
            ValidateRuntimeArchitecture();
            ValidateNetworkContract();
            Debug.Log(
                "[WeaponAnimatorControllerValidation] Weapon data, override mappings, " +
                "event timing, runtime switching, and owner-driven replication are valid.");
        }

        private static AnimatorOverrideController GetOrCreateController(
            string assetPath,
            RuntimeAnimatorController baseController)
        {
            AnimatorOverrideController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(assetPath);
            if (controller == null)
            {
                controller = new AnimatorOverrideController(baseController);
                AssetDatabase.CreateAsset(controller, assetPath);
            }
            else
            {
                controller.runtimeAnimatorController = baseController;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigureUnarmedOverrides(
            AnimatorOverrideController controller)
        {
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides =
                new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);

            foreach (ClipOverrideDefinition definition in s_unarmedOverrides)
            {
                AnimationClip sourceClip = LoadRequiredAsset<AnimationClip>(
                    definition.SourcePath);
                AnimationClip targetClip = LoadRequiredAsset<AnimationClip>(
                    definition.TargetPath);
                int overrideIndex = overrides.FindIndex(pair => pair.Key == sourceClip);
                if (overrideIndex < 0)
                {
                    if (definition.IsRequired)
                    {
                        throw new InvalidOperationException(
                            $"Base controller does not use {definition.SourcePath}.");
                    }

                    continue;
                }

                overrides[overrideIndex] =
                    new KeyValuePair<AnimationClip, AnimationClip>(sourceClip, targetClip);
                CopyAnimationEvents(sourceClip, targetClip);
            }

            controller.ApplyOverrides(overrides);
            EditorUtility.SetDirty(controller);
        }

        private static void CopyAnimationEvents(
            AnimationClip sourceClip,
            AnimationClip targetClip)
        {
            float sourceLength = Mathf.Max(sourceClip.length, Mathf.Epsilon);
            AnimationEvent[] copiedEvents = AnimationUtility
                .GetAnimationEvents(sourceClip)
                .Select(sourceEvent => new AnimationEvent
                {
                    functionName = sourceEvent.functionName,
                    time = Mathf.Clamp01(sourceEvent.time / sourceLength) * targetClip.length,
                    stringParameter = sourceEvent.stringParameter,
                    floatParameter = sourceEvent.floatParameter,
                    intParameter = sourceEvent.intParameter,
                    objectReferenceParameter = sourceEvent.objectReferenceParameter,
                    messageOptions = sourceEvent.messageOptions
                })
                .OrderBy(animationEvent => animationEvent.time)
                .ToArray();
            AnimationUtility.SetAnimationEvents(targetClip, copiedEvents);
            EditorUtility.SetDirty(targetClip);
        }

        private static void AssignWeaponController(
            string weaponPath,
            AnimatorOverrideController controller)
        {
            WeaponItem weapon = LoadRequiredAsset<WeaponItem>(weaponPath);
            SerializedObject serializedWeapon = new SerializedObject(weapon);
            GetRequiredProperty(serializedWeapon, "m_weaponAnimator")
                .objectReferenceValue = controller;
            GetRequiredProperty(serializedWeapon, "m_weaponPivotPosition")
                .vector3Value = Vector3.zero;
            GetRequiredProperty(serializedWeapon, "m_weaponPivotRotation")
                .vector3Value = Vector3.zero;
            GetRequiredProperty(serializedWeapon, "m_weaponPivotScale")
                .vector3Value = Vector3.one;
            serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
        }

        private static void ValidateControllerBase(
            AnimatorOverrideController controller,
            RuntimeAnimatorController expectedBaseController)
        {
            if (controller.runtimeAnimatorController != expectedBaseController)
            {
                throw new InvalidOperationException(
                    $"{controller.name} does not use the shared humanoid controller.");
            }
        }

        private static void ValidateDistinctControllers(
            params AnimatorOverrideController[] controllers)
        {
            if (controllers.Distinct().Count() != controllers.Length)
            {
                throw new InvalidOperationException(
                    "Every weapon needs an independently extensible override asset.");
            }
        }

        private static void ValidateUnarmedOverrides(
            AnimatorOverrideController controller)
        {
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides =
                new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            foreach (ClipOverrideDefinition definition in s_unarmedOverrides)
            {
                AnimationClip sourceClip = LoadRequiredAsset<AnimationClip>(
                    definition.SourcePath);
                AnimationClip targetClip = LoadRequiredAsset<AnimationClip>(
                    definition.TargetPath);
                KeyValuePair<AnimationClip, AnimationClip> configuredOverride =
                    overrides.FirstOrDefault(pair => pair.Key == sourceClip);
                if (configuredOverride.Key == null)
                {
                    if (definition.IsRequired)
                    {
                        throw new InvalidOperationException(
                            $"Base controller does not expose {definition.SourcePath}.");
                    }

                    continue;
                }

                if (configuredOverride.Value != targetClip)
                {
                    throw new InvalidOperationException(
                        $"Unarmed controller does not replace {sourceClip.name}.");
                }

                ValidateNormalizedEvents(sourceClip, targetClip);
            }
        }

        private static void ValidateNormalizedEvents(
            AnimationClip sourceClip,
            AnimationClip targetClip)
        {
            AnimationEvent[] sourceEvents = AnimationUtility.GetAnimationEvents(sourceClip);
            AnimationEvent[] targetEvents = AnimationUtility.GetAnimationEvents(targetClip);
            if (sourceEvents.Length != targetEvents.Length)
            {
                throw new InvalidOperationException(
                    $"{targetClip.name} does not preserve the source event set.");
            }

            float sourceLength = Mathf.Max(sourceClip.length, Mathf.Epsilon);
            float targetLength = Mathf.Max(targetClip.length, Mathf.Epsilon);
            for (int eventIndex = 0; eventIndex < sourceEvents.Length; eventIndex++)
            {
                float sourceNormalizedTime = sourceEvents[eventIndex].time / sourceLength;
                float targetNormalizedTime = targetEvents[eventIndex].time / targetLength;
                if (sourceEvents[eventIndex].functionName !=
                        targetEvents[eventIndex].functionName ||
                    !Mathf.Approximately(sourceNormalizedTime, targetNormalizedTime))
                {
                    throw new InvalidOperationException(
                        $"{targetClip.name} has invalid normalized event timing.");
                }
            }
        }

        private static void ValidateWeaponAssignment(
            string weaponPath,
            AnimatorOverrideController expectedController)
        {
            WeaponItem weapon = LoadRequiredAsset<WeaponItem>(weaponPath);
            if (weapon.WeaponAnimator != expectedController ||
                weapon.WeaponPivotScale == Vector3.zero)
            {
                throw new InvalidOperationException(
                    $"{weapon.name} has invalid Animator or hand-pivot data.");
            }
        }

        private static void ValidateRuntimeArchitecture()
        {
            BindingFlags publicInstance = BindingFlags.Public | BindingFlags.Instance;
            if (typeof(CharacterAnimatorManager).GetMethod(
                    nameof(CharacterAnimatorManager.UpdateAnimatorController),
                    publicInstance) == null ||
                typeof(PlayerInventoryManager).GetMethod(
                    nameof(PlayerInventoryManager.GetEquippedWeaponByID),
                    publicInstance) == null ||
                typeof(WeaponModelInstantiationSlot).GetMethod(
                    nameof(WeaponModelInstantiationSlot.LoadWeaponModel),
                    publicInstance) == null)
            {
                throw new InvalidOperationException(
                    "Weapon controller switching or pivot presentation is incomplete.");
            }
        }

        private static void ValidateNetworkContract()
        {
            PropertyInfo property = typeof(PlayerNetworkManager).GetProperty(
                nameof(PlayerNetworkManager.CurrentWeaponIDBeingUsed),
                BindingFlags.Public | BindingFlags.Instance) ??
                throw new InvalidOperationException(
                    "PlayerNetworkManager does not expose the current action weapon ID.");
            if (property.PropertyType != typeof(NetworkVariable<int>) ||
                typeof(PlayerNetworkManager).GetMethod(
                    "OnCurrentWeaponIDBeingUsedChanged",
                    BindingFlags.NonPublic | BindingFlags.Instance) == null)
            {
                throw new InvalidOperationException(
                    "Remote weapon Animator synchronization is incomplete.");
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
            {
                string nextPath = $"{currentPath}/{segments[segmentIndex]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[segmentIndex]);
                }

                currentPath = nextPath;
            }
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

        private readonly struct ClipOverrideDefinition
        {
            public ClipOverrideDefinition(
                string sourcePath,
                string targetPath,
                bool isRequired = true)
            {
                SourcePath = sourcePath;
                TargetPath = targetPath;
                IsRequired = isRequired;
            }

            public string SourcePath { get; }
            public string TargetPath { get; }
            public bool IsRequired { get; }
        }
    }
}
