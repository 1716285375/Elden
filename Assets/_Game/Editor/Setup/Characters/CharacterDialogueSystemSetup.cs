using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the authored assets required by EP129-131.</summary>
    public static class CharacterDialogueSystemSetup
    {
        private const string k_DialogueFolder =
            "Assets/_Game/Data/Dialogue/Nameless Knight";
        private const string k_StageZeroPath =
            k_DialogueFolder + "/Nameless Knight Stage 00.asset";
        private const string k_StageFivePath =
            k_DialogueFolder + "/Nameless Knight Stage 05.asset";
        private const string k_DialogueInteractablePath =
            "Assets/_Game/Prefabs/World/Objects/Dialogue/" +
            "Dialogue Interactable.prefab";
        private const string k_UndeadPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_NamelessKnightPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Nameless Knight NPC.prefab";
        private const string k_WorldAIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/World AI Manager.prefab";
        private const string k_WorldSaveManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/World Save Game Manager.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_WorldScenePath =
            WorldScenePathLayout.MasterScenePath;
        private const string k_NetworkPrefabsPath =
            "Assets/_Game/Settings/Networking/DefaultNetworkPrefabs.asset";
        private const string k_GiantDialogueOnePath =
            "Assets/_Game/Audio/Creatures/Giant/SFX_Hill_Giant_Dialogue_01.wav";
        private const string k_GiantDialogueTwoPath =
            "Assets/_Game/Audio/Creatures/Giant/SFX_Hill_Giant_Dialogue_02.wav";
        private const string k_CoughingPath =
            "Assets/_Game/Audio/SFX/Characters/Voice/SFX_Rickart_Line_Coughing.wav";
        private const string k_FarewellPath =
            "Assets/_Game/Audio/SFX/General/SFX_Luna_Line_Farewell_Wanderer.wav";
        private const string k_DialoguePopupName = "Dialogue Popup";
        private const string k_DialogueSubtitleName = "Dialogue Subtitle";
        private const string k_DialogueSpawnerName =
            "Nameless Knight Dialogue NPC Spawner";

        private static readonly Color s_popupColor =
            new Color(0.025f, 0.025f, 0.025f, 0.78f);
        private static readonly Color s_textColor =
            new Color(0.92f, 0.88f, 0.76f, 1f);

        [MenuItem("Tools/Elden/Configure Character Dialogue System")]
        public static void ConfigureCharacterDialogueSystem()
        {
            EnsureFolder(k_DialogueFolder);
            EnsureFolder("Assets/_Game/Prefabs/World/Objects/Dialogue");

            AudioClip giantDialogueOne = LoadRequiredAsset<AudioClip>(
                k_GiantDialogueOnePath);
            AudioClip giantDialogueTwo = LoadRequiredAsset<AudioClip>(
                k_GiantDialogueTwoPath);
            AudioClip coughing = LoadRequiredAsset<AudioClip>(k_CoughingPath);
            AudioClip farewell = LoadRequiredAsset<AudioClip>(k_FarewellPath);

            CharacterDialogue stageZero = ConfigureDialogueAsset(
                k_StageZeroPath,
                0,
                true,
                5,
                new[]
                {
                    "Still breathing, wanderer?",
                    "Come closer. I have no blade left for you."
                },
                new[] { giantDialogueOne, giantDialogueTwo },
                new[]
                {
                    "The Ashen Crypt remembers every oath broken within it.",
                    "Rest at the grace before you descend. The dead do not tire."
                },
                new[] { giantDialogueTwo, coughing },
                new[] { "Go carefully, wanderer." },
                new[] { farewell });
            CharacterDialogue stageFive = ConfigureDialogueAsset(
                k_StageFivePath,
                5,
                false,
                5,
                new[] { "You returned. Then the crypt did not claim you." },
                new[] { giantDialogueOne },
                new[]
                {
                    "Keep what you learned below. Some oaths are safer unspoken."
                },
                new[] { giantDialogueTwo },
                new[] { "Our roads part here." },
                new[] { farewell });

            DialogueInteractable dialogueInteractable =
                ConfigureDialogueInteractablePrefab();
            GameObject namelessKnight = ConfigureNamelessKnightPrefab();
            ConfigureSaveManager(stageZero, stageFive);
            ConfigurePlayerUI();
            ConfigureWorldAIManager(dialogueInteractable, namelessKnight);
            RegisterNetworkPrefab(dialogueInteractable.gameObject);
            RegisterNetworkPrefab(namelessKnight);
            ConfigureWorldScene(dialogueInteractable, namelessKnight);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateCharacterDialogueSystem();
            Debug.Log(
                "[CharacterDialogueSystemSetup] Configured staged dialogue, " +
                "network interaction, subtitle UI, save progress, and NPC spawning.");
        }

        [MenuItem("Tools/Elden/Validate Character Dialogue System")]
        public static void ValidateCharacterDialogueSystem()
        {
            CharacterDialogue stageZero = LoadRequiredAsset<CharacterDialogue>(
                k_StageZeroPath);
            CharacterDialogue stageFive = LoadRequiredAsset<CharacterDialogue>(
                k_StageFivePath);
            if (!stageZero.ValidateDialogueData(false) ||
                !stageFive.ValidateDialogueData(false) ||
                stageZero.RequiredStageID != 0 ||
                !stageZero.SetStageAfterDialogue ||
                stageZero.StageIDToSet != 5 ||
                stageFive.RequiredStageID != 5)
            {
                throw new InvalidOperationException(
                    "Nameless Knight dialogue stages are invalid.");
            }

            ValidateDialogueInteractablePrefab();
            ValidateNamelessKnightPrefab();
            ValidateSaveManager(stageZero, stageFive);
            ValidatePlayerUI();
            ValidateWorldAIManager();
            ValidateNetworkPrefab(k_DialogueInteractablePath);
            ValidateNetworkPrefab(k_NamelessKnightPrefabPath);
            ValidateWorldScene();
            Debug.Log(
                "[CharacterDialogueSystemValidation] EP129-131 dialogue assets are valid.");
        }

        private static CharacterDialogue ConfigureDialogueAsset(
            string assetPath,
            int requiredStageID,
            bool setStageAfterDialogue,
            int stageIDToSet,
            IReadOnlyList<string> greetingStrings,
            IReadOnlyList<AudioClip> greetingClips,
            IReadOnlyList<string> dialogueStrings,
            IReadOnlyList<AudioClip> dialogueClips,
            IReadOnlyList<string> farewellStrings,
            IReadOnlyList<AudioClip> farewellClips)
        {
            CharacterDialogue dialogue =
                AssetDatabase.LoadAssetAtPath<CharacterDialogue>(assetPath);
            if (dialogue == null)
            {
                dialogue = ScriptableObject.CreateInstance<CharacterDialogue>();
                AssetDatabase.CreateAsset(dialogue, assetPath);
            }

            SerializedObject serializedDialogue = new SerializedObject(dialogue);
            SetInteger(serializedDialogue, "m_requiredStageID", requiredStageID);
            SetBoolean(
                serializedDialogue,
                "m_setStageAfterDialogue",
                setStageAfterDialogue);
            SetInteger(serializedDialogue, "m_stageIDToSet", stageIDToSet);
            SetStringArray(
                serializedDialogue,
                "m_greetingStrings",
                greetingStrings);
            SetObjectArray(
                serializedDialogue,
                "m_greetingAudioClips",
                greetingClips.Cast<UnityEngine.Object>().ToArray());
            SetStringArray(
                serializedDialogue,
                "m_dialogueStrings",
                dialogueStrings);
            SetObjectArray(
                serializedDialogue,
                "m_dialogueAudioClips",
                dialogueClips.Cast<UnityEngine.Object>().ToArray());
            SetStringArray(
                serializedDialogue,
                "m_farewellStrings",
                farewellStrings);
            SetObjectArray(
                serializedDialogue,
                "m_farewellAudioClips",
                farewellClips.Cast<UnityEngine.Object>().ToArray());
            serializedDialogue.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dialogue);
            return dialogue;
        }

        private static DialogueInteractable ConfigureDialogueInteractablePrefab()
        {
            EditPrefab(
                k_DialogueInteractablePath,
                "Dialogue Interactable",
                root =>
                {
                    SetLayerRecursively(root, LayerMask.NameToLayer("Interactable"));
                    GetOrAddComponent<NetworkObject>(root);
                    Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(root);
                    rigidbody.isKinematic = true;
                    rigidbody.useGravity = false;
                    rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                    SphereCollider collider = GetOrAddComponent<SphereCollider>(root);
                    collider.isTrigger = true;
                    collider.center = new Vector3(0f, 1f, 0f);
                    collider.radius = 2.5f;
                    DialogueInteractable interactable =
                        GetOrAddComponent<DialogueInteractable>(root);
                    SerializedObject serializedInteractable =
                        new SerializedObject(interactable);
                    SetString(serializedInteractable, "m_interactableText", "Talk");
                    SetObjectReference(
                        serializedInteractable,
                        "m_interactableCollider",
                        collider);
                    SetBoolean(
                        serializedInteractable,
                        "m_hostOnlyInteractable",
                        false);
                    SetBoolean(
                        serializedInteractable,
                        "m_shouldDisableColliderAfterInteraction",
                        false);
                    serializedInteractable.ApplyModifiedPropertiesWithoutUndo();
                });
            return LoadRequiredAsset<GameObject>(k_DialogueInteractablePath)
                .GetComponent<DialogueInteractable>();
        }

        private static GameObject ConfigureNamelessKnightPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    k_NamelessKnightPrefabPath) == null &&
                !AssetDatabase.CopyAsset(
                    k_UndeadPrefabPath,
                    k_NamelessKnightPrefabPath))
            {
                throw new InvalidOperationException(
                    "Could not create the Nameless Knight NPC prefab.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(
                k_NamelessKnightPrefabPath);
            try
            {
                root.name = "Nameless Knight NPC";
                AICharacterSoundFXManager soundFXManager =
                    root.GetComponentInChildren<AICharacterSoundFXManager>(true) ??
                    throw new InvalidOperationException(
                        "Nameless Knight NPC is missing AICharacterSoundFXManager.");
                SerializedObject serializedSoundFX =
                    new SerializedObject(soundFXManager);
                GetRequiredProperty(
                    serializedSoundFX,
                    "m_characterDialogueID").enumValueIndex =
                    (int)CharacterDialogueID.NamelessKnight;
                serializedSoundFX.ApplyModifiedPropertiesWithoutUndo();

                AICharacterManager aiCharacter =
                    GetRequiredComponent<AICharacterManager>(root);
                SerializedObject serializedCharacter =
                    new SerializedObject(aiCharacter);
                SetBoolean(
                    serializedCharacter,
                    "m_autoAcquireTargets",
                    false);
                serializedCharacter.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(
                        root,
                        k_NamelessKnightPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the Nameless Knight NPC prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return LoadRequiredAsset<GameObject>(k_NamelessKnightPrefabPath);
        }

        private static void ConfigureSaveManager(
            CharacterDialogue stageZero,
            CharacterDialogue stageFive)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_WorldSaveManagerPrefabPath);
            try
            {
                WorldSaveGameManager saveManager =
                    GetRequiredComponent<WorldSaveGameManager>(root);
                SerializedObject serializedManager =
                    new SerializedObject(saveManager);
                SetObjectArray(
                    serializedManager,
                    "m_namelessKnightDialogues",
                    new UnityEngine.Object[] { stageZero, stageFive });
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                SavePrefab(root, k_WorldSaveManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigurePlayerUI()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Canvas canvas = root.GetComponentInChildren<Canvas>(true) ??
                    throw new InvalidOperationException(
                        "Player UI Manager is missing its Canvas.");
                PlayerUIPopUpManager popupManager =
                    GetRequiredComponent<PlayerUIPopUpManager>(root);
                TMP_FontAsset font = root.GetComponentsInChildren<TMP_Text>(true)
                    .Select(text => text.font)
                    .FirstOrDefault(candidate => candidate != null) ??
                    throw new InvalidOperationException(
                        "Player UI Manager is missing a TMP font.");

                RectTransform popup = GetOrCreateRectTransform(
                    canvas.transform,
                    k_DialoguePopupName);
                popup.anchorMin = new Vector2(0.12f, 0f);
                popup.anchorMax = new Vector2(0.88f, 0f);
                popup.pivot = new Vector2(0.5f, 0f);
                popup.anchoredPosition = new Vector2(0f, 48f);
                popup.sizeDelta = new Vector2(0f, 104f);

                Image background = GetOrAddComponent<Image>(popup.gameObject);
                background.color = s_popupColor;
                background.raycastTarget = false;
                VerticalLayoutGroup layout =
                    GetOrAddComponent<VerticalLayoutGroup>(popup.gameObject);
                layout.padding = new RectOffset(30, 30, 16, 16);
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                ContentSizeFitter fitter =
                    GetOrAddComponent<ContentSizeFitter>(popup.gameObject);
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                RectTransform subtitleRect = GetOrCreateRectTransform(
                    popup,
                    k_DialogueSubtitleName);
                TextMeshProUGUI subtitle =
                    GetOrAddComponent<TextMeshProUGUI>(subtitleRect.gameObject);
                subtitle.font = font;
                subtitle.fontSize = 28f;
                subtitle.fontStyle = FontStyles.SmallCaps;
                subtitle.alignment = TextAlignmentOptions.Center;
                subtitle.color = s_textColor;
                subtitle.raycastTarget = false;
                subtitle.textWrappingMode = TextWrappingModes.Normal;
                subtitle.text = string.Empty;
                LayoutElement subtitleLayout =
                    GetOrAddComponent<LayoutElement>(subtitle.gameObject);
                subtitleLayout.minHeight = 56f;
                subtitleLayout.preferredHeight = 72f;

                SetLayerRecursively(popup.gameObject, canvas.gameObject.layer);
                SerializedObject serializedPopup =
                    new SerializedObject(popupManager);
                SetObjectReference(
                    serializedPopup,
                    "m_dialoguePopup",
                    popup.gameObject);
                SetObjectReference(
                    serializedPopup,
                    "m_dialogueSubtitleText",
                    subtitle);
                serializedPopup.ApplyModifiedPropertiesWithoutUndo();
                popup.gameObject.SetActive(false);
                SavePrefab(root, k_PlayerUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureWorldAIManager(
            DialogueInteractable dialogueInteractable,
            GameObject namelessKnight)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_WorldAIManagerPrefabPath);
            try
            {
                WorldAIManager manager = GetRequiredComponent<WorldAIManager>(root);
                SetDialoguePrefab(manager, dialogueInteractable);
                ConfigureDialogueSpawner(root.transform, namelessKnight);
                SavePrefab(root, k_WorldAIManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureDialogueSpawner(
            Transform managerRoot,
            GameObject namelessKnight)
        {
            Transform spawnerTransform = managerRoot.Find(k_DialogueSpawnerName);
            if (spawnerTransform == null)
            {
                spawnerTransform = new GameObject(k_DialogueSpawnerName).transform;
                spawnerTransform.SetParent(managerRoot, false);
            }

            spawnerTransform.localPosition = new Vector3(3f, 0.1f, 3f);
            spawnerTransform.localRotation = Quaternion.Euler(0f, 210f, 0f);
            AICharacterSpawner spawner =
                GetOrAddComponent<AICharacterSpawner>(spawnerTransform.gameObject);
            SerializedObject serializedSpawner = new SerializedObject(spawner);
            SetObjectReference(
                serializedSpawner,
                "m_characterGameObject",
                namelessKnight);
            SetInteger(serializedSpawner, "m_patrolPathID", 0);
            SetBoolean(serializedSpawner, "m_repeatPatrol", false);
            SetBoolean(serializedSpawner, "m_isSleeping", false);
            SetBoolean(serializedSpawner, "m_willInvestigateSound", false);
            SetInteger(serializedSpawner, "m_bossID", 0);
            SetBoolean(serializedSpawner, "m_manuallySetStats", true);
            GetRequiredProperty(
                serializedSpawner,
                "m_maximumHealth").floatValue = 500f;
            GetRequiredProperty(
                serializedSpawner,
                "m_maximumStamina").floatValue = 100f;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureWorldScene(
            DialogueInteractable dialogueInteractable,
            GameObject namelessKnight)
        {
            Scene scene = SceneManager.GetSceneByPath(k_WorldScenePath);
            bool openedForSetup = !scene.IsValid() || !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(
                    k_WorldScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                WorldAIManager manager = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<WorldAIManager>())
                    .FirstOrDefault(candidate => candidate != null);
                if (manager == null)
                {
                    GameObject managerPrefab = LoadRequiredAsset<GameObject>(
                        k_WorldAIManagerPrefabPath);
                    manager = ((GameObject)PrefabUtility.InstantiatePrefab(
                        managerPrefab,
                        scene)).GetComponent<WorldAIManager>();
                }

                SetDialoguePrefab(manager, dialogueInteractable);
                if (manager.transform.Find(k_DialogueSpawnerName) == null)
                {
                    ConfigureDialogueSpawner(manager.transform, namelessKnight);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        "Could not save the configured World Scene.");
                }
            }
            finally
            {
                if (openedForSetup)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void SetDialoguePrefab(
            WorldAIManager manager,
            DialogueInteractable dialogueInteractable)
        {
            SerializedObject serializedManager = new SerializedObject(manager);
            SetObjectReference(
                serializedManager,
                "m_dialogueInteractablePrefab",
                dialogueInteractable);
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterNetworkPrefab(GameObject prefab)
        {
            NetworkPrefabsList prefabsList =
                LoadRequiredAsset<NetworkPrefabsList>(k_NetworkPrefabsPath);
            SerializedObject serializedList = new SerializedObject(prefabsList);
            SerializedProperty entries = GetRequiredProperty(serializedList, "List");
            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                if (entries.GetArrayElementAtIndex(entryIndex)
                        .FindPropertyRelative("Prefab")?.objectReferenceValue == prefab)
                {
                    return;
                }
            }

            int newIndex = entries.arraySize;
            entries.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newEntry = entries.GetArrayElementAtIndex(newIndex);
            newEntry.FindPropertyRelative("Override").enumValueIndex = 0;
            newEntry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            newEntry.FindPropertyRelative("SourcePrefabToOverride")
                .objectReferenceValue = null;
            newEntry.FindPropertyRelative("SourceHashToOverride").longValue = 0;
            newEntry.FindPropertyRelative("OverridingTargetPrefab")
                .objectReferenceValue = null;
            serializedList.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefabsList);
        }

        private static void ValidateDialogueInteractablePrefab()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_DialogueInteractablePath);
            DialogueInteractable interactable =
                GetRequiredComponent<DialogueInteractable>(prefab);
            SphereCollider collider = GetRequiredComponent<SphereCollider>(prefab);
            Rigidbody rigidbody = GetRequiredComponent<Rigidbody>(prefab);
            if (prefab.layer != LayerMask.NameToLayer("Interactable") ||
                prefab.GetComponent<NetworkObject>() == null ||
                interactable.InteractableText != "Talk" ||
                interactable.IsHostOnlyInteractable ||
                !collider.isTrigger ||
                !Mathf.Approximately(collider.radius, 2.5f) ||
                !rigidbody.isKinematic ||
                rigidbody.constraints != RigidbodyConstraints.FreezeAll)
            {
                throw new InvalidOperationException(
                    "Dialogue Interactable prefab is invalid.");
            }
        }

        private static void ValidateNamelessKnightPrefab()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_NamelessKnightPrefabPath);
            AICharacterSoundFXManager soundFXManager =
                prefab.GetComponentInChildren<AICharacterSoundFXManager>(true) ??
                throw new InvalidOperationException(
                    "Nameless Knight NPC is missing AICharacterSoundFXManager.");
            AICharacterManager aiCharacter =
                GetRequiredComponent<AICharacterManager>(prefab);
            if (prefab.GetComponent<NetworkObject>() == null ||
                soundFXManager.CharacterDialogueID !=
                    CharacterDialogueID.NamelessKnight ||
                aiCharacter.AutoAcquireTargets)
            {
                throw new InvalidOperationException(
                    "Nameless Knight NPC is not configured as a passive dialogue NPC.");
            }
        }

        private static void ValidateSaveManager(
            CharacterDialogue stageZero,
            CharacterDialogue stageFive)
        {
            WorldSaveGameManager manager = LoadRequiredAsset<GameObject>(
                    k_WorldSaveManagerPrefabPath)
                .GetComponent<WorldSaveGameManager>();
            SerializedProperty dialogues = GetRequiredProperty(
                new SerializedObject(manager),
                "m_namelessKnightDialogues");
            if (dialogues.arraySize != 2 ||
                dialogues.GetArrayElementAtIndex(0).objectReferenceValue != stageZero ||
                dialogues.GetArrayElementAtIndex(1).objectReferenceValue != stageFive)
            {
                throw new InvalidOperationException(
                    "World Save Game Manager dialogue list is invalid.");
            }
        }

        private static void ValidatePlayerUI()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(k_PlayerUIPrefabPath);
            PlayerUIPopUpManager manager =
                GetRequiredComponent<PlayerUIPopUpManager>(prefab);
            SerializedObject serializedManager = new SerializedObject(manager);
            GameObject popup = GetRequiredProperty(
                    serializedManager,
                    "m_dialoguePopup")
                .objectReferenceValue as GameObject;
            TMP_Text subtitle = GetRequiredProperty(
                    serializedManager,
                    "m_dialogueSubtitleText")
                .objectReferenceValue as TMP_Text;
            RectTransform popupRect = popup?.GetComponent<RectTransform>();
            if (popup == null ||
                popup.activeSelf ||
                subtitle == null ||
                popup.GetComponent<ContentSizeFitter>() == null ||
                popup.GetComponent<VerticalLayoutGroup>() == null ||
                popupRect == null ||
                !Mathf.Approximately(popupRect.anchorMin.y, 0f) ||
                popupRect.anchoredPosition.y < 40f)
            {
                throw new InvalidOperationException(
                    "Bottom-center dialogue popup is invalid.");
            }
        }

        private static void ValidateWorldAIManager()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_WorldAIManagerPrefabPath);
            WorldAIManager manager = GetRequiredComponent<WorldAIManager>(prefab);
            AICharacterSpawner spawner = prefab.transform
                .Find(k_DialogueSpawnerName)
                ?.GetComponent<AICharacterSpawner>();
            if (manager.DialogueInteractablePrefab == null || spawner == null)
            {
                throw new InvalidOperationException(
                    "World AI Manager is missing dialogue spawning data.");
            }
        }

        private static void ValidateNetworkPrefab(string prefabPath)
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(prefabPath);
            NetworkPrefabsList prefabsList =
                LoadRequiredAsset<NetworkPrefabsList>(k_NetworkPrefabsPath);
            SerializedProperty entries = GetRequiredProperty(
                new SerializedObject(prefabsList),
                "List");
            int matches = 0;
            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                if (entries.GetArrayElementAtIndex(entryIndex)
                        .FindPropertyRelative("Prefab")?.objectReferenceValue == prefab)
                {
                    matches++;
                }
            }

            if (matches != 1)
            {
                throw new InvalidOperationException(
                    $"{prefab.name} must be registered exactly once for networking.");
            }
        }

        private static void ValidateWorldScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_WorldScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(
                    k_WorldScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                WorldAIManager[] managers = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<WorldAIManager>())
                    .Where(manager => manager != null)
                    .ToArray();
                if (managers.Length != 1 ||
                    managers[0].DialogueInteractablePrefab == null ||
                    managers[0].transform.Find(k_DialogueSpawnerName) == null)
                {
                    throw new InvalidOperationException(
                        "World Scene requires one configured World AI Manager.");
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

        private static void EditPrefab(
            string prefabPath,
            string rootName,
            Action<GameObject> configure)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    configure(root);
                    SavePrefab(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                return;
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject(rootName);
                SceneManager.MoveGameObjectToScene(root, previewScene);
                configure(root);
                SavePrefab(root, prefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static RectTransform GetOrCreateRectTransform(
            Transform parent,
            string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing as RectTransform ??
                    throw new InvalidOperationException(
                        $"{childName} must use a RectTransform.");
            }

            GameObject child = new GameObject(childName, typeof(RectTransform));
            RectTransform rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static T GetRequiredComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject?.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{gameObject?.name ?? "Object"} is missing {typeof(T).Name}.");
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

        private static void SetBoolean(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            GetRequiredProperty(serializedObject, propertyName).boolValue = value;
        }

        private static void SetInteger(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).intValue = value;
        }

        private static void SetString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            GetRequiredProperty(serializedObject, propertyName).stringValue = value;
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetRequiredProperty(serializedObject, propertyName)
                .objectReferenceValue = value;
        }

        private static void SetStringArray(
            SerializedObject serializedObject,
            string propertyName,
            IReadOnlyList<string> values)
        {
            SerializedProperty property = GetRequiredProperty(
                serializedObject,
                propertyName);
            property.arraySize = values.Count;
            for (int valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                property.GetArrayElementAtIndex(valueIndex).stringValue =
                    values[valueIndex];
            }
        }

        private static void SetObjectArray(
            SerializedObject serializedObject,
            string propertyName,
            IReadOnlyList<UnityEngine.Object> values)
        {
            SerializedProperty property = GetRequiredProperty(
                serializedObject,
                propertyName);
            property.arraySize = values.Count;
            for (int valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                property.GetArrayElementAtIndex(valueIndex).objectReferenceValue =
                    values[valueIndex];
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (layer < 0)
            {
                throw new InvalidOperationException(
                    "The Interactable layer is missing from the project.");
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        private static void SavePrefab(GameObject root, string prefabPath)
        {
            if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
            {
                throw new InvalidOperationException($"Could not save {prefabPath}.");
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
    }
}
