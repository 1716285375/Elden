using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>
    /// Configures and validates the EP41 weapon-whoosh and damage-grunt sound flow.
    /// </summary>
    public static class SoundEffectSystemSetup
    {
        private const string k_PlayerPrefabPath =
            "Assets/Data/Prefabs/Player.prefab";
        private const string k_AICharacterPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_MainMenuScenePath =
            "Assets/Scenes/Scene_Main_Menu_01.unity";
        private const string k_WeaponFolder =
            "Assets/Data/Items/Weapons/Melee Weapons";
        private const string k_CombatAudioFolder =
            "Assets/Art/Audio/SFX/Combat";
        private const string k_UndeadAudioFolder =
            "Assets/Art/Audio/Creatures/Undead";

        private static readonly SoundCollectionDefinition[] s_weaponSounds =
        {
            new SoundCollectionDefinition(
                $"{k_WeaponFolder}/Straight Sword.asset",
                "m_whooshes",
                BuildNumberedPaths(k_CombatAudioFolder, "SFX_Small_Whoosh_", 1, 4)),
            new SoundCollectionDefinition(
                $"{k_WeaponFolder}/Broadsword.asset",
                "m_whooshes",
                BuildNumberedPaths(k_CombatAudioFolder, "SFX_Heavy_Whoosh_", 1, 4)),
            new SoundCollectionDefinition(
                $"{k_WeaponFolder}/Unarmed.asset",
                "m_whooshes",
                BuildNumberedPaths(
                    k_CombatAudioFolder,
                    "SFX_Medium_Blunt_Whoosh_",
                    1,
                    6))
        };

        private static readonly string[] s_playerDamageGrunts =
            BuildNumberedPaths(k_CombatAudioFolder, "SFX_Male_01_Hit_", 1, 8);

        private static readonly string[] s_aiDamageGrunts =
        {
            $"{k_UndeadAudioFolder}/SFX_Zombie_Pain_Short_3.wav",
            $"{k_UndeadAudioFolder}/SFX_Zombie_Pain_Short_4.wav",
            $"{k_UndeadAudioFolder}/SFX_Zombie_Pain_Short_6.wav"
        };

        [MenuItem("Tools/Elden/Configure Sound Effects")]
        public static void ConfigureSoundEffects()
        {
            ConfigureWeaponWhooshes();
            ConfigureCharacterSoundEffects(
                k_PlayerPrefabPath,
                s_playerDamageGrunts,
                false);
            ConfigureCharacterSoundEffects(
                k_AICharacterPrefabPath,
                s_aiDamageGrunts,
                true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateSoundEffects();
            Debug.Log(
                "[SoundEffectSystemSetup] Configured weapon whooshes and " +
                "character damage grunts.");
        }

        [MenuItem("Tools/Elden/Validate Sound Effects")]
        public static void ValidateSoundEffects()
        {
            ValidateRuntimeContracts();
            foreach (SoundCollectionDefinition definition in s_weaponSounds)
            {
                ValidateSoundCollection(definition);
            }

            ValidateCharacterSoundEffects(
                k_PlayerPrefabPath,
                s_playerDamageGrunts,
                false);
            ValidateCharacterSoundEffects(
                k_AICharacterPrefabPath,
                s_aiDamageGrunts,
                true);
            ValidateWorldSoundManager();
            Debug.Log(
                "[SoundEffectSystemValidation] Whoosh selection, hand routing, " +
                "damage grunts, prefabs, and world manager are valid.");
        }

        private static void ConfigureWeaponWhooshes()
        {
            foreach (SoundCollectionDefinition definition in s_weaponSounds)
            {
                WeaponItem weapon = LoadRequiredAsset<WeaponItem>(
                    definition.AssetPath);
                SetAudioClipArray(
                    weapon,
                    definition.SerializedPropertyName,
                    definition.AudioPaths);
            }
        }

        private static void ConfigureCharacterSoundEffects(
            string prefabPath,
            string[] damageGruntPaths,
            bool createManager)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                CharacterSoundFXManager soundManager =
                    root.GetComponentInChildren<CharacterSoundFXManager>(true);
                if (soundManager == null && createManager)
                {
                    AICharacterAnimatorManager animatorManager =
                        root.GetComponentInChildren<AICharacterAnimatorManager>(true);
                    if (animatorManager == null)
                    {
                        throw new InvalidOperationException(
                            $"{prefabPath} is missing its AI Animator Manager.");
                    }

                    soundManager = animatorManager.gameObject
                        .AddComponent<CharacterSoundFXManager>();
                }

                if (soundManager == null)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} is missing a Character Sound FX Manager.");
                }

                AudioSource audioSource = soundManager.GetComponent<AudioSource>() ??
                    soundManager.gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = 22f;

                SerializedObject serializedManager = new SerializedObject(soundManager);
                SetObjectReference(
                    serializedManager,
                    "m_audioSource",
                    audioSource);
                SetAudioClipArray(
                    serializedManager,
                    "m_damageGrunts",
                    damageGruntPaths);
                serializedManager.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(soundManager);
                EditorUtility.SetDirty(audioSource);
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                {
                    throw new InvalidOperationException(
                        $"Could not save {prefabPath}.");
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
            MethodInfo playSoundMethod = typeof(WorldSoundFXManager).GetMethod(
                nameof(WorldSoundFXManager.PlaySoundEffect),
                publicInstance);
            MethodInfo playWhooshMethod = typeof(CharacterSoundFXManager).GetMethod(
                nameof(CharacterSoundFXManager.PlayWeaponWhoosh),
                publicInstance);
            MethodInfo playDamageGruntMethod =
                typeof(CharacterSoundFXManager).GetMethod(
                    nameof(CharacterSoundFXManager.PlayDamageGrunt),
                    publicInstance);
            if (playSoundMethod == null ||
                playWhooshMethod == null ||
                playDamageGruntMethod == null ||
                typeof(PlayerEquipmentManager).GetMethod(
                    nameof(PlayerEquipmentManager.OpenDamageCollider),
                    publicInstance) == null)
            {
                throw new InvalidOperationException(
                    "The EP41 sound-effect runtime contracts are incomplete.");
            }

            AudioClip testClip = AudioClip.Create(
                "EP41 Selection Validation",
                1,
                1,
                44100,
                false);
            try
            {
                AudioClip[] clips = { null, testClip, null };
                if (!WorldSoundFXManager.TrySelectRandomSoundEffect(
                        clips,
                        out AudioClip selectedClip) ||
                    selectedClip != testClip ||
                    WorldSoundFXManager.TrySelectRandomSoundEffect(
                        Array.Empty<AudioClip>(),
                        out _))
                {
                    throw new InvalidOperationException(
                        "Random sound selection must ignore null and empty entries.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testClip);
            }
        }

        private static void ValidateSoundCollection(
            SoundCollectionDefinition definition)
        {
            WeaponItem weapon = LoadRequiredAsset<WeaponItem>(definition.AssetPath);
            SerializedObject serializedWeapon = new SerializedObject(weapon);
            ValidateAudioClipArray(
                serializedWeapon,
                definition.SerializedPropertyName,
                definition.AudioPaths,
                definition.AssetPath);
        }

        private static void ValidateCharacterSoundEffects(
            string prefabPath,
            string[] damageGruntPaths,
            bool requireAIManager)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                CharacterSoundFXManager soundManager =
                    root.GetComponentInChildren<CharacterSoundFXManager>(true);
                AudioSource audioSource = soundManager?.GetComponent<AudioSource>();
                if (soundManager == null ||
                    audioSource == null ||
                    audioSource.playOnAwake ||
                    !Mathf.Approximately(audioSource.spatialBlend, 1f) ||
                    requireAIManager &&
                    root.GetComponentInChildren<AICharacterAnimatorManager>(true) == null)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} has invalid character sound components.");
                }

                SerializedObject serializedManager = new SerializedObject(soundManager);
                SerializedProperty audioSourceProperty = GetRequiredProperty(
                    serializedManager,
                    "m_audioSource");
                if (audioSourceProperty.objectReferenceValue != audioSource)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} does not reference its spatial Audio Source.");
                }

                ValidateAudioClipArray(
                    serializedManager,
                    "m_damageGrunts",
                    damageGruntPaths,
                    prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateWorldSoundManager()
        {
            Scene scene = SceneManager.GetSceneByPath(k_MainMenuScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(
                    k_MainMenuScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                WorldSoundFXManager manager = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<WorldSoundFXManager>(true))
                    .FirstOrDefault();
                if (manager == null || manager.RollingSoundFX == null)
                {
                    throw new InvalidOperationException(
                        "The main menu must bootstrap the persistent World Sound FX Manager.");
                }
            }
            finally
            {
                if (openedForValidation)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void SetAudioClipArray(
            UnityEngine.Object target,
            string propertyName,
            string[] audioPaths)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SetAudioClipArray(serializedObject, propertyName, audioPaths);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetAudioClipArray(
            SerializedObject serializedObject,
            string propertyName,
            string[] audioPaths)
        {
            SerializedProperty property = GetRequiredProperty(
                serializedObject,
                propertyName);
            property.arraySize = audioPaths.Length;
            for (int audioIndex = 0; audioIndex < audioPaths.Length; audioIndex++)
            {
                property.GetArrayElementAtIndex(audioIndex).objectReferenceValue =
                    LoadRequiredAsset<AudioClip>(audioPaths[audioIndex]);
            }
        }

        private static void ValidateAudioClipArray(
            SerializedObject serializedObject,
            string propertyName,
            string[] expectedPaths,
            string ownerPath)
        {
            SerializedProperty property = GetRequiredProperty(
                serializedObject,
                propertyName);
            if (property.arraySize != expectedPaths.Length)
            {
                throw new InvalidOperationException(
                    $"{ownerPath} has the wrong number of sounds in {propertyName}.");
            }

            for (int audioIndex = 0; audioIndex < expectedPaths.Length; audioIndex++)
            {
                AudioClip expectedClip = LoadRequiredAsset<AudioClip>(
                    expectedPaths[audioIndex]);
                if (property.GetArrayElementAtIndex(audioIndex)
                        .objectReferenceValue != expectedClip)
                {
                    throw new InvalidOperationException(
                        $"{ownerPath} has an invalid sound at index {audioIndex}.");
                }
            }
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetRequiredProperty(serializedObject, propertyName)
                .objectReferenceValue = value;
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

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private static string[] BuildNumberedPaths(
            string folder,
            string filePrefix,
            int firstNumber,
            int lastNumber)
        {
            return Enumerable.Range(firstNumber, lastNumber - firstNumber + 1)
                .Select(number => $"{folder}/{filePrefix}{number:00}.wav")
                .ToArray();
        }

        private readonly struct SoundCollectionDefinition
        {
            internal SoundCollectionDefinition(
                string assetPath,
                string serializedPropertyName,
                string[] audioPaths)
            {
                AssetPath = assetPath;
                SerializedPropertyName = serializedPropertyName;
                AudioPaths = audioPaths;
            }

            internal string AssetPath { get; }
            internal string SerializedPropertyName { get; }
            internal string[] AudioPaths { get; }
        }
    }
}
