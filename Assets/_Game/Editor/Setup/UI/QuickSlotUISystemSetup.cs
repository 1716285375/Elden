using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP90 gameplay quick-slot UI.</summary>
    public static class QuickSlotUISystemSetup
    {
        private const int k_EquipmentSlotCount = 15;
        private const int k_QuickSlotCount = 3;
        private const string k_PlayerControlsPath =
            "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_PlayerPrefabPath =
            "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_EquipmentSlotPrefabPath =
            "Assets/_Game/Prefabs/UI/Equipment Slot.prefab";
        private const string k_QuickSlotPrefabPath =
            "Assets/_Game/Prefabs/UI/Quick Slot UI.prefab";
        private const string k_HealthFlaskPath =
            "Assets/_Game/Data/Items/Quick Slot Items/Flask of Crimson Tears.asset";
        private const string k_FocusFlaskPath =
            "Assets/_Game/Data/Items/Quick Slot Items/Flask of Cerulean Tears.asset";

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
            "Secondary Projectile",
            "Quick Slot 01",
            "Quick Slot 02",
            "Quick Slot 03"
        };

        [MenuItem("Tools/Elden/Configure Quick Slot UI System")]
        public static void ConfigureQuickSlotUISystem()
        {
            ConfigureQuickSlotItems();
            ConfigurePlayerPrefab();
            ConfigurePlayerUIPrefab();
            AssetDatabase.SaveAssets();
            ValidateQuickSlotUISystem();
            Debug.Log(
                "[QuickSlotUISystemSetup] Configured EP90 three-slot item " +
                "equipment, counts, switching, and HUD presentation.");
        }

        [MenuItem("Tools/Elden/Validate Quick Slot UI System")]
        public static void ValidateQuickSlotUISystem()
        {
            ValidateInput();
            ValidateQuickSlotItems();
            ValidatePlayerPrefab();
            ValidatePlayerUIPrefab();
            Debug.Log(
                "[QuickSlotUISystemValidation] Three item slots, consumable " +
                "counts, input, and HUD quantity presentation are valid.");
        }

        private static void ConfigureQuickSlotItems()
        {
            SetConsumable(
                LoadRequiredAsset<QuickSlotItem>(k_HealthFlaskPath),
                true);
            SetConsumable(
                LoadRequiredAsset<QuickSlotItem>(k_FocusFlaskPath),
                true);
        }

        private static void SetConsumable(QuickSlotItem item, bool isConsumable)
        {
            SerializedObject serializedItem = new SerializedObject(item);
            GetProperty(serializedItem, "m_isConsumable").boolValue = isConsumable;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void ConfigurePlayerPrefab()
        {
            QuickSlotItem healthFlask =
                LoadRequiredAsset<QuickSlotItem>(k_HealthFlaskPath);
            QuickSlotItem focusFlask =
                LoadRequiredAsset<QuickSlotItem>(k_FocusFlaskPath);
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerInventoryManager inventory =
                    root.GetComponent<PlayerInventoryManager>() ??
                    throw new InvalidOperationException(
                        "Player prefab requires PlayerInventoryManager.");
                SerializedObject serializedInventory = new SerializedObject(
                    inventory);
                SerializedProperty quickSlots = GetProperty(
                    serializedInventory,
                    "m_quickSlotItemsInQuickSlots");
                quickSlots.arraySize = k_QuickSlotCount;
                quickSlots.GetArrayElementAtIndex(0).objectReferenceValue =
                    healthFlask;
                quickSlots.GetArrayElementAtIndex(1).objectReferenceValue =
                    focusFlask;
                quickSlots.GetArrayElementAtIndex(2).objectReferenceValue = null;
                GetProperty(serializedInventory, "m_quickSlotItemIndex")
                    .intValue = 0;
                GetProperty(serializedInventory, "m_startingQuickSlotItem")
                    .objectReferenceValue = healthFlask;
                serializedInventory.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(inventory);
                PrefabUtility.SaveAsPrefabAsset(root, k_PlayerPrefabPath);
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
            layout.cellSize = new Vector2(330f, 68f);
            layout.spacing = new Vector2(24f, 8f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.childAlignment = TextAnchor.UpperCenter;
            if (slotsGrid is RectTransform gridRect)
            {
                gridRect.sizeDelta = new Vector2(700f, 620f);
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
                    label.text = s_equipmentSlotNames[slotIndex]
                        .ToUpperInvariant();
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

        private static void ValidateInput()
        {
            InputActionAsset controls = LoadRequiredAsset<InputActionAsset>(
                k_PlayerControlsPath);
            InputAction action = controls.FindAction(
                "Player Movement/Switch Quick Slot Item",
                true);
            if (!action.bindings.Any(binding =>
                    binding.path == "<Gamepad>/dpad/down") ||
                !action.bindings.Any(binding =>
                    binding.path == "<Keyboard>/downArrow"))
            {
                throw new InvalidOperationException(
                    "Switch Quick Slot Item requires controller and keyboard bindings.");
            }
        }

        private static void ValidateQuickSlotItems()
        {
            QuickSlotItem healthFlask =
                LoadRequiredAsset<QuickSlotItem>(k_HealthFlaskPath);
            QuickSlotItem focusFlask =
                LoadRequiredAsset<QuickSlotItem>(k_FocusFlaskPath);
            if (!healthFlask.IsConsumable ||
                !focusFlask.IsConsumable ||
                healthFlask.ItemIcon == null ||
                focusFlask.ItemIcon == null)
            {
                throw new InvalidOperationException(
                    "Both flasks require consumable state and HUD icons.");
            }
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerInventoryManager inventory =
                    root.GetComponent<PlayerInventoryManager>();
                SerializedObject serializedInventory = new SerializedObject(
                    inventory);
                SerializedProperty quickSlots = GetProperty(
                    serializedInventory,
                    "m_quickSlotItemsInQuickSlots");
                if (quickSlots.arraySize != k_QuickSlotCount ||
                    quickSlots.GetArrayElementAtIndex(0).objectReferenceValue ==
                        null ||
                    quickSlots.GetArrayElementAtIndex(1).objectReferenceValue ==
                        null ||
                    quickSlots.GetArrayElementAtIndex(2).objectReferenceValue !=
                        null ||
                    GetProperty(serializedInventory, "m_quickSlotItemIndex")
                        .intValue != 0)
                {
                    throw new InvalidOperationException(
                        "Player requires Health, Focus, and empty item quick slots.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
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
                Transform slotsGrid = FindDescendant(root.transform, "Slots Grid");
                GameObject quickSlotPrefab = LoadRequiredAsset<GameObject>(
                    k_QuickSlotPrefabPath);
                UIQuickSlot hudQuickSlot =
                    quickSlotPrefab.GetComponent<UIQuickSlot>();
                SerializedObject serializedQuickSlot = new SerializedObject(
                    hudQuickSlot);
                if (slotsGrid == null ||
                    slotsGrid.childCount != k_EquipmentSlotCount ||
                    GetProperty(serializedEquipment, "m_equipmentSlotIcons")
                        .arraySize != k_EquipmentSlotCount ||
                    GetProperty(serializedEquipment, "m_equipmentSlotButtons")
                        .arraySize != k_EquipmentSlotCount ||
                    GetProperty(
                            serializedEquipment,
                            "m_equipmentSlotQuantityTexts")
                        .arraySize != k_EquipmentSlotCount ||
                    GetProperty(serializedQuickSlot, "m_quantityText")
                        .objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        "Player UI requires fifteen equipment slots and HUD count.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindDescendant(
            Transform root,
            string objectName)
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
            SerializedProperty array = GetProperty(
                serializedObject,
                propertyName);
            array.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                array.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
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
                    $"{serializedObject.targetObject.GetType().Name} is " +
                    $"missing serialized property {propertyName}.");
        }
    }
}
