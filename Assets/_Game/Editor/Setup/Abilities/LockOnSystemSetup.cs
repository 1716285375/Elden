using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ.Editor
{
    public static class LockOnSystemSetup
    {
        private const string k_PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_PlayerControlsPath = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_PlayerCameraMapName = "Player Camera";
        private const string k_LockOnActionName = "Lock On";
        private const string k_PlayerLayerName = "Player";
        private const string k_ObstructionLayerName = "Default";
        private const float k_LockOnRadius = 20f;
        private const float k_TargetHeightOffset = 1.5f;
        private const float k_OcclusionGracePeriod = 0.5f;

        [MenuItem("Tools/Elden/Configure Lock On System")]
        public static void ConfigureLockOnSystem()
        {
            ConfigurePlayerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateLockOnSystem();
            Debug.Log(
                "[LockOnSystemSetup] Configured player target detection, obstruction " +
                "handling, and lock-on input.");
        }

        [MenuItem("Tools/Elden/Validate Lock On System")]
        public static void ValidateLockOnSystem()
        {
            ValidatePlayerControls();
            ValidatePlayerPrefab();
            ValidateTargetSelection();
            Debug.Log(
                "[LockOnSystemValidation] Input bindings, player manager, nearest target, " +
                "and directional switching are valid.");
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                PlayerLockOnManager lockOnManager =
                    GetOrAddComponent<PlayerLockOnManager>(playerRoot);
                SerializedObject serializedManager = new SerializedObject(lockOnManager);
                GetRequiredProperty(serializedManager, "m_lockOnRadius").floatValue =
                    k_LockOnRadius;
                GetRequiredProperty(serializedManager, "m_characterLayers").intValue =
                    1 << GetRequiredLayer(k_PlayerLayerName);
                GetRequiredProperty(serializedManager, "m_obstructionLayers").intValue =
                    1 << GetRequiredLayer(k_ObstructionLayerName);
                GetRequiredProperty(serializedManager, "m_targetHeightOffset").floatValue =
                    k_TargetHeightOffset;
                GetRequiredProperty(serializedManager, "m_occlusionGracePeriod").floatValue =
                    k_OcclusionGracePeriod;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(lockOnManager);
                PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidatePlayerControls()
        {
            InputActionAsset playerControls = LoadRequiredAsset<InputActionAsset>(
                k_PlayerControlsPath);
            InputActionMap cameraMap = playerControls.FindActionMap(
                k_PlayerCameraMapName,
                true);
            InputAction lockOnAction = cameraMap.FindAction(k_LockOnActionName, true);
            bool hasGamepadBinding = false;
            bool hasKeyboardBinding = false;
            foreach (InputBinding binding in lockOnAction.bindings)
            {
                hasGamepadBinding |=
                    binding.path == "<Gamepad>/rightStickPress" &&
                    binding.groups.Contains("Gamepad");
                hasKeyboardBinding |=
                    binding.path == "<Keyboard>/tab" &&
                    binding.groups.Contains("Keyboard&Mouse");
            }

            if (lockOnAction.type != InputActionType.Button ||
                lockOnAction.expectedControlType != "Button" ||
                !hasGamepadBinding ||
                !hasKeyboardBinding)
            {
                throw new InvalidOperationException(
                    "Lock On must be a Button bound to right-stick press and keyboard Tab.");
            }
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                GetRequiredComponent<PlayerManager>(playerRoot);
                PlayerLockOnManager lockOnManager =
                    GetRequiredComponent<PlayerLockOnManager>(playerRoot);
                SerializedObject serializedManager = new SerializedObject(lockOnManager);
                int characterLayers = GetRequiredProperty(
                    serializedManager,
                    "m_characterLayers").intValue;
                int obstructionLayers = GetRequiredProperty(
                    serializedManager,
                    "m_obstructionLayers").intValue;
                if (!Mathf.Approximately(
                        GetRequiredProperty(serializedManager, "m_lockOnRadius").floatValue,
                        k_LockOnRadius) ||
                    characterLayers != 1 << GetRequiredLayer(k_PlayerLayerName) ||
                    obstructionLayers != 1 << GetRequiredLayer(k_ObstructionLayerName) ||
                    !Mathf.Approximately(
                        GetRequiredProperty(
                            serializedManager,
                            "m_targetHeightOffset").floatValue,
                        k_TargetHeightOffset) ||
                    !Mathf.Approximately(
                        GetRequiredProperty(
                            serializedManager,
                            "m_occlusionGracePeriod").floatValue,
                        k_OcclusionGracePeriod))
                {
                    throw new InvalidOperationException(
                        "Player lock-on detection settings are not configured.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateTargetSelection()
        {
            GameObject referenceObject = new GameObject("Lock On Direction Reference");
            CharacterManager currentTarget = CreateTestCharacter(
                "Lock On Current Target",
                new Vector3(0f, 0f, 5f));
            CharacterManager leftTarget = CreateTestCharacter(
                "Lock On Left Target",
                new Vector3(-2f, 0f, 6f));
            CharacterManager rightTarget = CreateTestCharacter(
                "Lock On Right Target",
                new Vector3(2f, 0f, 6f));

            try
            {
                List<CharacterManager> possibleTargets = new List<CharacterManager>
                {
                    leftTarget,
                    currentTarget,
                    rightTarget
                };
                if (PlayerLockOnTargetSelector.SelectClosestTarget(
                        possibleTargets,
                        Vector3.zero) != currentTarget ||
                    PlayerLockOnTargetSelector.SelectDirectionalTarget(
                        possibleTargets,
                        currentTarget,
                        referenceObject.transform,
                        Vector3.zero,
                        -1f) != leftTarget ||
                    PlayerLockOnTargetSelector.SelectDirectionalTarget(
                        possibleTargets,
                        currentTarget,
                        referenceObject.transform,
                        Vector3.zero,
                        1f) != rightTarget)
                {
                    throw new InvalidOperationException(
                        "Lock-on target selection does not respect distance and direction.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(referenceObject);
                UnityEngine.Object.DestroyImmediate(currentTarget.gameObject);
                UnityEngine.Object.DestroyImmediate(leftTarget.gameObject);
                UnityEngine.Object.DestroyImmediate(rightTarget.gameObject);
            }
        }

        private static CharacterManager CreateTestCharacter(
            string characterName,
            Vector3 position)
        {
            GameObject characterObject = new GameObject(characterName);
            characterObject.transform.position = position;
            characterObject.AddComponent<NetworkObject>();
            return characterObject.AddComponent<CharacterManager>();
        }

        private static int GetRequiredLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0
                ? layer
                : throw new InvalidOperationException(
                    $"Could not find the required '{layerName}' layer.");
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static T GetRequiredComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{gameObject.name} needs a {typeof(T).Name}.");
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
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
    }
}
