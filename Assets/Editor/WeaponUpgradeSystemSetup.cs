using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the authored assets required by EP132-136.</summary>
    public static class WeaponUpgradeSystemSetup
    {
        private const string k_MaterialFolder =
            "Assets/Data/Items/Upgrade Materials";
        private const string k_SmallMaterialPath =
            k_MaterialFolder + "/Small Smithing Stone.asset";
        private const string k_MediumMaterialPath =
            k_MaterialFolder + "/Medium Smithing Stone.asset";
        private const string k_LargeMaterialPath =
            k_MaterialFolder + "/Large Smithing Stone.asset";
        private const string k_AnvilPrefabPath =
            "Assets/Data/Prefabs/World Objects/Weapon Upgrade Anvil.prefab";
        private const string k_MaterialPickupPath =
            "Assets/Data/Prefabs/Interactables/Small Smithing Stone Pickup.prefab";
        private const string k_BlacksmithDialogueFolder =
            "Assets/Data/Dialogue/Blacksmith";
        private const string k_BlacksmithDialoguePath =
            k_BlacksmithDialogueFolder + "/Blacksmith Stage 00.asset";
        private const string k_NamelessKnightPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Nameless Knight NPC.prefab";
        private const string k_BlacksmithPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Blacksmith NPC.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";
        private const string k_WorldAIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/World AI Manager.prefab";
        private const string k_WorldSaveManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/World Save Game Manager.prefab";
        private const string k_WorldItemDatabasePrefabPath =
            "Assets/Data/Prefabs/Word Managers/World Item Database.prefab";
        private const string k_NetworkPrefabsPath =
            "Assets/DefaultNetworkPrefabs.asset";
        private const string k_HoverSoundPath =
            "Assets/Art/Audio/UI/SFX_Menu_Sound_Hover_01.wav";
        private const string k_ConfirmSoundPath =
            "Assets/Art/Audio/UI/SFX_Menu_Sound_Press_01.wav";
        private const string k_UnableSoundPath =
            "Assets/Art/Audio/UI/SFX_Menu_Raise_Stat_01.wav";
        private const string k_BlacksmithVoicePath =
            "Assets/Art/Audio/Creatures/Jireh/SFX_Jireh_Line_02.wav";
        private const string k_BlacksmithFarewellPath =
            "Assets/Art/Audio/SFX/General/SFX_Luna_Line_Farewell_Wanderer.wav";

        private static readonly Color s_backgroundColor =
            new(0.015f, 0.015f, 0.015f, 0.92f);
        private static readonly Color s_panelColor =
            new(0.08f, 0.075f, 0.065f, 0.96f);
        private static readonly Color s_buttonColor =
            new(0.18f, 0.16f, 0.12f, 0.96f);
        private static readonly Color s_textColor =
            new(0.93f, 0.88f, 0.72f, 1f);

        [MenuItem("Tools/Elden/Configure Weapon Upgrade System")]
        public static void ConfigureWeaponUpgradeSystem()
        {
            EnsureFolder(k_MaterialFolder);
            EnsureFolder("Assets/Data/Prefabs/World Objects");
            EnsureFolder("Assets/Data/Prefabs/Interactables");
            EnsureFolder(k_BlacksmithDialogueFolder);

            UpgradeMaterial smallMaterial = ConfigureUpgradeMaterial(
                k_SmallMaterialPath,
                "Small Smithing Stone",
                "A common stone used to strengthen armaments up to +3.",
                UpgradeStone.Small,
                12);
            UpgradeMaterial mediumMaterial = ConfigureUpgradeMaterial(
                k_MediumMaterialPath,
                "Medium Smithing Stone",
                "A tempered stone used to strengthen armaments from +3 to +6.",
                UpgradeStone.Medium,
                1);
            UpgradeMaterial largeMaterial = ConfigureUpgradeMaterial(
                k_LargeMaterialPath,
                "Large Smithing Stone",
                "A rare stone used to strengthen armaments beyond +6.",
                UpgradeStone.Large,
                1);
            ConfigureItemDatabase(
                new[] { smallMaterial, mediumMaterial, largeMaterial });

            CharacterDialogue blacksmithDialogue = ConfigureBlacksmithDialogue();
            GameObject blacksmithPrefab = ConfigureBlacksmithPrefab();
            GameObject anvilPrefab = ConfigureAnvilPrefab();
            GameObject materialPickup = ConfigureMaterialPickup(smallMaterial);
            ConfigureSaveManager(blacksmithDialogue);
            ConfigurePlayerUI();
            ConfigureWorldSpawners(
                blacksmithPrefab,
                anvilPrefab,
                materialPickup);
            RegisterNetworkPrefab(blacksmithPrefab);
            RegisterNetworkPrefab(anvilPrefab);
            RegisterNetworkPrefab(materialPickup);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateWeaponUpgradeSystem();
            Debug.Log(
                "[WeaponUpgradeSystemSetup] Configured upgrade data, UI, " +
                "materials, Anvil, Blacksmith, persistence, and world spawners.");
        }

        [MenuItem("Tools/Elden/Validate Weapon Upgrade System")]
        public static void ValidateWeaponUpgradeSystem()
        {
            UpgradeMaterial small = LoadRequiredAsset<UpgradeMaterial>(
                k_SmallMaterialPath);
            UpgradeMaterial medium = LoadRequiredAsset<UpgradeMaterial>(
                k_MediumMaterialPath);
            UpgradeMaterial large = LoadRequiredAsset<UpgradeMaterial>(
                k_LargeMaterialPath);
            if (!small.IsStackable ||
                small.MaxItemAmount != 99 ||
                small.UpgradeStone != UpgradeStone.Small ||
                medium.UpgradeStone != UpgradeStone.Medium ||
                large.UpgradeStone != UpgradeStone.Large ||
                small.ItemID < 0 || medium.ItemID < 0 || large.ItemID < 0)
            {
                throw new InvalidOperationException(
                    "Upgrade material assets or stable catalog IDs are invalid.");
            }

            ValidateItemDatabase(small, medium, large);
            ValidatePlayerUI();
            ValidateReusableInteractable<AnvilInteractable>(k_AnvilPrefabPath);
            ValidateReusableInteractable<PickupItemInteractable>(
                k_MaterialPickupPath,
                false);
            ValidateBlacksmith();
            ValidateWorldSpawners();
            ValidateNetworkPrefab(k_BlacksmithPrefabPath);
            ValidateNetworkPrefab(k_AnvilPrefabPath);
            ValidateNetworkPrefab(k_MaterialPickupPath);
            Debug.Log(
                "[WeaponUpgradeSystemValidation] EP132-136 authored assets are valid.");
        }

        private static UpgradeMaterial ConfigureUpgradeMaterial(
            string assetPath,
            string itemName,
            string description,
            UpgradeStone upgradeStone,
            int startingAmount)
        {
            UpgradeMaterial material =
                AssetDatabase.LoadAssetAtPath<UpgradeMaterial>(assetPath);
            if (material == null)
            {
                material = ScriptableObject.CreateInstance<UpgradeMaterial>();
                AssetDatabase.CreateAsset(material, assetPath);
            }

            SerializedObject serializedMaterial = new(material);
            SetString(serializedMaterial, "m_itemName", itemName);
            SetString(serializedMaterial, "m_itemDescription", description);
            SetInteger(serializedMaterial, "m_maxItemAmount", 99);
            SetInteger(serializedMaterial, "m_currentItemAmount", startingAmount);
            GetRequiredProperty(serializedMaterial, "m_upgradeStone")
                .enumValueIndex = (int)upgradeStone;
            serializedMaterial.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureItemDatabase(
            IReadOnlyList<UpgradeMaterial> materials)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_WorldItemDatabasePrefabPath);
            try
            {
                WorldItemDatabase database =
                    GetRequiredComponent<WorldItemDatabase>(root);
                SerializedObject serializedDatabase = new(database);
                SerializedProperty allItems = GetRequiredProperty(
                    serializedDatabase,
                    "m_items");
                foreach (UpgradeMaterial material in materials)
                {
                    AppendUniqueObject(allItems, material);
                }

                SetObjectArray(
                    serializedDatabase,
                    "m_upgradeMaterials",
                    materials.Cast<UnityEngine.Object>().ToArray());
                serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
                SavePrefab(root, k_WorldItemDatabasePrefabPath);

                for (int itemIndex = 0; itemIndex < allItems.arraySize; itemIndex++)
                {
                    if (allItems.GetArrayElementAtIndex(itemIndex)
                            .objectReferenceValue is not Item item)
                    {
                        continue;
                    }

                    SerializedObject serializedItem = new(item);
                    SetInteger(serializedItem, "m_itemID", itemIndex);
                    serializedItem.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(item);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static CharacterDialogue ConfigureBlacksmithDialogue()
        {
            CharacterDialogue dialogue =
                AssetDatabase.LoadAssetAtPath<CharacterDialogue>(
                    k_BlacksmithDialoguePath);
            if (dialogue == null)
            {
                dialogue = ScriptableObject.CreateInstance<CharacterDialogue>();
                AssetDatabase.CreateAsset(dialogue, k_BlacksmithDialoguePath);
            }

            AudioClip voice = LoadRequiredAsset<AudioClip>(k_BlacksmithVoicePath);
            AudioClip farewell = LoadRequiredAsset<AudioClip>(
                k_BlacksmithFarewellPath);
            SerializedObject serializedDialogue = new(dialogue);
            SetInteger(serializedDialogue, "m_requiredStageID", 0);
            SetBoolean(serializedDialogue, "m_setStageAfterDialogue", false);
            SetInteger(serializedDialogue, "m_stageIDToSet", 0);
            GetRequiredProperty(serializedDialogue, "m_dialogueEndEvent")
                .enumValueIndex = (int)DialogueEndEvent.Blacksmith;
            SetStringArray(
                serializedDialogue,
                "m_greetingStrings",
                new[] { "Need an armament strengthened?" });
            SetObjectArray(
                serializedDialogue,
                "m_greetingAudioClips",
                new UnityEngine.Object[] { voice });
            SetStringArray(
                serializedDialogue,
                "m_dialogueStrings",
                new[] { "Bring me smithing stones, and I will temper your steel." });
            SetObjectArray(
                serializedDialogue,
                "m_dialogueAudioClips",
                new UnityEngine.Object[] { voice });
            SetStringArray(
                serializedDialogue,
                "m_farewellStrings",
                new[] { "Show me the armament." });
            SetObjectArray(
                serializedDialogue,
                "m_farewellAudioClips",
                new UnityEngine.Object[] { farewell });
            serializedDialogue.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dialogue);
            return dialogue;
        }

        private static GameObject ConfigureBlacksmithPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_BlacksmithPrefabPath) ==
                null &&
                !AssetDatabase.CopyAsset(
                    k_NamelessKnightPrefabPath,
                    k_BlacksmithPrefabPath))
            {
                throw new InvalidOperationException(
                    "Could not create the Blacksmith NPC prefab.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(
                k_BlacksmithPrefabPath);
            try
            {
                root.name = "Blacksmith NPC";
                AICharacterSoundFXManager soundFXManager = root
                    .GetComponentInChildren<AICharacterSoundFXManager>(true);
                SerializedObject serializedSoundFX = new(soundFXManager);
                GetRequiredProperty(serializedSoundFX, "m_characterDialogueID")
                    .enumValueIndex = (int)CharacterDialogueID.Blacksmith;
                SetObjectReference(
                    serializedSoundFX,
                    "m_interactableDialogueObject",
                    null);
                serializedSoundFX.ApplyModifiedPropertiesWithoutUndo();
                SavePrefab(root, k_BlacksmithPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return LoadRequiredAsset<GameObject>(k_BlacksmithPrefabPath);
        }

        private static GameObject ConfigureAnvilPrefab()
        {
            EditPrefab(
                k_AnvilPrefabPath,
                "Weapon Upgrade Anvil",
                root =>
                {
                    SetLayerRecursively(
                        root,
                        LayerMask.NameToLayer("Interactable"));
                    GetOrAddComponent<NetworkObject>(root);
                    Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(root);
                    rigidbody.isKinematic = true;
                    rigidbody.useGravity = false;
                    rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                    SphereCollider collider =
                        GetOrAddComponent<SphereCollider>(root);
                    collider.isTrigger = true;
                    collider.radius = 2.75f;
                    AnvilInteractable interactable =
                        GetOrAddComponent<AnvilInteractable>(root);
                    SerializedObject serializedInteractable = new(interactable);
                    SetString(
                        serializedInteractable,
                        "m_interactableText",
                        "Strengthen Armament");
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
                    ConfigureAnvilVisual(root.transform);
                });
            return LoadRequiredAsset<GameObject>(k_AnvilPrefabPath);
        }

        private static void ConfigureAnvilVisual(Transform root)
        {
            Transform visualRoot = root.Find("Anvil Visual");
            if (visualRoot == null)
            {
                visualRoot = new GameObject("Anvil Visual").transform;
                visualRoot.SetParent(root, false);
            }

            CreatePrimitiveVisual(
                visualRoot,
                "Base",
                new Vector3(0f, 0.45f, 0f),
                new Vector3(0.85f, 0.9f, 0.7f));
            CreatePrimitiveVisual(
                visualRoot,
                "Face",
                new Vector3(0f, 1.05f, 0f),
                new Vector3(1.75f, 0.32f, 0.72f));
            CreatePrimitiveVisual(
                visualRoot,
                "Horn",
                new Vector3(1.05f, 1.05f, 0f),
                new Vector3(0.75f, 0.2f, 0.45f));
        }

        private static GameObject ConfigureMaterialPickup(UpgradeMaterial material)
        {
            EditPrefab(
                k_MaterialPickupPath,
                "Small Smithing Stone Pickup",
                root =>
                {
                    SetLayerRecursively(
                        root,
                        LayerMask.NameToLayer("Interactable"));
                    GetOrAddComponent<NetworkObject>(root);
                    Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(root);
                    rigidbody.isKinematic = true;
                    rigidbody.useGravity = false;
                    rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                    SphereCollider collider =
                        GetOrAddComponent<SphereCollider>(root);
                    collider.isTrigger = true;
                    collider.radius = 1.5f;
                    PickupItemInteractable interactable =
                        GetOrAddComponent<PickupItemInteractable>(root);
                    SerializedObject serializedInteractable = new(interactable);
                    SetString(
                        serializedInteractable,
                        "m_interactableText",
                        "Pick Up Small Smithing Stone");
                    SetObjectReference(
                        serializedInteractable,
                        "m_interactableCollider",
                        collider);
                    SetBoolean(
                        serializedInteractable,
                        "m_hostOnlyInteractable",
                        true);
                    SetBoolean(
                        serializedInteractable,
                        "m_shouldDisableColliderAfterInteraction",
                        true);
                    GetRequiredProperty(serializedInteractable, "m_pickupType")
                        .enumValueIndex = (int)ItemPickupType.WorldSpawn;
                    SetInteger(serializedInteractable, "m_itemID", 1001);
                    SetBoolean(serializedInteractable, "m_hasBeenLooted", false);
                    SetObjectReference(
                        serializedInteractable,
                        "m_item",
                        material);
                    serializedInteractable.ApplyModifiedPropertiesWithoutUndo();
                    CreatePrimitiveVisual(
                        root.transform,
                        "Stone Visual",
                        new Vector3(0f, 0.35f, 0f),
                        new Vector3(0.45f, 0.7f, 0.4f));
                });
            return LoadRequiredAsset<GameObject>(k_MaterialPickupPath);
        }

        private static void ConfigureSaveManager(CharacterDialogue dialogue)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_WorldSaveManagerPrefabPath);
            try
            {
                WorldSaveGameManager manager =
                    GetRequiredComponent<WorldSaveGameManager>(root);
                SerializedObject serializedManager = new(manager);
                SetObjectArray(
                    serializedManager,
                    "m_blacksmithDialogues",
                    new UnityEngine.Object[] { dialogue });
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
                PlayerUIManager playerUIManager =
                    GetRequiredComponent<PlayerUIManager>(root);
                PlayerUIWeaponUpgradeManager upgradeManager =
                    GetOrAddComponent<PlayerUIWeaponUpgradeManager>(root);
                AudioSource audioSource = GetOrAddComponent<AudioSource>(root);
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 0f;

                RectTransform menuWindow = GetOrCreateRectTransform(
                    root.transform,
                    "Weapon Upgrade Menu");
                StretchToParent(menuWindow);
                Image background = GetOrAddComponent<Image>(menuWindow.gameObject);
                background.color = s_backgroundColor;

                RectTransform panel = GetOrCreateRectTransform(
                    menuWindow,
                    "Upgrade Panel");
                SetCenteredRect(panel, new Vector2(820f, 640f), Vector2.zero);
                GetOrAddComponent<Image>(panel.gameObject).color = s_panelColor;

                CreateText(
                    panel,
                    "Title",
                    "UPGRADE WEAPONS",
                    new Vector2(0f, 265f),
                    new Vector2(700f, 65f),
                    38f);
                Button rightWeaponButton = CreateButton(
                    panel,
                    "Right Weapon Button",
                    "RIGHT HAND",
                    new Vector2(-205f, 150f),
                    new Vector2(330f, 120f));
                Image rightWeaponIcon = CreateIcon(
                    rightWeaponButton.transform,
                    "Weapon Icon");
                Button leftWeaponButton = CreateButton(
                    panel,
                    "Left Weapon Button",
                    "LEFT HAND",
                    new Vector2(205f, 150f),
                    new Vector2(330f, 120f));
                Image leftWeaponIcon = CreateIcon(
                    leftWeaponButton.transform,
                    "Weapon Icon");
                TMP_Text weaponNameText = CreateText(
                    panel,
                    "Weapon Name",
                    "No Weapon Selected",
                    new Vector2(0f, 55f),
                    new Vector2(700f, 48f),
                    28f);
                TMP_Text upgradeLevelText = CreateText(
                    panel,
                    "Upgrade Level",
                    "Current Level: +0",
                    new Vector2(0f, 8f),
                    new Vector2(700f, 42f),
                    23f);
                TMP_Text currentMaterialsText = CreateText(
                    panel,
                    "Current Materials",
                    "Current Materials: 0",
                    new Vector2(0f, -65f),
                    new Vector2(700f, 42f),
                    23f);
                TMP_Text materialsRequiredText = CreateText(
                    panel,
                    "Materials Required",
                    "Materials Required: N/A",
                    new Vector2(0f, -112f),
                    new Vector2(700f, 42f),
                    23f);
                Button upgradeButton = CreateButton(
                    panel,
                    "Upgrade Button",
                    "STRENGTHEN ARMAMENT",
                    new Vector2(0f, -205f),
                    new Vector2(430f, 60f));
                Button returnButton = CreateButton(
                    panel,
                    "Return Button",
                    "RETURN",
                    new Vector2(0f, -275f),
                    new Vector2(260f, 48f));

                RectTransform confirmation = GetOrCreateRectTransform(
                    menuWindow,
                    "Confirm Upgrade Popup");
                SetCenteredRect(
                    confirmation,
                    new Vector2(560f, 260f),
                    Vector2.zero);
                GetOrAddComponent<Image>(confirmation.gameObject).color =
                    new Color(0.035f, 0.03f, 0.025f, 0.99f);
                TMP_Text confirmationText = CreateText(
                    confirmation,
                    "Confirmation Text",
                    "Strengthen this armament?",
                    new Vector2(0f, 48f),
                    new Vector2(500f, 80f),
                    25f);
                Button confirmButton = CreateButton(
                    confirmation,
                    "Confirm Button",
                    "CONFIRM",
                    new Vector2(-135f, -65f),
                    new Vector2(220f, 55f));
                Button cancelButton = CreateButton(
                    confirmation,
                    "Cancel Button",
                    "CANCEL",
                    new Vector2(135f, -65f),
                    new Vector2(220f, 55f));

                ClearPersistentListeners(rightWeaponButton);
                UnityEventTools.AddPersistentListener(
                    rightWeaponButton.onClick,
                    upgradeManager.SelectRightHandWeapon);
                ClearPersistentListeners(leftWeaponButton);
                UnityEventTools.AddPersistentListener(
                    leftWeaponButton.onClick,
                    upgradeManager.SelectLeftHandWeapon);
                ClearPersistentListeners(upgradeButton);
                UnityEventTools.AddPersistentListener(
                    upgradeButton.onClick,
                    upgradeManager.AttemptToUpgradeWeaponFromUI);
                ClearPersistentListeners(returnButton);
                UnityEventTools.AddPersistentListener(
                    returnButton.onClick,
                    upgradeManager.CloseMenu);
                ClearPersistentListeners(confirmButton);
                UnityEventTools.AddPersistentListener(
                    confirmButton.onClick,
                    upgradeManager.UpgradeWeaponFromUI);
                ClearPersistentListeners(cancelButton);
                UnityEventTools.AddPersistentListener(
                    cancelButton.onClick,
                    upgradeManager.CancelUpgradeWeapon);

                ConfigureCharacterMenuEntry(root, upgradeManager);

                SerializedObject serializedUpgradeManager = new(upgradeManager);
                SetObjectReference(
                    serializedUpgradeManager,
                    "m_menuWindow",
                    menuWindow.gameObject);
                SetObjectReference(serializedUpgradeManager, "m_rightWeaponButton", rightWeaponButton);
                SetObjectReference(serializedUpgradeManager, "m_leftWeaponButton", leftWeaponButton);
                SetObjectReference(serializedUpgradeManager, "m_rightWeaponIcon", rightWeaponIcon);
                SetObjectReference(serializedUpgradeManager, "m_leftWeaponIcon", leftWeaponIcon);
                SetObjectReference(serializedUpgradeManager, "m_weaponNameText", weaponNameText);
                SetObjectReference(serializedUpgradeManager, "m_upgradeLevelText", upgradeLevelText);
                SetObjectReference(serializedUpgradeManager, "m_currentMaterialsText", currentMaterialsText);
                SetObjectReference(serializedUpgradeManager, "m_materialsRequiredText", materialsRequiredText);
                SetObjectReference(serializedUpgradeManager, "m_upgradeButton", upgradeButton);
                SetObjectReference(serializedUpgradeManager, "m_confirmationPopup", confirmation.gameObject);
                SetObjectReference(serializedUpgradeManager, "m_confirmationText", confirmationText);
                SetObjectReference(serializedUpgradeManager, "m_confirmButton", confirmButton);
                SetObjectReference(serializedUpgradeManager, "m_cancelButton", cancelButton);
                serializedUpgradeManager.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedUIManager = new(playerUIManager);
                SetObjectReference(
                    serializedUIManager,
                    "m_playerUIWeaponUpgradeManager",
                    upgradeManager);
                SetObjectReference(serializedUIManager, "m_uiAudioSource", audioSource);
                SetObjectReference(
                    serializedUIManager,
                    "m_menuHoverSound",
                    LoadRequiredAsset<AudioClip>(k_HoverSoundPath));
                SetObjectReference(
                    serializedUIManager,
                    "m_menuConfirmSound",
                    LoadRequiredAsset<AudioClip>(k_ConfirmSoundPath));
                SetObjectReference(
                    serializedUIManager,
                    "m_unableToContinueSound",
                    LoadRequiredAsset<AudioClip>(k_UnableSoundPath));
                serializedUIManager.ApplyModifiedPropertiesWithoutUndo();

                confirmation.gameObject.SetActive(false);
                menuWindow.gameObject.SetActive(false);
                SavePrefab(root, k_PlayerUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureCharacterMenuEntry(
            GameObject root,
            PlayerUIWeaponUpgradeManager upgradeManager)
        {
            Transform characterMenu = FindDescendant(root.transform, "Character Menu");
            Transform equipmentButton = FindDescendant(
                characterMenu,
                "Equipment Button");
            if (characterMenu == null || equipmentButton == null)
            {
                throw new InvalidOperationException(
                    "Player UI prefab is missing the Character Menu Equipment Button.");
            }

            Transform upgradeButtonTransform = FindDescendant(
                characterMenu,
                "Upgrade Weapon Button");
            GameObject upgradeButtonObject;
            if (upgradeButtonTransform == null)
            {
                upgradeButtonObject = UnityEngine.Object.Instantiate(
                    equipmentButton.gameObject,
                    equipmentButton.parent);
                upgradeButtonObject.name = "Upgrade Weapon Button";
                upgradeButtonObject.transform.SetSiblingIndex(
                    equipmentButton.GetSiblingIndex() + 1);
            }
            else
            {
                upgradeButtonObject = upgradeButtonTransform.gameObject;
            }

            Button upgradeButton = GetRequiredComponent<Button>(
                upgradeButtonObject);
            TMP_Text label = upgradeButtonObject
                .GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "UPGRADE WEAPON";
            }

            GetOrAddComponent<PlayerUIHoverSound>(upgradeButtonObject);
            ClearPersistentListeners(upgradeButton);
            UnityEventTools.AddPersistentListener(
                upgradeButton.onClick,
                upgradeManager.OpenWeaponUpgradeMenu);
        }

        private static void ConfigureWorldSpawners(
            GameObject blacksmithPrefab,
            GameObject anvilPrefab,
            GameObject materialPickup)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_WorldAIManagerPrefabPath);
            try
            {
                ConfigureAICharacterSpawner(
                    root.transform,
                    "Blacksmith NPC Spawner",
                    blacksmithPrefab,
                    new Vector3(7f, 0.1f, 3f),
                    Quaternion.Euler(0f, 210f, 0f));
                ConfigureNetworkObjectSpawner(
                    root.transform,
                    "Weapon Upgrade Anvil Spawner",
                    anvilPrefab,
                    new Vector3(5f, 0.1f, 3f));
                ConfigureNetworkObjectSpawner(
                    root.transform,
                    "Small Smithing Stone Pickup Spawner",
                    materialPickup,
                    new Vector3(5f, 0.1f, 1f));
                SavePrefab(root, k_WorldAIManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureAICharacterSpawner(
            Transform parent,
            string spawnerName,
            GameObject characterPrefab,
            Vector3 position,
            Quaternion rotation)
        {
            Transform spawnerTransform = parent.Find(spawnerName);
            if (spawnerTransform == null)
            {
                spawnerTransform = new GameObject(spawnerName).transform;
                spawnerTransform.SetParent(parent, false);
            }

            spawnerTransform.localPosition = position;
            spawnerTransform.localRotation = rotation;
            AICharacterSpawner spawner =
                GetOrAddComponent<AICharacterSpawner>(spawnerTransform.gameObject);
            SerializedObject serializedSpawner = new(spawner);
            SetObjectReference(
                serializedSpawner,
                "m_characterGameObject",
                characterPrefab);
            SetInteger(serializedSpawner, "m_patrolPathID", 0);
            SetBoolean(serializedSpawner, "m_repeatPatrol", false);
            SetBoolean(serializedSpawner, "m_isSleeping", false);
            SetBoolean(serializedSpawner, "m_willInvestigateSound", false);
            SetInteger(serializedSpawner, "m_bossID", 0);
            SetBoolean(serializedSpawner, "m_manuallySetStats", true);
            GetRequiredProperty(serializedSpawner, "m_maximumHealth")
                .floatValue = 500f;
            GetRequiredProperty(serializedSpawner, "m_maximumStamina")
                .floatValue = 100f;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureNetworkObjectSpawner(
            Transform parent,
            string spawnerName,
            GameObject prefab,
            Vector3 position)
        {
            Transform spawnerTransform = parent.Find(spawnerName);
            if (spawnerTransform == null)
            {
                spawnerTransform = new GameObject(spawnerName).transform;
                spawnerTransform.SetParent(parent, false);
            }

            spawnerTransform.localPosition = position;
            spawnerTransform.localRotation = Quaternion.identity;
            WorldNetworkObjectSpawner spawner =
                GetOrAddComponent<WorldNetworkObjectSpawner>(
                    spawnerTransform.gameObject);
            SerializedObject serializedSpawner = new(spawner);
            SetObjectReference(
                serializedSpawner,
                "m_networkPrefab",
                GetRequiredComponent<NetworkObject>(prefab));
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterNetworkPrefab(GameObject prefab)
        {
            NetworkPrefabsList prefabsList =
                LoadRequiredAsset<NetworkPrefabsList>(k_NetworkPrefabsPath);
            SerializedObject serializedList = new(prefabsList);
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

        private static void ValidateItemDatabase(params UpgradeMaterial[] materials)
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_WorldItemDatabasePrefabPath);
            WorldItemDatabase database =
                GetRequiredComponent<WorldItemDatabase>(prefab);
            SerializedObject serializedDatabase = new(database);
            SerializedProperty upgradeMaterials = GetRequiredProperty(
                serializedDatabase,
                "m_upgradeMaterials");
            foreach (UpgradeMaterial material in materials)
            {
                int matches = CountObjectReferences(upgradeMaterials, material);
                if (matches != 1 || database.Items.Count(item => item == material) != 1)
                {
                    throw new InvalidOperationException(
                        $"{material.name} must be registered exactly once.");
                }
            }
        }

        private static void ValidatePlayerUI()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(k_PlayerUIPrefabPath);
            PlayerUIManager playerUIManager =
                GetRequiredComponent<PlayerUIManager>(prefab);
            PlayerUIWeaponUpgradeManager upgradeManager =
                GetRequiredComponent<PlayerUIWeaponUpgradeManager>(prefab);
            SerializedObject serializedUpgrade = new(upgradeManager);
            string[] requiredReferences =
            {
                "m_menuWindow",
                "m_rightWeaponButton",
                "m_leftWeaponButton",
                "m_rightWeaponIcon",
                "m_leftWeaponIcon",
                "m_currentMaterialsText",
                "m_materialsRequiredText",
                "m_confirmationPopup",
                "m_confirmButton",
                "m_cancelButton"
            };
            foreach (string propertyName in requiredReferences)
            {
                if (GetRequiredProperty(serializedUpgrade, propertyName)
                        .objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"Weapon Upgrade UI is missing {propertyName}.");
                }
            }

            AudioSource audioSource = playerUIManager.GetComponent<AudioSource>();
            if (audioSource == null ||
                !Mathf.Approximately(audioSource.spatialBlend, 0f) ||
                FindDescendant(prefab.transform, "Upgrade Weapon Button") == null)
            {
                throw new InvalidOperationException(
                    "Player UI upgrade entry or non-spatial audio is invalid.");
            }
        }

        private static void ValidateReusableInteractable<T>(
            string prefabPath,
            bool shouldBeReusable = true)
            where T : Interactable
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(prefabPath);
            T interactable = GetRequiredComponent<T>(prefab);
            if (prefab.GetComponent<NetworkObject>() == null ||
                interactable.InteractableCollider?.isTrigger != true)
            {
                throw new InvalidOperationException(
                    $"{prefab.name} requires a NetworkObject and trigger collider.");
            }

            if (!shouldBeReusable)
            {
                return;
            }

            SerializedObject serializedInteractable = new(interactable);
            if (GetRequiredProperty(
                    serializedInteractable,
                    "m_shouldDisableColliderAfterInteraction").boolValue)
            {
                throw new InvalidOperationException(
                    $"{prefab.name} must remain reusable after interaction.");
            }
        }

        private static void ValidateBlacksmith()
        {
            CharacterDialogue dialogue = LoadRequiredAsset<CharacterDialogue>(
                k_BlacksmithDialoguePath);
            GameObject blacksmith = LoadRequiredAsset<GameObject>(
                k_BlacksmithPrefabPath);
            AICharacterSoundFXManager soundFX = blacksmith
                .GetComponentInChildren<AICharacterSoundFXManager>(true);
            GameObject saveManagerPrefab = LoadRequiredAsset<GameObject>(
                k_WorldSaveManagerPrefabPath);
            WorldSaveGameManager saveManager =
                GetRequiredComponent<WorldSaveGameManager>(saveManagerPrefab);
            SerializedProperty dialogues = GetRequiredProperty(
                new SerializedObject(saveManager),
                "m_blacksmithDialogues");
            if (!dialogue.ValidateDialogueData(false) ||
                dialogue.DialogueEndEvent != DialogueEndEvent.Blacksmith ||
                soundFX?.CharacterDialogueID != CharacterDialogueID.Blacksmith ||
                CountObjectReferences(dialogues, dialogue) != 1)
            {
                throw new InvalidOperationException(
                    "Blacksmith dialogue, prefab identity, or save registration is invalid.");
            }
        }

        private static void ValidateWorldSpawners()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_WorldAIManagerPrefabPath);
            if (prefab.transform.Find("Blacksmith NPC Spawner") == null ||
                prefab.transform.Find("Weapon Upgrade Anvil Spawner") == null ||
                prefab.transform.Find("Small Smithing Stone Pickup Spawner") == null)
            {
                throw new InvalidOperationException(
                    "World AI Manager is missing one or more upgrade-system spawners.");
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

        private static Button CreateButton(
            Transform parent,
            string name,
            string labelText,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            RectTransform rectTransform = GetOrCreateRectTransform(parent, name);
            SetCenteredRect(rectTransform, size, anchoredPosition);
            Image image = GetOrAddComponent<Image>(rectTransform.gameObject);
            image.color = s_buttonColor;
            Button button = GetOrAddComponent<Button>(rectTransform.gameObject);
            button.targetGraphic = image;
            GetOrAddComponent<PlayerUIHoverSound>(rectTransform.gameObject);
            TMP_Text label = CreateText(
                rectTransform,
                "Label",
                labelText,
                Vector2.zero,
                size - new Vector2(20f, 10f),
                22f);
            label.raycastTarget = false;
            return button;
        }

        private static Image CreateIcon(Transform parent, string name)
        {
            RectTransform rectTransform = GetOrCreateRectTransform(parent, name);
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(18f, 0f);
            rectTransform.sizeDelta = new Vector2(78f, 78f);
            Image image = GetOrAddComponent<Image>(rectTransform.gameObject);
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string text,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize)
        {
            RectTransform rectTransform = GetOrCreateRectTransform(parent, name);
            SetCenteredRect(rectTransform, size, anchoredPosition);
            TextMeshProUGUI label =
                GetOrAddComponent<TextMeshProUGUI>(rectTransform.gameObject);
            label.text = text;
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.color = s_textColor;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static void CreatePrimitiveVisual(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale)
        {
            Transform existing = parent.Find(name);
            GameObject visual = existing != null
                ? existing.gameObject
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = localScale;
            visual.layer = parent.gameObject.layer;
            Collider primitiveCollider = visual.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(primitiveCollider);
            }
        }

        private static void ClearPersistentListeners(Button button)
        {
            for (int listenerIndex = button.onClick.GetPersistentEventCount() - 1;
                listenerIndex >= 0;
                listenerIndex--)
            {
                UnityEventTools.RemovePersistentListener(
                    button.onClick,
                    listenerIndex);
            }

            button.onClick.RemoveAllListeners();
        }

        private static void EditPrefab(
            string prefabPath,
            string rootName,
            Action<GameObject> configure)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
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
                GameObject root = new(rootName);
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

            GameObject child = new(childName, typeof(RectTransform));
            RectTransform rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            return root?.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == name);
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static void SetCenteredRect(
            RectTransform rectTransform,
            Vector2 size,
            Vector2 anchoredPosition)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.localScale = Vector3.one;
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

        private static void AppendUniqueObject(
            SerializedProperty array,
            UnityEngine.Object value)
        {
            if (CountObjectReferences(array, value) > 0)
            {
                return;
            }

            int index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            array.GetArrayElementAtIndex(index).objectReferenceValue = value;
        }

        private static int CountObjectReferences(
            SerializedProperty array,
            UnityEngine.Object value)
        {
            int matches = 0;
            for (int index = 0; index < array.arraySize; index++)
            {
                if (array.GetArrayElementAtIndex(index).objectReferenceValue == value)
                {
                    matches++;
                }
            }

            return matches;
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
