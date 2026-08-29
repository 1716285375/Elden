using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP89 ammunition equipment and HUD UI.</summary>
    public static class ProjectileUISystemSetup
    {
        private const int k_EquipmentSlotCount = 12;
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_EquipmentSlotPrefabPath =
            "Assets/_Game/Prefabs/UI/Equipment Slot.prefab";
        private const string k_QuickSlotPrefabPath =
            "Assets/_Game/Prefabs/UI/Quick Slot UI.prefab";
        private const string k_StandardArrowPath =
            "Assets/_Game/Data/Items/Projectiles/Standard Arrow.asset";
        private const string k_FireArrowPath =
            "Assets/_Game/Data/Items/Projectiles/Fire Arrow.asset";
        private const string k_StandardArrowIconPath =
            "Assets/Art/Textures/UI/Items/Standard Arrow Icon.png";
        private const string k_FireArrowIconPath =
            "Assets/Art/Textures/UI/Items/Fire Arrow Icon.png";
        private const string k_ProjectileContainerName = "Projectile Quick Slots";

        private static readonly string[] s_equipmentSlotNames =
        {
            "Right Weapon 01",
            "Right Weapon 02",
            "Right Weapon 03",
            "Left Weapon 01",
            "Left Weapon 02",
            "Left Weapon 03",
            "Head",
            "Body",
            "Leg",
            "Hand",
            "Main Projectile",
            "Secondary Projectile"
        };

        [MenuItem("Tools/Elden/Configure Projectile UI System")]
        public static void ConfigureProjectileUISystem()
        {
            ConfigureProjectileIcons();
            ConfigureEquipmentSlotPrefab();
            ConfigurePlayerUIPrefab();
            AssetDatabase.SaveAssets();
            ValidateProjectileUISystem();
            Debug.Log(
                "[ProjectileUISystemSetup] Configured EP89 primary and secondary " +
                "ammunition equipment slots, counts, and Bow-context HUD.");
        }

        [MenuItem("Tools/Elden/Validate Projectile UI System")]
        public static void ValidateProjectileUISystem()
        {
            ValidateProjectileItems();
            ValidateEquipmentSlotPrefab();
            ValidatePlayerUIPrefab();
            Debug.Log(
                "[ProjectileUISystemValidation] Projectile icons, two equipment " +
                "slots, quantity labels, and hidden-by-default HUD are valid.");
        }

        private static void ConfigureProjectileIcons()
        {
            Sprite standardArrowIcon = ConfigureSprite(k_StandardArrowIconPath);
            Sprite fireArrowIcon = ConfigureSprite(k_FireArrowIconPath);
            SetItemIcon(
                LoadRequiredAsset<RangedProjectileItem>(k_StandardArrowPath),
                standardArrowIcon);
            SetItemIcon(
                LoadRequiredAsset<RangedProjectileItem>(k_FireArrowPath),
                fireArrowIcon);
        }

        private static Sprite ConfigureSprite(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as
                TextureImporter ?? throw new InvalidOperationException(
                    $"Projectile icon source is missing: {assetPath}");
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

            return LoadRequiredAsset<Sprite>(assetPath);
        }

        private static void SetItemIcon(Item item, Sprite icon)
        {
            SerializedObject serializedItem = new SerializedObject(item);
            GetProperty(serializedItem, "m_itemIcon").objectReferenceValue = icon;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void ConfigureEquipmentSlotPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_EquipmentSlotPrefabPath);
            try
            {
                TMP_Text label = root.transform.Find("Label")
                    ?.GetComponent<TMP_Text>() ??
                    throw new InvalidOperationException(
                        "Equipment Slot prefab requires its Label text.");
                TMP_Text quantity = GetOrCreateText(
                    root.transform,
                    "Quantity",
                    label.font);
                RectTransform quantityRect = quantity.rectTransform;
                quantityRect.anchorMin = new Vector2(1f, 0f);
                quantityRect.anchorMax = new Vector2(1f, 0f);
                quantityRect.pivot = new Vector2(1f, 0f);
                quantityRect.anchoredPosition = new Vector2(-16f, 8f);
                quantityRect.sizeDelta = new Vector2(72f, 30f);
                quantity.fontSize = 18f;
                quantity.alignment = TextAlignmentOptions.BottomRight;
                quantity.color = new Color(0.9f, 0.84f, 0.7f, 1f);
                quantity.raycastTarget = false;
                quantity.text = "30";
                quantity.gameObject.SetActive(false);
                SetLayerRecursively(root, 5);
                PrefabUtility.SaveAsPrefabAsset(root, k_EquipmentSlotPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigurePlayerUIPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerUIPrefabPath);
            try
            {
                PlayerUIEquipmentManager equipmentManager =
                    root.GetComponent<PlayerUIEquipmentManager>() ??
                    throw new InvalidOperationException(
                        "Player UI prefab requires PlayerUIEquipmentManager.");
                Transform slotsGrid = FindDescendant(root.transform, "Slots Grid") ??
                    throw new InvalidOperationException(
                        "Player UI prefab requires the Equipment Slots Grid.");
                ConfigureEquipmentGrid(equipmentManager, slotsGrid);
                ConfigureProjectileHUD(root);
                PrefabUtility.SaveAsPrefabAsset(root, k_PlayerUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureEquipmentGrid(
            PlayerUIEquipmentManager equipmentManager,
            Transform slotsGrid)
        {
            GameObject slotPrefab = LoadRequiredAsset<GameObject>(
                k_EquipmentSlotPrefabPath);
            GridLayoutGroup layout = slotsGrid.GetComponent<GridLayoutGroup>() ??
                slotsGrid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(330f, 82f);
            layout.spacing = new Vector2(24f, 14f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.childAlignment = TextAnchor.UpperCenter;
            if (slotsGrid is RectTransform gridRect)
            {
                gridRect.sizeDelta = new Vector2(700f, 590f);
            }

            Image[] icons = new Image[k_EquipmentSlotCount];
            Button[] buttons = new Button[k_EquipmentSlotCount];
            TMP_Text[] quantities = new TMP_Text[k_EquipmentSlotCount];
            for (int slotIndex = 0;
                slotIndex < k_EquipmentSlotCount;
                slotIndex++)
            {
                Transform slotTransform = slotsGrid.Find(
                    s_equipmentSlotNames[slotIndex]);
                if (slotTransform == null)
                {
                    GameObject slot = PrefabUtility.InstantiatePrefab(
                        slotPrefab,
                        slotsGrid) as GameObject;
                    slot.name = s_equipmentSlotNames[slotIndex];
                    slotTransform = slot.transform;
                }

                TMP_Text label = slotTransform.Find("Label")
                    ?.GetComponent<TMP_Text>();
                if (label != null)
                {
                    label.text = s_equipmentSlotNames[slotIndex].ToUpperInvariant();
                }

                icons[slotIndex] = slotTransform.Find("Item Icon")
                    ?.GetComponent<Image>();
                buttons[slotIndex] = slotTransform.GetComponent<Button>();
                quantities[slotIndex] = slotTransform.Find("Quantity")
                    ?.GetComponent<TMP_Text>();
                ConfigureSlotButton(
                    buttons[slotIndex],
                    equipmentManager,
                    slotIndex);
            }

            SerializedObject serializedManager = new SerializedObject(
                equipmentManager);
            SetObjectArray(serializedManager, "m_equipmentSlotIcons", icons);
            SetObjectArray(serializedManager, "m_equipmentSlotButtons", buttons);
            SetObjectArray(
                serializedManager,
                "m_equipmentSlotQuantityTexts",
                quantities);
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(equipmentManager);
        }

        private static void ConfigureSlotButton(
            Button button,
            PlayerUIEquipmentManager equipmentManager,
            int slotIndex)
        {
            if (button == null)
            {
                throw new InvalidOperationException(
                    $"Equipment slot {slotIndex} requires a Button.");
            }

            for (int listenerIndex = button.onClick.GetPersistentEventCount() - 1;
                listenerIndex >= 0;
                listenerIndex--)
            {
                if (button.onClick.GetPersistentTarget(listenerIndex) ==
                        equipmentManager &&
                    button.onClick.GetPersistentMethodName(listenerIndex) ==
                        nameof(PlayerUIEquipmentManager.SelectEquipmentSlot))
                {
                    UnityEventTools.RemovePersistentListener(
                        button.onClick,
                        listenerIndex);
                }
            }

            UnityAction<int> selectSlot = equipmentManager.SelectEquipmentSlot;
            UnityEventTools.AddIntPersistentListener(
                button.onClick,
                selectSlot,
                slotIndex);
            EditorUtility.SetDirty(button);
        }

        private static void ConfigureProjectileHUD(GameObject root)
        {
            PlayerUIHUDManager hudManager = root
                .GetComponentsInChildren<PlayerUIHUDManager>(true)
                .Single();
            RectTransform hud = hudManager.transform as RectTransform ??
                throw new InvalidOperationException(
                    "PlayerUIHUDManager requires a RectTransform.");
            RectTransform container = GetOrCreateRectTransform(
                hud,
                k_ProjectileContainerName);
            container.anchorMin = Vector2.zero;
            container.anchorMax = Vector2.zero;
            container.pivot = Vector2.zero;
            container.anchoredPosition = new Vector2(260f, 40f);
            container.sizeDelta = new Vector2(124f, 58f);

            GameObject quickSlotPrefab = LoadRequiredAsset<GameObject>(
                k_QuickSlotPrefabPath);
            UIQuickSlot mainProjectileSlot = GetOrCreateQuickSlot(
                container,
                quickSlotPrefab,
                "Main Projectile Quick Slot",
                Vector2.zero);
            UIQuickSlot secondaryProjectileSlot = GetOrCreateQuickSlot(
                container,
                quickSlotPrefab,
                "Secondary Projectile Quick Slot",
                new Vector2(66f, 0f));

            SerializedObject serializedHUD = new SerializedObject(hudManager);
            GetProperty(serializedHUD, "m_mainProjectileQuickSlot")
                .objectReferenceValue = mainProjectileSlot;
            GetProperty(serializedHUD, "m_secondaryProjectileQuickSlot")
                .objectReferenceValue = secondaryProjectileSlot;
            GetProperty(serializedHUD, "m_projectileQuickSlotsGameObject")
                .objectReferenceValue = container.gameObject;
            serializedHUD.ApplyModifiedPropertiesWithoutUndo();
            container.gameObject.SetActive(false);
            EditorUtility.SetDirty(hudManager);
        }

        private static UIQuickSlot GetOrCreateQuickSlot(
            RectTransform parent,
            GameObject prefab,
            string objectName,
            Vector2 anchoredPosition)
        {
            Transform existing = parent.Find(objectName);
            GameObject slot = existing != null
                ? existing.gameObject
                : PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            slot.name = objectName;
            RectTransform slotRect = slot.transform as RectTransform;
            slotRect.anchorMin = Vector2.zero;
            slotRect.anchorMax = Vector2.zero;
            slotRect.pivot = Vector2.zero;
            slotRect.anchoredPosition = anchoredPosition;
            slotRect.sizeDelta = new Vector2(58f, 58f);
            slotRect.localScale = Vector3.one;
            return slot.GetComponent<UIQuickSlot>() ??
                throw new InvalidOperationException(
                    $"{objectName} requires UIQuickSlot.");
        }

        private static void ValidateProjectileItems()
        {
            RangedProjectileItem standardArrow =
                LoadRequiredAsset<RangedProjectileItem>(k_StandardArrowPath);
            RangedProjectileItem fireArrow =
                LoadRequiredAsset<RangedProjectileItem>(k_FireArrowPath);
            if (standardArrow.ItemIcon == null || fireArrow.ItemIcon == null)
            {
                throw new InvalidOperationException(
                    "Both projectile items require equipment and HUD icons.");
            }
        }

        private static void ValidateEquipmentSlotPrefab()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_EquipmentSlotPrefabPath);
            TMP_Text quantity = prefab.transform.Find("Quantity")
                ?.GetComponent<TMP_Text>();
            if (quantity == null || quantity.gameObject.activeSelf)
            {
                throw new InvalidOperationException(
                    "Equipment Slot requires a hidden default Quantity label.");
            }
        }

        private static void ValidatePlayerUIPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerUIPrefabPath);
            try
            {
                PlayerUIEquipmentManager equipmentManager =
                    root.GetComponent<PlayerUIEquipmentManager>();
                SerializedObject serializedEquipment = new SerializedObject(
                    equipmentManager);
                PlayerUIHUDManager hudManager = root
                    .GetComponentsInChildren<PlayerUIHUDManager>(true)
                    .Single();
                SerializedObject serializedHUD = new SerializedObject(hudManager);
                GameObject projectileContainer = GetProperty(
                        serializedHUD,
                        "m_projectileQuickSlotsGameObject")
                    .objectReferenceValue as GameObject;
                if (GetProperty(serializedEquipment, "m_equipmentSlotIcons")
                        .arraySize != k_EquipmentSlotCount ||
                    GetProperty(serializedEquipment, "m_equipmentSlotButtons")
                        .arraySize != k_EquipmentSlotCount ||
                    GetProperty(
                            serializedEquipment,
                            "m_equipmentSlotQuantityTexts")
                        .arraySize != k_EquipmentSlotCount ||
                    projectileContainer == null ||
                    projectileContainer.activeSelf ||
                    GetProperty(serializedHUD, "m_mainProjectileQuickSlot")
                        .objectReferenceValue == null ||
                    GetProperty(serializedHUD, "m_secondaryProjectileQuickSlot")
                        .objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        "Player UI requires twelve equipment slots and two hidden " +
                        "projectile HUD quick slots.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static TMP_Text GetOrCreateText(
            Transform parent,
            string objectName,
            TMP_FontAsset font)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                return existing.GetComponent<TMP_Text>() ??
                    existing.gameObject.AddComponent<TextMeshProUGUI>();
            }

            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.font = font;
            return text;
        }

        private static RectTransform GetOrCreateRectTransform(
            Transform parent,
            string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing is RectTransform existingRect)
            {
                return existingRect;
            }

            GameObject child = new GameObject(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            foreach (Transform child in root)
            {
                if (child.name == objectName)
                {
                    return child;
                }

                Transform nested = FindDescendant(child, objectName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void SetObjectArray<T>(
            SerializedObject serializedObject,
            string propertyName,
            T[] values) where T : UnityEngine.Object
        {
            SerializedProperty array = GetProperty(serializedObject, propertyName);
            array.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                array.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.layer = layer;
            }
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private static SerializedProperty GetProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} is missing " +
                    $"serialized property {propertyName}.");
        }
    }
}
