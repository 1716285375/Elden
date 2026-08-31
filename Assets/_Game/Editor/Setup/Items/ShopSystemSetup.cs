using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the authored assets required by EP163-168.</summary>
    public static class ShopSystemSetup
    {
        private const string k_ShopFolder = "Assets/_Game/Data/Shops";
        private const string k_BlacksmithShopPath =
            k_ShopFolder + "/Blacksmith Shop.asset";
        private const string k_BlacksmithPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Blacksmith NPC.prefab";
        private const string k_StraightSwordPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset";
        private const string k_StarterHoodPath =
            "Assets/_Game/Data/Items/Armor/Starter Hood.asset";
        private const string k_StandardArrowPath =
            "Assets/_Game/Data/Items/Projectiles/Standard Arrow.asset";
        private const string k_InputActionsPath =
            "Assets/_Game/Settings/Input/PlayerControls.inputactions";

        [MenuItem("Tools/Elden/Configure Shop System")]
        public static void ConfigureShopSystem()
        {
            EnsureFolder(k_ShopFolder);
            Item sword = ConfigureItemValue(k_StraightSwordPath, 400);
            Item hood = ConfigureItemValue(k_StarterHoodPath, 600);
            Item arrow = ConfigureItemValue(k_StandardArrowPath, 20);
            CharacterShop shop = ConfigureCharacterShop(
                new[] { sword, hood, arrow },
                new[] { 2, 1, 30 },
                new[] { false, true, true });
            ConfigureBlacksmith(shop);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateShopSystem();
            Debug.Log(
                "[ShopSystemSetup] Configured Blacksmith stock, prices, " +
                "persistent Shop ID, and shared category input.");
        }

        [MenuItem("Tools/Elden/Validate Shop System")]
        public static void ValidateShopSystem()
        {
            CharacterShop shop = LoadRequiredAsset<CharacterShop>(
                k_BlacksmithShopPath);
            Item sword = LoadRequiredAsset<Item>(k_StraightSwordPath);
            Item hood = LoadRequiredAsset<Item>(k_StarterHoodPath);
            Item arrow = LoadRequiredAsset<Item>(k_StandardArrowPath);
            if (shop.StockCount != 3 ||
                sword.ItemValue != 400 ||
                hood.ItemValue != 600 ||
                arrow.ItemValue != 20)
            {
                throw new InvalidOperationException(
                    "Shop stock or authored item values are invalid.");
            }

            List<Item> runtimeStock = shop.CreateRuntimeInventory(null);
            try
            {
                if (runtimeStock.Count != 3 ||
                    runtimeStock[0].ShopStockAmount != 2)
                {
                    throw new InvalidOperationException(
                        "Finite non-stackable shop stock was clamped.");
                }
            }
            finally
            {
                foreach (Item runtimeItem in runtimeStock)
                {
                    UnityEngine.Object.DestroyImmediate(runtimeItem);
                }
            }

            GameObject prefab = PrefabUtility.LoadPrefabContents(
                k_BlacksmithPrefabPath);
            try
            {
                AICharacterInventoryManager inventory = prefab
                    .GetComponentInChildren<AICharacterInventoryManager>(true);
                if (inventory == null ||
                    !inventory.IsShop ||
                    inventory.CharacterShopID != Shops.Blacksmith)
                {
                    throw new InvalidOperationException(
                        "Blacksmith NPC is not connected to its persistent shop.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            ValidateInputActions();
            ValidateSaveRoundTrip(sword);
            ValidateRuntimeCopyIsolation(arrow);
            ValidateShopRules(sword, hood);
            Debug.Log(
                "[ShopSystemValidation] EP163-168 shop assets and rules are valid.");
        }

        private static CharacterShop ConfigureCharacterShop(
            IReadOnlyList<Item> items,
            IReadOnlyList<int> amounts,
            IReadOnlyList<bool> infiniteItems)
        {
            CharacterShop shop =
                AssetDatabase.LoadAssetAtPath<CharacterShop>(
                    k_BlacksmithShopPath);
            if (shop == null)
            {
                shop = ScriptableObject.CreateInstance<CharacterShop>();
                AssetDatabase.CreateAsset(shop, k_BlacksmithShopPath);
            }

            SerializedObject serializedShop = new(shop);
            SetObjectList(serializedShop, "m_items", items);
            SetIntegerList(serializedShop, "m_itemAmounts", amounts);
            SetBooleanList(serializedShop, "m_infiniteItems", infiniteItems);
            serializedShop.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shop);
            return shop;
        }

        private static Item ConfigureItemValue(string path, int value)
        {
            Item item = LoadRequiredAsset<Item>(path);
            SerializedObject serializedItem = new(item);
            SerializedProperty valueProperty = GetRequiredProperty(
                serializedItem,
                "m_itemValue");
            valueProperty.intValue = Mathf.Max(0, value);
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static void ConfigureBlacksmith(CharacterShop shop)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_BlacksmithPrefabPath);
            try
            {
                AICharacterInventoryManager inventory = root
                    .GetComponentInChildren<AICharacterInventoryManager>(true);
                if (inventory == null)
                {
                    throw new InvalidOperationException(
                        "Blacksmith prefab requires AICharacterInventoryManager.");
                }

                SerializedObject serializedInventory = new(inventory);
                GetRequiredProperty(serializedInventory, "m_characterShopID")
                    .enumValueIndex = (int)Shops.Blacksmith;
                GetRequiredProperty(serializedInventory, "m_characterShop")
                    .objectReferenceValue = shop;
                serializedInventory.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_BlacksmithPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateInputActions()
        {
            InputActionAsset inputActions =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                    k_InputActionsPath);
            InputActionMap uiMap = inputActions?.FindActionMap("UI", false);
            InputAction previous = uiMap?.FindAction(
                "Previous Shop Category",
                false);
            InputAction next = uiMap?.FindAction(
                "Next Shop Category",
                false);
            if (previous == null ||
                next == null ||
                previous.bindings.Count != 2 ||
                next.bindings.Count != 2)
            {
                throw new InvalidOperationException(
                    "Shop category actions require keyboard and gamepad bindings.");
            }
        }

        private static void ValidateSaveRoundTrip(Item item)
        {
            CharacterSaveData saveData = new();
            SerializableShopInventory inventory = new(
                Shops.Blacksmith,
                new[] { item.ItemID },
                new[] { 2 },
                new[] { false });
            saveData.SetShopInventory(inventory);
            CharacterSaveData restored = JsonUtility.FromJson<CharacterSaveData>(
                JsonUtility.ToJson(saveData));
            SerializableShopInventory restoredInventory =
                restored.GetShopInventory(Shops.Blacksmith);
            if (restoredInventory == null ||
                restoredInventory.ItemCount != 1 ||
                restoredInventory.GetItemID(0) != item.ItemID ||
                restoredInventory.GetItemAmount(0) != 2)
            {
                throw new InvalidOperationException(
                    "Shop inventory did not survive JSON serialization.");
            }
        }

        private static void ValidateRuntimeCopyIsolation(Item template)
        {
            int originalAmount = template.CurrentItemAmount;
            bool originalInfinite = template.IsInfinite;
            Item runtimeCopy = UnityEngine.Object.Instantiate(template);
            runtimeCopy.hideFlags = HideFlags.DontSave;
            runtimeCopy.SetCurrentItemAmount(1);
            runtimeCopy.SetInfinite(!originalInfinite);
            UnityEngine.Object.DestroyImmediate(runtimeCopy);
            if (template.CurrentItemAmount != originalAmount ||
                template.IsInfinite != originalInfinite)
            {
                throw new InvalidOperationException(
                    "Runtime stock mutation changed the catalog Item asset.");
            }
        }

        private static void ValidateShopRules(Item weapon, Item armor)
        {
            if (AICharacterInventoryManager.CalculateSellValue(weapon) != 100 ||
                PlayerUIShopManager.GetCycledCategory(
                    ShopItemCategory.All,
                    -1) != ShopItemCategory.Weapons ||
                PlayerUIShopManager.GetCycledCategory(
                    ShopItemCategory.Weapons,
                    1) != ShopItemCategory.All ||
                !PlayerUIShopManager.MatchesCategory(
                    weapon,
                    ShopItemCategory.Weapons) ||
                !PlayerUIShopManager.MatchesCategory(
                    armor,
                    ShopItemCategory.Armor) ||
                PlayerUIShopManager.MatchesCategory(
                    weapon,
                    ShopItemCategory.Armor))
            {
                throw new InvalidOperationException(
                    "Shop sell value or category rules are invalid.");
            }
        }

        private static void SetObjectList(
            SerializedObject serializedObject,
            string propertyName,
            IReadOnlyList<Item> values)
        {
            SerializedProperty property = GetRequiredProperty(
                serializedObject,
                propertyName);
            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }
        }

        private static void SetIntegerList(
            SerializedObject serializedObject,
            string propertyName,
            IReadOnlyList<int> values)
        {
            SerializedProperty property = GetRequiredProperty(
                serializedObject,
                propertyName);
            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).intValue = values[index];
            }
        }

        private static void SetBooleanList(
            SerializedObject serializedObject,
            string propertyName,
            IReadOnlyList<bool> values)
        {
            SerializedProperty property = GetRequiredProperty(
                serializedObject,
                propertyName);
            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).boolValue = values[index];
            }
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.name} is missing {propertyName}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = folderPath[..folderPath.LastIndexOf('/')];
            string folderName = folderPath[(folderPath.LastIndexOf('/') + 1)..];
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
