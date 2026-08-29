using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP67 item pickup gameplay loop.</summary>
    public static class ItemPickupSystemSetup
    {
        private const string k_PlayerUIPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";
        private const string k_PickupPrefabPath =
            "Assets/Data/Prefabs/Interactables/Item Pickup.prefab";
        private const string k_PickupMaterialPath =
            "Assets/Data/Materials/Item Pickup Glow.mat";
        private const string k_MainMenuScenePath =
            WorldScenePathLayout.MainMenuScenePath;
        private const string k_WorldScenePath =
            WorldScenePathLayout.MasterScenePath;
        private const string k_PickupSoundPath =
            "Assets/Art/Audio/SFX/General/SFX_Pick_Up_Rare_Item_01.wav";
        private const string k_StraightSwordPath =
            "Assets/Data/Items/Weapons/Melee Weapons/Straight Sword.asset";
        private const string k_BroadswordPath =
            "Assets/Data/Items/Weapons/Melee Weapons/Broadsword.asset";
        private const string k_MediumShieldPath =
            "Assets/Data/Items/Weapons/Melee Weapons/Medium Shield.asset";
        private const string k_MediumShieldIconPath =
            "Assets/Art/Textures/UI/Items/Iron_Shield_Icon.png";
        private const string k_InteractableLayerName = "Interactable";
        private const string k_PlayerSpawnPointName = "Player Spawn Point";
        private const string k_PopupOrganizerName = "Popup Organizer";
        private const string k_MessagePopupName = "Player Message Popup";
        private const string k_ItemPopupName = "Item Pickup Popup";

        private static readonly string[] s_worldPickupNames =
        {
            "World Item Pickup 000",
            "World Item Pickup 001",
            "World Item Pickup 002"
        };

        private static readonly string[] s_worldItemPaths =
        {
            k_StraightSwordPath,
            k_BroadswordPath,
            k_MediumShieldPath
        };

        private static readonly Vector3[] s_worldPickupOffsets =
        {
            new Vector3(2f, 0.65f, 2f),
            new Vector3(-2f, 0.65f, 2f),
            new Vector3(0f, 0.65f, 4f)
        };

        private static readonly Color s_backgroundColor =
            new Color(0.015f, 0.01f, 0.008f, 0.9f);
        private static readonly Color s_borderColor =
            new Color(0.48f, 0.39f, 0.22f, 0.95f);
        private static readonly Color s_textColor =
            new Color(0.88f, 0.82f, 0.68f, 1f);
        private static readonly Color s_mutedTextColor =
            new Color(0.62f, 0.58f, 0.5f, 1f);

        [MenuItem("Tools/Elden/Configure Item Pickup System")]
        public static void ConfigureItemPickupSystem()
        {
            EnsureFolder("Assets/Data/Prefabs/Interactables");
            EnsureFolder("Assets/Data/Materials");
            ConfigureMediumShieldIcon();
            ConfigurePickupMaterial();
            ConfigurePickupPrefab();
            ConfigurePlayerUIPrefab();
            ConfigureWorldSoundManager();
            ConfigureWorldPickups();
            AssetDatabase.SaveAssets();
            ValidateItemPickupSystem();
            Debug.Log(
                "[ItemPickupSystemSetup] Configured persistent Host-owned world " +
                "loot, runtime inventory collection, popup feedback, and pickup audio.");
        }

        [MenuItem("Tools/Elden/Validate Item Pickup System")]
        public static void ValidateItemPickupSystem()
        {
            ValidateRuntimeContracts();
            ValidateSaveRoundTrip();
            ValidatePickupPrefab();
            ValidatePlayerUIPrefab();
            ValidateWorldSoundManager();
            ValidateWorldPickups();
            Debug.Log(
                "[ItemPickupSystemValidation] Save data, inventory boundary, Host " +
                "authority, pickup presentation, and world instances are valid.");
        }

        private static void ConfigureMediumShieldIcon()
        {
            TextureImporter importer = AssetImporter.GetAtPath(k_MediumShieldIconPath)
                as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "The Medium Shield icon source texture is missing.");
            }

            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            Sprite shieldIcon = LoadRequiredAsset<Sprite>(k_MediumShieldIconPath);
            Item shield = LoadRequiredAsset<Item>(k_MediumShieldPath);
            SerializedObject serializedShield = new SerializedObject(shield);
            SetObjectReference(serializedShield, "m_itemIcon", shieldIcon);
            serializedShield.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shield);
        }

        private static void ConfigurePickupMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                k_PickupMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find(
                    "Universal Render Pipeline/Particles/Unlit") ??
                    Shader.Find("Sprites/Default") ??
                    throw new InvalidOperationException(
                        "No compatible pickup presentation shader is available.");
                material = new Material(shader)
                {
                    name = "Item Pickup Glow"
                };
                AssetDatabase.CreateAsset(material, k_PickupMaterialPath);
            }

            Color glowColor = new Color(1f, 0.72f, 0.24f, 0.82f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", glowColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", glowColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", glowColor * 2.5f);
                material.EnableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
        }

        private static void ConfigurePickupPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_PickupPrefabPath) != null)
            {
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(
                    k_PickupPrefabPath);
                try
                {
                    ConfigurePickupObject(prefabRoot);
                    SavePrefab(prefabRoot, k_PickupPrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }

                return;
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("Item Pickup");
                SceneManager.MoveGameObjectToScene(root, previewScene);
                ConfigurePickupObject(root);
                SavePrefab(root, k_PickupPrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void ConfigurePickupObject(GameObject root)
        {
            int interactableLayer = LayerMask.NameToLayer(k_InteractableLayerName);
            if (interactableLayer < 0)
            {
                throw new InvalidOperationException(
                    "EP52 must configure the Interactable layer before EP67.");
            }

            root.name = "Item Pickup";
            root.layer = interactableLayer;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            GetOrAddComponent<NetworkObject>(root);
            Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(root);
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

            SphereCollider trigger = GetOrAddComponent<SphereCollider>(root);
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.45f, 0f);
            trigger.radius = 1.45f;
            trigger.enabled = true;

            ConfigurePickupPresentation(root.transform, interactableLayer);

            PickupItemInteractable pickup =
                GetOrAddComponent<PickupItemInteractable>(root);
            SerializedObject serializedPickup = new SerializedObject(pickup);
            SetString(serializedPickup, "m_interactableText", "Pick Up Item");
            SetObjectReference(
                serializedPickup,
                "m_interactableCollider",
                trigger);
            SetBoolean(serializedPickup, "m_hostOnlyInteractable", false);
            SetBoolean(
                serializedPickup,
                "m_shouldDisableColliderAfterInteraction",
                true);
            SetEnum(serializedPickup, "m_pickupType", ItemPickupType.CharacterDrop);
            SetInteger(serializedPickup, "m_itemID", -1);
            SetBoolean(serializedPickup, "m_hasBeenLooted", false);
            SetObjectReference(
                serializedPickup,
                "m_item",
                LoadRequiredAsset<Item>(k_StraightSwordPath));
            serializedPickup.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(rigidbody);
            EditorUtility.SetDirty(trigger);
            EditorUtility.SetDirty(pickup);
        }

        private static void ConfigurePickupPresentation(Transform parent, int layer)
        {
            Transform visual = parent.Find("Pickup Visual Effect");
            if (visual == null)
            {
                GameObject visualObject = GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);
                visualObject.name = "Pickup Visual Effect";
                UnityEngine.Object.DestroyImmediate(
                    visualObject.GetComponent<Collider>());
                visual = visualObject.transform;
                visual.SetParent(parent, false);
            }

            visual.gameObject.layer = layer;
            visual.localPosition = new Vector3(0f, 0.48f, 0f);
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one * 0.28f;
            MeshRenderer meshRenderer = GetOrAddComponent<MeshRenderer>(
                visual.gameObject);
            meshRenderer.sharedMaterial = LoadRequiredAsset<Material>(
                k_PickupMaterialPath);
            meshRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            Transform particlesTransform = GetOrCreateChild(
                parent,
                "Pickup Particles");
            particlesTransform.gameObject.layer = layer;
            particlesTransform.localPosition = new Vector3(0f, 0.2f, 0f);
            particlesTransform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            particlesTransform.localScale = Vector3.one;
            ParticleSystem particles = GetOrAddComponent<ParticleSystem>(
                particlesTransform.gameObject);
            ParticleSystem.MainModule main = particles.main;
            main.duration = 1.5f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.65f, 0.18f, 0.9f),
                new Color(1f, 0.94f, 0.58f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 14f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.2f;
            ParticleSystemRenderer particleRenderer =
                GetOrAddComponent<ParticleSystemRenderer>(
                    particlesTransform.gameObject);
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = LoadRequiredAsset<Material>(
                k_PickupMaterialPath);
        }

        private static void ConfigurePlayerUIPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerUIPrefabPath);
            try
            {
                PlayerUIPopUpManager popupManager =
                    GetRequiredComponent<PlayerUIPopUpManager>(root);
                Transform canvas = root.transform.Find("Player UI") ??
                    throw new InvalidOperationException(
                        "Player UI Manager is missing the Player UI Canvas.");
                TMP_FontAsset font = root.GetComponentsInChildren<TMP_Text>(true)
                    .Select(text => text.font)
                    .FirstOrDefault(candidate => candidate != null) ??
                    throw new InvalidOperationException(
                        "Player UI Manager is missing a TMP font.");

                RectTransform organizer = GetOrCreateRectTransform(
                    canvas,
                    k_PopupOrganizerName);
                ConfigureBottomCenteredRect(
                    organizer,
                    new Vector2(0f, 72f),
                    new Vector2(760f, 204f));
                VerticalLayoutGroup layout = GetOrAddComponent<VerticalLayoutGroup>(
                    organizer.gameObject);
                layout.padding = new RectOffset(10, 10, 8, 8);
                layout.spacing = 10f;
                layout.childAlignment = TextAnchor.LowerCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                RectTransform messagePopup = FindDescendant(
                        root.transform,
                        k_MessagePopupName) as RectTransform ??
                    throw new InvalidOperationException(
                        "EP52 must configure the player message popup before EP67.");
                messagePopup.SetParent(organizer, false);
                ConfigureLayoutElement(messagePopup.gameObject, 720f, 68f);
                ConfigurePopupBackground(messagePopup.gameObject);
                DisableDisplayRaycasts(messagePopup);

                RectTransform itemPopup = GetOrCreateRectTransform(
                    organizer,
                    k_ItemPopupName);
                ConfigureLayoutElement(itemPopup.gameObject, 720f, 108f);
                ConfigurePopupBackground(itemPopup.gameObject);

                Image itemIcon = GetOrAddComponent<Image>(
                    GetOrCreateRectTransform(itemPopup, "Item Icon").gameObject);
                ConfigureAnchoredRect(
                    itemIcon.rectTransform,
                    new Vector2(-290f, 0f),
                    new Vector2(82f, 82f));
                itemIcon.preserveAspect = true;
                itemIcon.raycastTarget = false;

                TMP_Text itemName = ConfigureText(
                    GetOrCreateRectTransform(itemPopup, "Item Name"),
                    font,
                    "Straight Sword",
                    TextAlignmentOptions.MidlineLeft,
                    28f,
                    s_textColor);
                ConfigureAnchoredRect(
                    itemName.rectTransform,
                    new Vector2(8f, 14f),
                    new Vector2(470f, 42f));

                TMP_Text itemAmount = ConfigureText(
                    GetOrCreateRectTransform(itemPopup, "Item Amount"),
                    font,
                    "x1",
                    TextAlignmentOptions.MidlineRight,
                    25f,
                    s_textColor);
                ConfigureAnchoredRect(
                    itemAmount.rectTransform,
                    new Vector2(300f, 14f),
                    new Vector2(80f, 42f));

                TMP_Text continueText = ConfigureText(
                    GetOrCreateRectTransform(itemPopup, "Continue Prompt"),
                    font,
                    "Y / R   Continue",
                    TextAlignmentOptions.MidlineRight,
                    18f,
                    s_mutedTextColor);
                ConfigureAnchoredRect(
                    continueText.rectTransform,
                    new Vector2(160f, -27f),
                    new Vector2(360f, 30f));

                SetObjectReference(popupManager, "m_itemPopup", itemPopup.gameObject);
                SetObjectReference(popupManager, "m_itemIcon", itemIcon);
                SetObjectReference(popupManager, "m_itemNameText", itemName);
                SetObjectReference(popupManager, "m_itemAmountText", itemAmount);
                SetLayerRecursively(organizer.gameObject, canvas.gameObject.layer);
                DisableDisplayRaycasts(organizer);
                itemPopup.gameObject.SetActive(false);
                EditorUtility.SetDirty(layout);
                EditorUtility.SetDirty(popupManager);
                SavePrefab(root, k_PlayerUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureWorldSoundManager()
        {
            Scene scene = OpenSceneIfNeeded(k_MainMenuScenePath, out bool opened);
            try
            {
                WorldSoundFXManager manager = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<WorldSoundFXManager>(true))
                    .FirstOrDefault() ??
                    throw new InvalidOperationException(
                        "The main menu is missing WorldSoundFXManager.");
                SerializedObject serializedManager = new SerializedObject(manager);
                SetObjectReference(
                    serializedManager,
                    "m_pickupItemSoundEffect",
                    LoadRequiredAsset<AudioClip>(k_PickupSoundPath));
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
                SaveScene(scene, "pickup sound");
            }
            finally
            {
                CloseSceneIfOpened(scene, opened);
            }
        }

        private static void ConfigureWorldPickups()
        {
            Scene scene = OpenSceneIfNeeded(k_WorldScenePath, out bool opened);
            try
            {
                Transform spawnPoint = FindTransform(scene, k_PlayerSpawnPointName) ??
                    throw new InvalidOperationException(
                        $"The World Scene is missing {k_PlayerSpawnPointName}.");
                GameObject pickupPrefab = LoadRequiredAsset<GameObject>(
                    k_PickupPrefabPath);
                for (int pickupIndex = 0;
                    pickupIndex < s_worldPickupNames.Length;
                    pickupIndex++)
                {
                    GameObject pickupRoot = FindRoot(
                        scene,
                        s_worldPickupNames[pickupIndex]);
                    if (pickupRoot == null)
                    {
                        pickupRoot = PrefabUtility.InstantiatePrefab(
                            pickupPrefab,
                            scene) as GameObject ??
                            throw new InvalidOperationException(
                                "Could not instantiate the Item Pickup prefab.");
                    }

                    pickupRoot.name = s_worldPickupNames[pickupIndex];
                    pickupRoot.transform.SetPositionAndRotation(
                        spawnPoint.TransformPoint(s_worldPickupOffsets[pickupIndex]),
                        spawnPoint.rotation);
                    pickupRoot.transform.localScale = Vector3.one;
                    pickupRoot.SetActive(true);
                    PickupItemInteractable pickup =
                        GetRequiredComponent<PickupItemInteractable>(pickupRoot);
                    SerializedObject serializedPickup = new SerializedObject(pickup);
                    SetEnum(
                        serializedPickup,
                        "m_pickupType",
                        ItemPickupType.WorldSpawn);
                    SetInteger(serializedPickup, "m_itemID", pickupIndex);
                    SetBoolean(serializedPickup, "m_hasBeenLooted", false);
                    SetObjectReference(
                        serializedPickup,
                        "m_item",
                        LoadRequiredAsset<Item>(s_worldItemPaths[pickupIndex]));
                    serializedPickup.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(pickupRoot);
                    EditorUtility.SetDirty(pickup);
                }

                SaveScene(scene, "world item pickups");
            }
            finally
            {
                CloseSceneIfOpened(scene, opened);
            }
        }

        private static void ValidateRuntimeContracts()
        {
            BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;
            if (!typeof(Interactable).IsAssignableFrom(
                    typeof(PickupItemInteractable)) ||
                typeof(PickupItemInteractable).GetMethod(
                    "Interact",
                    publicInstance) == null ||
                typeof(PlayerInventoryManager).GetMethod(
                    "AddItemToInventory",
                    publicInstance) == null ||
                typeof(PlayerInventoryManager).GetMethod(
                    "RemoveItemFromInventory",
                    publicInstance) == null ||
                typeof(PlayerUIPopUpManager).GetMethod(
                    "SendItemPopup",
                    publicInstance) == null ||
                typeof(CharacterSoundFXManager).GetMethod(
                    "PlayPickupItemSound",
                    publicInstance) == null)
            {
                throw new InvalidOperationException(
                    "The item pickup runtime contract is incomplete.");
            }
        }

        private static void ValidateSaveRoundTrip()
        {
            CharacterSaveData saveData = new CharacterSaveData();
            saveData.SetWorldItemLooted(0, false);
            saveData.SetWorldItemLooted(67, true);
            string json = JsonUtility.ToJson(saveData);
            CharacterSaveData restoredData =
                JsonUtility.FromJson<CharacterSaveData>(json);
            if (restoredData == null ||
                !restoredData.TryGetWorldItemLooted(0, out bool firstLooted) ||
                firstLooted ||
                !restoredData.IsWorldItemLooted(67) ||
                restoredData.TryGetWorldItemLooted(99, out _))
            {
                throw new InvalidOperationException(
                    "World item loot state must survive a JSON save round trip.");
            }
        }

        private static void ValidatePickupPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PickupPrefabPath);
            try
            {
                PickupItemInteractable pickup =
                    GetRequiredComponent<PickupItemInteractable>(root);
                Rigidbody rigidbody = GetRequiredComponent<Rigidbody>(root);
                SphereCollider trigger = GetRequiredComponent<SphereCollider>(root);
                bool hasPresentation =
                    root.transform.Find("Pickup Visual Effect")
                        ?.GetComponent<MeshRenderer>()?.sharedMaterial != null &&
                    root.transform.Find("Pickup Particles")
                        ?.GetComponent<ParticleSystem>() != null;
                if (root.GetComponent<NetworkObject>() == null ||
                    !rigidbody.isKinematic ||
                    rigidbody.useGravity ||
                    !trigger.isTrigger ||
                    pickup.PickupType != ItemPickupType.CharacterDrop ||
                    pickup.ItemID != -1 ||
                    pickup.Item == null ||
                    pickup.IsHostOnlyInteractable ||
                    !hasPresentation)
                {
                    throw new InvalidOperationException(
                        "The generic CharacterDrop pickup prefab is invalid.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidatePlayerUIPrefab()
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_PlayerUIPrefabPath) ??
                throw new InvalidOperationException(
                    "Player UI Manager prefab is missing.");
            Transform organizer = root.transform.Find(
                $"Player UI/{k_PopupOrganizerName}") ??
                throw new InvalidOperationException(
                    "Player UI is missing its Popup Organizer.");
            Transform messagePopup = organizer.Find(k_MessagePopupName);
            Transform itemPopup = organizer.Find(k_ItemPopupName);
            PlayerUIPopUpManager popupManager =
                GetRequiredComponent<PlayerUIPopUpManager>(root);
            Image itemIcon = itemPopup?.Find("Item Icon")?.GetComponent<Image>();
            TMP_Text itemName = itemPopup?.Find("Item Name")
                ?.GetComponent<TMP_Text>();
            TMP_Text itemAmount = itemPopup?.Find("Item Amount")
                ?.GetComponent<TMP_Text>();
            bool hasRaycastTarget = organizer.GetComponentsInChildren<Graphic>(true)
                .Any(graphic => graphic.raycastTarget);
            if (organizer.GetComponent<VerticalLayoutGroup>() == null ||
                messagePopup == null ||
                itemPopup == null ||
                itemPopup.gameObject.activeSelf ||
                itemIcon == null ||
                itemName == null ||
                itemAmount == null ||
                itemPopup.Find("Continue Prompt") == null ||
                hasRaycastTarget)
            {
                throw new InvalidOperationException(
                    "Player UI item popup presentation is invalid.");
            }

            ValidateObjectReference(
                popupManager,
                "m_itemPopup",
                itemPopup.gameObject);
            ValidateObjectReference(popupManager, "m_itemIcon", itemIcon);
            ValidateObjectReference(popupManager, "m_itemNameText", itemName);
            ValidateObjectReference(popupManager, "m_itemAmountText", itemAmount);
        }

        private static void ValidateWorldSoundManager()
        {
            Scene scene = OpenSceneIfNeeded(k_MainMenuScenePath, out bool opened);
            try
            {
                WorldSoundFXManager manager = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<WorldSoundFXManager>(true))
                    .FirstOrDefault() ??
                    throw new InvalidOperationException(
                        "The main menu is missing WorldSoundFXManager.");
                if (manager.PickupItemSoundEffect !=
                    LoadRequiredAsset<AudioClip>(k_PickupSoundPath))
                {
                    throw new InvalidOperationException(
                        "WorldSoundFXManager is missing the pickup sound.");
                }
            }
            finally
            {
                CloseSceneIfOpened(scene, opened);
            }
        }

        private static void ValidateWorldPickups()
        {
            Scene scene = OpenSceneIfNeeded(k_WorldScenePath, out bool opened);
            try
            {
                Transform spawnPoint = FindTransform(scene, k_PlayerSpawnPointName);
                HashSet<int> uniqueIDs = new HashSet<int>();
                for (int pickupIndex = 0;
                    pickupIndex < s_worldPickupNames.Length;
                    pickupIndex++)
                {
                    GameObject root = FindRoot(
                        scene,
                        s_worldPickupNames[pickupIndex]) ??
                        throw new InvalidOperationException(
                            $"World Scene is missing {s_worldPickupNames[pickupIndex]}.");
                    PickupItemInteractable pickup =
                        GetRequiredComponent<PickupItemInteractable>(root);
                    bool isNearby = spawnPoint != null &&
                        Vector3.Distance(root.transform.position, spawnPoint.position) <=
                        6f;
                    if (root.GetComponent<NetworkObject>() == null ||
                        pickup.PickupType != ItemPickupType.WorldSpawn ||
                        pickup.ItemID < 0 ||
                        !uniqueIDs.Add(pickup.ItemID) ||
                        pickup.Item != LoadRequiredAsset<Item>(
                            s_worldItemPaths[pickupIndex]) ||
                        !isNearby)
                    {
                        throw new InvalidOperationException(
                            $"{root.name} is not a valid unique WorldSpawn pickup.");
                    }
                }
            }
            finally
            {
                CloseSceneIfOpened(scene, opened);
            }
        }

        private static void ConfigurePopupBackground(GameObject popup)
        {
            Image background = GetOrAddComponent<Image>(popup);
            background.color = s_backgroundColor;
            background.raycastTarget = false;
            Outline outline = GetOrAddComponent<Outline>(popup);
            outline.effectColor = s_borderColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
            EditorUtility.SetDirty(background);
            EditorUtility.SetDirty(outline);
        }

        private static TMP_Text ConfigureText(
            RectTransform rectTransform,
            TMP_FontAsset font,
            string content,
            TextAlignmentOptions alignment,
            float fontSize,
            Color color)
        {
            TextMeshProUGUI text =
                GetOrAddComponent<TextMeshProUGUI>(rectTransform.gameObject);
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.SmallCaps;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            EditorUtility.SetDirty(text);
            return text;
        }

        private static void ConfigureLayoutElement(
            GameObject gameObject,
            float width,
            float height)
        {
            LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(gameObject);
            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
            EditorUtility.SetDirty(layoutElement);
        }

        private static void ConfigureBottomCenteredRect(
            RectTransform rectTransform,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private static void ConfigureAnchoredRect(
            RectTransform rectTransform,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private static RectTransform GetOrCreateRectTransform(
            Transform parent,
            string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                return existing as RectTransform ??
                    throw new InvalidOperationException(
                        $"{objectName} must use a RectTransform.");
            }

            GameObject child = new GameObject(objectName, typeof(RectTransform));
            RectTransform rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static Transform FindDescendant(Transform parent, string objectName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == objectName)
                {
                    return child;
                }

                Transform descendant = FindDescendant(child, objectName);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private static Transform FindTransform(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(transform => transform.name == objectName);
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == objectName);
        }

        private static Scene OpenSceneIfNeeded(string scenePath, out bool opened)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            opened = !scene.IsValid() || !scene.isLoaded;
            return opened
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)
                : scene;
        }

        private static void SaveScene(Scene scene, string featureName)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    $"Could not save the scene {featureName} configuration.");
            }
        }

        private static void CloseSceneIfOpened(Scene scene, bool opened)
        {
            if (opened && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
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
            for (int segmentIndex = 1;
                segmentIndex < segments.Length;
                segmentIndex++)
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
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}.");
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
            return gameObject.GetComponent<T>() ??
                throw new InvalidOperationException(
                    $"{gameObject.name} is missing {typeof(T).Name}.");
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} is missing " +
                    $"{propertyName}.");
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SetObjectReference(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetRequiredProperty(serializedObject, propertyName)
                .objectReferenceValue = value;
        }

        private static void SetString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            GetRequiredProperty(serializedObject, propertyName).stringValue = value;
        }

        private static void SetInteger(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).intValue = value;
        }

        private static void SetBoolean(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            GetRequiredProperty(serializedObject, propertyName).boolValue = value;
        }

        private static void SetEnum<TEnum>(
            SerializedObject serializedObject,
            string propertyName,
            TEnum value)
            where TEnum : Enum
        {
            GetRequiredProperty(serializedObject, propertyName).enumValueIndex =
                Convert.ToInt32(value);
        }

        private static void ValidateObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object expectedValue)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            if (GetRequiredProperty(serializedObject, propertyName)
                    .objectReferenceValue != expectedValue)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{propertyName} is not configured.");
            }
        }

        private static void DisableDisplayRaycasts(Transform parent)
        {
            foreach (Graphic graphic in parent.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
                EditorUtility.SetDirty(graphic);
            }
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
