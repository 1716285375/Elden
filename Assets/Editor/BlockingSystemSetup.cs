using System;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP55 blocked-damage foundation.</summary>
    public static class BlockingSystemSetup
    {
        private const int k_BlockedDamageEffectID = 2;
        private const float k_PhysicalAbsorption = 85f;
        private const float k_MagicAbsorption = 40f;
        private const float k_FireAbsorption = 35f;
        private const float k_LightningAbsorption = 25f;
        private const float k_HolyAbsorption = 35f;
        private const string k_PlayerPrefabPath =
            "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_EffectFolderPath = "Assets/Resources/Effects";
        private const string k_BlockedDamageEffectPath =
            k_EffectFolderPath + "/Take Blocked Damage Effect.asset";
        private const string k_BlockSoundFolder =
            "Assets/Art/Audio/SFX/Combat/";

        private static readonly string[] s_blockSoundPaths =
        {
            k_BlockSoundFolder + "SFX_Impact_Metal_Light_01.wav",
            k_BlockSoundFolder + "SFX_Impact_Metal_01.wav",
            k_BlockSoundFolder + "SFX_Impact_Metal_Medium_01.wav",
            k_BlockSoundFolder + "SFX_Impact_Metal_Large_01.wav",
            k_BlockSoundFolder + "SFX_Impact_Metal_Colossal_01.wav"
        };

        [MenuItem("Tools/Elden/Configure Blocking System Pt.1")]
        public static void ConfigureBlockingSystem()
        {
            EnsureAssetFolder(k_EffectFolderPath);
            ConfigureBlockedDamageEffect();
            ConfigurePlayerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateBlockingSystem();
            Debug.Log(
                "[BlockingSystemSetup] Configured synchronized block state, " +
                "direction checks, five-type absorption, intensity, and feedback.");
        }

        [MenuItem("Tools/Elden/Validate Blocking System Pt.1")]
        public static void ValidateBlockingSystem()
        {
            TakeBlockedDamageEffect effect =
                LoadRequiredAsset<TakeBlockedDamageEffect>(
                    k_BlockedDamageEffectPath);
            ValidateRuntimeContracts();
            ValidateDirections();
            ValidateDamageIntensity();
            ValidateAbsorption(effect);
            ValidatePlayerPrefab();
            ValidateResourceLookup(effect);
            Debug.Log(
                "[BlockingSystemValidation] Front/back checks, one-hit registry, " +
                "owner network state, absorption, intensity, and runtime copies are valid.");
        }

        private static void ConfigureBlockedDamageEffect()
        {
            TakeBlockedDamageEffect effect =
                AssetDatabase.LoadAssetAtPath<TakeBlockedDamageEffect>(
                    k_BlockedDamageEffectPath);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<TakeBlockedDamageEffect>();
                AssetDatabase.CreateAsset(effect, k_BlockedDamageEffectPath);
            }

            SerializedObject serializedEffect = new SerializedObject(effect);
            GetRequiredProperty(serializedEffect, "m_instantEffectId").intValue =
                k_BlockedDamageEffectID;
            SerializedProperty blockSounds = GetRequiredProperty(
                serializedEffect,
                "m_blockSounds");
            blockSounds.arraySize = s_blockSoundPaths.Length;
            for (int soundIndex = 0;
                soundIndex < s_blockSoundPaths.Length;
                soundIndex++)
            {
                blockSounds.GetArrayElementAtIndex(soundIndex)
                    .objectReferenceValue = LoadRequiredAsset<AudioClip>(
                        s_blockSoundPaths[soundIndex]);
            }

            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                CharacterStatsManager statsManager =
                    GetRequiredComponent<CharacterStatsManager>(root);
                SetFloat(
                    statsManager,
                    "m_blockingPhysicalAbsorption",
                    k_PhysicalAbsorption);
                SetFloat(
                    statsManager,
                    "m_blockingMagicAbsorption",
                    k_MagicAbsorption);
                SetFloat(
                    statsManager,
                    "m_blockingFireAbsorption",
                    k_FireAbsorption);
                SetFloat(
                    statsManager,
                    "m_blockingLightningAbsorption",
                    k_LightningAbsorption);
                SetFloat(
                    statsManager,
                    "m_blockingHolyAbsorption",
                    k_HolyAbsorption);
                EditorUtility.SetDirty(statsManager);
                if (PrefabUtility.SaveAsPrefabAsset(root, k_PlayerPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "Could not save Player blocking absorption values.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateRuntimeContracts()
        {
            BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;
            BindingFlags privateInstance =
                BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo checkForBlock = typeof(DamageCollider).GetMethod(
                nameof(DamageCollider.CheckForBlock),
                publicInstance);
            MethodInfo blockingDot = typeof(DamageCollider).GetMethod(
                "GetBlockingDotValues",
                privateInstance);
            MethodInfo damage = typeof(DamageCollider).GetMethod(
                "Damage",
                privateInstance,
                null,
                new[] { typeof(CharacterManager), typeof(Vector3), typeof(bool) },
                null);
            ParameterInfo blockedParameter = typeof(CharacterNetworkManager)
                .GetMethod(
                    nameof(CharacterNetworkManager.RequestCharacterDamageServerRpc),
                    publicInstance)
                ?.GetParameters()
                .FirstOrDefault(parameter => parameter.Name == "wasBlocked");
            if (checkForBlock == null ||
                blockingDot == null ||
                !blockingDot.IsVirtual ||
                damage == null ||
                !damage.IsVirtual ||
                blockedParameter?.ParameterType != typeof(bool) ||
                !typeof(TakeDamageEffect).IsAssignableFrom(
                    typeof(TakeBlockedDamageEffect)))
            {
                throw new InvalidOperationException(
                    "The blocked-damage collider and RPC contracts are incomplete.");
            }
        }

        private static void ValidateDirections()
        {
            float frontDot = DamageCollider.CalculateBlockingDot(
                Vector3.forward,
                Vector3.forward);
            float rearDot = DamageCollider.CalculateBlockingDot(
                Vector3.forward,
                Vector3.back);
            float angledDot = DamageCollider.CalculateBlockingDot(
                Vector3.forward,
                new Vector3(1f, 0f, 1f));
            if (frontDot <= 0.3f ||
                rearDot > 0.3f ||
                angledDot <= 0.3f)
            {
                throw new InvalidOperationException(
                    "Blocking direction checks must accept front arcs and reject rear hits.");
            }
        }

        private static void ValidateDamageIntensity()
        {
            (float PoiseDamage, DamageIntensity Expected)[] cases =
            {
                (0f, DamageIntensity.Ping),
                (9.99f, DamageIntensity.Ping),
                (10f, DamageIntensity.Light),
                (29.99f, DamageIntensity.Light),
                (30f, DamageIntensity.Medium),
                (69.99f, DamageIntensity.Medium),
                (70f, DamageIntensity.Heavy),
                (119.99f, DamageIntensity.Heavy),
                (120f, DamageIntensity.Colossal)
            };
            foreach ((float poiseDamage, DamageIntensity expected) in cases)
            {
                DamageIntensity actual = TakeBlockedDamageEffect
                    .GetDamageIntensityBasedOnPoiseDamage(poiseDamage);
                if (actual != expected)
                {
                    throw new InvalidOperationException(
                        $"Poise damage {poiseDamage} resolved to {actual}, not {expected}.");
                }
            }
        }

        private static void ValidateAbsorption(TakeBlockedDamageEffect template)
        {
            TakeBlockedDamageEffect runtimeEffect =
                template.CreateRuntimeBlockedDamageEffect(
                    null,
                    100f,
                    50f,
                    40f,
                    20f,
                    10f,
                    Vector3.one,
                    70f,
                    k_PhysicalAbsorption,
                    k_MagicAbsorption,
                    k_FireAbsorption,
                    k_LightningAbsorption,
                    k_HolyAbsorption,
                    50f);
            TakeBlockedDamageEffect fullAbsorptionEffect =
                template.CreateRuntimeBlockedDamageEffect(
                    null,
                    100f,
                    0f,
                    0f,
                    0f,
                    0f,
                    Vector3.zero,
                    0f,
                    100f,
                    100f,
                    100f,
                    100f,
                    100f,
                    100f);
            try
            {
                const int k_ExpectedDamage = 92;
                if (runtimeEffect.CalculateBlockedDamage() != k_ExpectedDamage ||
                    runtimeEffect.DamageIntensity != DamageIntensity.Heavy ||
                    !runtimeEffect.WasBlocked ||
                    runtimeEffect.ContactPoint != Vector3.one ||
                    fullAbsorptionEffect.CalculateBlockedDamage() != 0 ||
                    template.FinalDamageDealt != 0 ||
                    template.WasBlocked ||
                    (runtimeEffect.hideFlags & HideFlags.DontSave) == 0)
                {
                    throw new InvalidOperationException(
                        "Blocked effects must absorb each type on a transient template copy.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeEffect);
                UnityEngine.Object.DestroyImmediate(fullAbsorptionEffect);
            }
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject player = LoadRequiredAsset<GameObject>(k_PlayerPrefabPath);
            CharacterNetworkManager networkManager =
                GetRequiredComponent<CharacterNetworkManager>(player);
            CharacterStatsManager statsManager =
                GetRequiredComponent<CharacterStatsManager>(player);
            if (networkManager.IsBlocking.ReadPerm !=
                    NetworkVariableReadPermission.Everyone ||
                networkManager.IsBlocking.WritePerm !=
                    NetworkVariableWritePermission.Owner ||
                networkManager.IsBlocking.Value ||
                !Mathf.Approximately(
                    statsManager.BlockingPhysicalAbsorption,
                    k_PhysicalAbsorption) ||
                !Mathf.Approximately(
                    statsManager.BlockingMagicAbsorption,
                    k_MagicAbsorption) ||
                !Mathf.Approximately(
                    statsManager.BlockingFireAbsorption,
                    k_FireAbsorption) ||
                !Mathf.Approximately(
                    statsManager.BlockingLightningAbsorption,
                    k_LightningAbsorption) ||
                !Mathf.Approximately(
                    statsManager.BlockingHolyAbsorption,
                    k_HolyAbsorption))
            {
                throw new InvalidOperationException(
                    "Player block state or absorption values are invalid.");
            }
        }

        private static void ValidateResourceLookup(TakeBlockedDamageEffect effect)
        {
            TakeBlockedDamageEffect resourceEffect =
                Resources.Load<TakeBlockedDamageEffect>(
                    "Effects/Take Blocked Damage Effect");
            SerializedProperty sounds = GetRequiredProperty(
                new SerializedObject(effect),
                "m_blockSounds");
            if (resourceEffect != effect ||
                sounds.arraySize != Enum.GetValues(typeof(DamageIntensity)).Length)
            {
                throw new InvalidOperationException(
                    "The world effects manager needs one block sound per intensity.");
            }

            for (int soundIndex = 0; soundIndex < sounds.arraySize; soundIndex++)
            {
                if (sounds.GetArrayElementAtIndex(soundIndex)
                        .objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"Block sound {soundIndex} is missing.");
                }
            }
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string currentPath = "Assets";
            string[] pathParts = folderPath.Split('/');
            for (int pathIndex = 1; pathIndex < pathParts.Length; pathIndex++)
            {
                string nextPath = $"{currentPath}/{pathParts[pathIndex]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, pathParts[pathIndex]);
                }

                currentPath = nextPath;
            }
        }

        private static void SetFloat(
            Component component,
            string propertyName,
            float value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} is missing " +
                    $"serialized property {propertyName}.");
        }

        private static T GetRequiredComponent<T>(GameObject gameObject)
            where T : Component
        {
            return gameObject.GetComponent<T>() ??
                throw new InvalidOperationException(
                    $"{gameObject.name} is missing {typeof(T).Name}.");
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
