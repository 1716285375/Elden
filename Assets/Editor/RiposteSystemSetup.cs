using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP72 Riposte pipeline.</summary>
    public static class RiposteSystemSetup
    {
        private const string k_PlayerControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_AIControllerPath =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Undead AI Animator.controller";
        private const string k_RiposteClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/General/" +
            "core_main_riposte_01.anim";
        private const string k_UnarmedRiposteClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/General/" +
            "unarmed_main_riposte_01.anim";
        private const string k_MaceRiposteClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/Mace/" +
            "mace_main_riposte_01.anim";
        private const string k_RipostedClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Combat/General/" +
            "core_main_riposte_victim_01.anim";
        private const string k_GetUpClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Reactions/" +
            "core_main_faceup_getup_01.anim";
        private const string k_UnarmedOverridePath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Overrides/" +
            "Unarmed Animator.overrideController";
        private const string k_WeaponFolderPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons";
        private const string k_CriticalEffectPath =
            "Assets/Resources/Effects/Take Critical Damage Effect.asset";
        private const string k_MainMenuScenePath =
            WorldScenePathLayout.MainMenuScenePath;
        private const string k_CriticalStrikeSoundPath =
            "Assets/Art/Audio/SFX/General/SFX_Critical_Strike_01.wav";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_RiposteStateName = "Riposte_01";
        private const string k_RipostedStateName = "Riposted_01";
        private const string k_GetUpStateName = "Riposted_Get_Up_01";
        private const string k_ApplyDamageEventName = "ApplyCriticalDamage";
        private const float k_CriticalDamageEventTime = 0.7312914f;

        private static readonly string[] s_attackerClipPaths =
        {
            k_RiposteClipPath,
            k_UnarmedRiposteClipPath,
            k_MaceRiposteClipPath
        };

        [MenuItem("Tools/Elden/Configure Riposte System")]
        public static void ConfigureRiposteSystem()
        {
            TakeCriticalDamageEffect criticalEffect = ConfigureCriticalEffect();
            ConfigureAnimatorController(k_PlayerControllerPath, true);
            ConfigureAnimatorController(k_AIControllerPath, false);
            ConfigureAnimationEvents();
            ConfigureUnarmedOverride();
            ConfigureWeaponModifiers();
            ConfigureWorldManagers(criticalEffect);
            AssetDatabase.SaveAssets();
            ValidateRiposteSystem();
            Debug.Log(
                "[RiposteSystemSetup] Configured detection, server reservation, " +
                "paired animations, delayed damage, VFX, and SFX.");
        }

        [MenuItem("Tools/Elden/Validate Riposte System")]
        public static void ValidateRiposteSystem()
        {
            ValidateRuntimeContracts();
            ValidateAnimatorController(k_PlayerControllerPath, true);
            ValidateAnimatorController(k_AIControllerPath, false);
            ValidateAnimationEvents();
            ValidateUnarmedOverride();
            ValidateWeaponModifiers();
            ValidateWorldManagers();
            Debug.Log(
                "[RiposteSystemValidation] Critical entry, network payload, " +
                "death branch, alignment, and hit-frame settlement are valid.");
        }

        private static TakeCriticalDamageEffect ConfigureCriticalEffect()
        {
            TakeCriticalDamageEffect criticalEffect =
                AssetDatabase.LoadAssetAtPath<TakeCriticalDamageEffect>(
                    k_CriticalEffectPath);
            if (criticalEffect != null)
            {
                return criticalEffect;
            }

            criticalEffect = ScriptableObject.CreateInstance<TakeCriticalDamageEffect>();
            criticalEffect.name = "Take Critical Damage Effect";
            AssetDatabase.CreateAsset(criticalEffect, k_CriticalEffectPath);
            return criticalEffect;
        }

        private static void ConfigureAnimatorController(
            string controllerPath,
            bool includeAttackerState)
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                controllerPath);
            AnimatorControllerLayer actionLayer = GetRequiredLayer(controller);
            AnimatorStateMachine stateMachine = actionLayer.stateMachine;
            AnimatorState emptyState = GetRequiredState(
                stateMachine,
                k_EmptyStateName);
            EnsureBoolParameter(controller, "isDead");

            if (includeAttackerState)
            {
                AnimatorState riposteState = EnsureState(
                    stateMachine,
                    k_RiposteStateName,
                    LoadRequiredAsset<AnimationClip>(k_RiposteClipPath),
                    new Vector3(1260f, 250f));
                ConfigureTransition(
                    riposteState,
                    emptyState,
                    Array.Empty<AnimatorCondition>());
            }

            AnimatorState ripostedState = EnsureState(
                stateMachine,
                k_RipostedStateName,
                LoadRequiredAsset<AnimationClip>(k_RipostedClipPath),
                new Vector3(1260f, 340f));
            AnimatorState getUpState = EnsureState(
                stateMachine,
                k_GetUpStateName,
                LoadRequiredAsset<AnimationClip>(k_GetUpClipPath),
                new Vector3(1510f, 340f));
            ConfigureTransition(
                ripostedState,
                getUpState,
                new[]
                {
                    new AnimatorCondition
                    {
                        mode = AnimatorConditionMode.IfNot,
                        parameter = "isDead",
                        threshold = 0f
                    }
                });
            ConfigureTransition(
                getUpState,
                emptyState,
                Array.Empty<AnimatorCondition>());
            EditorUtility.SetDirty(controller);
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
                k_RipostedClipPath);
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
            AnimationClip baseRiposte = LoadRequiredAsset<AnimationClip>(
                k_RiposteClipPath);
            unarmedController[baseRiposte] = LoadRequiredAsset<AnimationClip>(
                k_UnarmedRiposteClipPath);
            EditorUtility.SetDirty(unarmedController);
        }

        private static void ConfigureWeaponModifiers()
        {
            string[] weaponGuids = AssetDatabase.FindAssets(
                "t:MeleeWeaponItem",
                new[] { k_WeaponFolderPath });
            foreach (string weaponGuid in weaponGuids)
            {
                MeleeWeaponItem weapon = AssetDatabase.LoadAssetAtPath<MeleeWeaponItem>(
                    AssetDatabase.GUIDToAssetPath(weaponGuid));
                SerializedObject serializedWeapon = new SerializedObject(weapon);
                serializedWeapon
                    .FindProperty("m_riposteAttack01Modifier")
                    .floatValue = 3.3f;
                serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
            }
        }

        private static void ConfigureWorldManagers(
            TakeCriticalDamageEffect criticalEffect)
        {
            Scene scene = GetSceneForEditing(k_MainMenuScenePath, out bool wasLoaded);
            try
            {
                WorldCharacterEffectsManager effectsManager =
                    FindComponentInScene<WorldCharacterEffectsManager>(scene) ??
                    throw new InvalidOperationException(
                        "Main Menu is missing WorldCharacterEffectsManager.");
                SerializedObject serializedEffects = new SerializedObject(
                    effectsManager);
                serializedEffects
                    .FindProperty("m_takeCriticalDamageEffect")
                    .objectReferenceValue = criticalEffect;
                SerializedProperty instantEffects = serializedEffects.FindProperty(
                    "m_instantEffects");
                AppendUniqueObjectReference(instantEffects, criticalEffect);
                serializedEffects.ApplyModifiedPropertiesWithoutUndo();

                WorldSoundFXManager soundManager =
                    FindComponentInScene<WorldSoundFXManager>(scene) ??
                    throw new InvalidOperationException(
                        "Main Menu is missing WorldSoundFXManager.");
                SerializedObject serializedSound = new SerializedObject(soundManager);
                serializedSound
                    .FindProperty("m_criticalStrikeSoundEffect")
                    .objectReferenceValue = LoadRequiredAsset<AudioClip>(
                        k_CriticalStrikeSoundPath);
                serializedSound.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(effectsManager);
                EditorUtility.SetDirty(soundManager);
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
            MethodInfo riposteRpc = typeof(CharacterNetworkManager).GetMethod(
                nameof(CharacterNetworkManager.NotifyServerOfRiposteServerRpc),
                k_PublicInstance);
            string[] parameterNames = riposteRpc?.GetParameters()
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
                    nameof(CharacterCombatManager.AttemptCriticalAttack),
                    k_PublicInstance) == null ||
                typeof(CharacterCombatManager).GetMethod(
                    nameof(CharacterCombatManager.ApplyCriticalDamage),
                    k_PublicInstance) == null ||
                typeof(TakeCriticalDamageEffect).GetMethod(
                    nameof(TakeCriticalDamageEffect
                        .CreateRuntimeCriticalDamageEffect),
                    k_PublicInstance) == null ||
                parameterNames == null ||
                !parameterNames.SequenceEqual(expectedNames))
            {
                throw new InvalidOperationException(
                    "The EP72 Critical runtime and RPC contracts are incomplete.");
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
            AnimatorState ripostedState = GetRequiredState(
                stateMachine,
                k_RipostedStateName);
            AnimatorState getUpState = GetRequiredState(
                stateMachine,
                k_GetUpStateName);
            bool hasAliveBranch = ripostedState.transitions.Any(transition =>
                transition.destinationState == getUpState &&
                transition.hasExitTime &&
                transition.conditions.Length == 1 &&
                transition.conditions[0].parameter == "isDead" &&
                transition.conditions[0].mode == AnimatorConditionMode.IfNot);
            bool getUpReturnsToEmpty = getUpState.transitions.Any(transition =>
                transition.destinationState == emptyState &&
                transition.hasExitTime);
            if (!hasAliveBranch || !getUpReturnsToEmpty)
            {
                throw new InvalidOperationException(
                    $"{controllerPath} has an invalid Riposted death branch.");
            }

            if (includeAttackerState)
            {
                AnimatorState riposteState = GetRequiredState(
                    stateMachine,
                    k_RiposteStateName);
                if (riposteState.motion !=
                    LoadRequiredAsset<AnimationClip>(k_RiposteClipPath))
                {
                    throw new InvalidOperationException(
                        "Riposte_01 must use the shared overridable clip.");
                }
            }
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
                        $"{attackerClipPath} contains obsolete Critical events.");
                }
            }

            AnimationEvent[] victimEvents = AnimationUtility.GetAnimationEvents(
                LoadRequiredAsset<AnimationClip>(k_RipostedClipPath));
            if (victimEvents.Length != 1 ||
                victimEvents[0].functionName != k_ApplyDamageEventName ||
                victimEvents[0].time <= 0f ||
                typeof(CharacterAnimatorManager).GetMethod(
                    k_ApplyDamageEventName,
                    BindingFlags.Public | BindingFlags.Instance) == null)
            {
                throw new InvalidOperationException(
                    "The Riposted clip must settle damage once on its hit frame.");
            }
        }

        private static void ValidateUnarmedOverride()
        {
            AnimatorOverrideController unarmedController =
                LoadRequiredAsset<AnimatorOverrideController>(
                    k_UnarmedOverridePath);
            AnimationClip baseRiposte = LoadRequiredAsset<AnimationClip>(
                k_RiposteClipPath);
            if (unarmedController[baseRiposte] !=
                LoadRequiredAsset<AnimationClip>(k_UnarmedRiposteClipPath))
            {
                throw new InvalidOperationException(
                    "The Unarmed controller does not override Riposte_01.");
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
                MeleeWeaponItem weapon = AssetDatabase.LoadAssetAtPath<MeleeWeaponItem>(
                    AssetDatabase.GUIDToAssetPath(weaponGuid));
                if (weapon == null || weapon.RiposteAttack01Modifier <= 0f)
                {
                    throw new InvalidOperationException(
                        "Every melee weapon needs a positive Riposte modifier.");
                }
            }
        }

        private static void ValidateWorldManagers()
        {
            TakeCriticalDamageEffect criticalEffect =
                LoadRequiredAsset<TakeCriticalDamageEffect>(k_CriticalEffectPath);
            Scene scene = GetSceneForEditing(k_MainMenuScenePath, out bool wasLoaded);
            try
            {
                WorldCharacterEffectsManager effectsManager =
                    FindComponentInScene<WorldCharacterEffectsManager>(scene);
                WorldSoundFXManager soundManager =
                    FindComponentInScene<WorldSoundFXManager>(scene);
                SerializedObject serializedEffects = effectsManager != null
                    ? new SerializedObject(effectsManager)
                    : null;
                SerializedObject serializedSound = soundManager != null
                    ? new SerializedObject(soundManager)
                    : null;
                if (serializedEffects == null ||
                    serializedEffects
                        .FindProperty("m_takeCriticalDamageEffect")
                        .objectReferenceValue != criticalEffect ||
                    serializedSound == null ||
                    serializedSound
                        .FindProperty("m_criticalStrikeSoundEffect")
                        .objectReferenceValue !=
                    LoadRequiredAsset<AudioClip>(k_CriticalStrikeSoundPath))
                {
                    throw new InvalidOperationException(
                        "Persistent managers are missing Critical effect data.");
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

        private static void AppendUniqueObjectReference(
            SerializedProperty arrayProperty,
            UnityEngine.Object objectReference)
        {
            for (int index = 0; index < arrayProperty.arraySize; index++)
            {
                if (arrayProperty
                        .GetArrayElementAtIndex(index)
                        .objectReferenceValue == objectReference)
                {
                    return;
                }
            }

            int newIndex = arrayProperty.arraySize;
            arrayProperty.InsertArrayElementAtIndex(newIndex);
            arrayProperty
                .GetArrayElementAtIndex(newIndex)
                .objectReferenceValue = objectReference;
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
