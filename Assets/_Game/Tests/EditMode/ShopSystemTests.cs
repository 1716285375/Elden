using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class ShopSystemTests
    {
        private const string k_ShopPath =
            "Assets/_Game/Data/Shops/Blacksmith Shop.asset";
        private const string k_BlacksmithPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Blacksmith NPC.prefab";
        private const string k_SwordPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset";
        private const string k_HoodPath =
            "Assets/_Game/Data/Items/Armor/Starter Hood.asset";
        private const string k_ArrowPath =
            "Assets/_Game/Data/Items/Projectiles/Standard Arrow.asset";
        private const string k_InputActionsPath =
            "Assets/_Game/Settings/Input/PlayerControls.inputactions";

        [Test]
        public void AuthoredBlacksmithExposesPersistentShopIdentity()
        {
            Object shop = AssetDatabase.LoadMainAssetAtPath(k_ShopPath);
            SerializedObject serializedShop = new(shop);
            Assert.That(serializedShop.FindProperty("m_items").arraySize,
                Is.EqualTo(3));
            Assert.That(serializedShop.FindProperty("m_itemAmounts")
                .GetArrayElementAtIndex(0).intValue, Is.EqualTo(2));
            Assert.That(serializedShop.FindProperty("m_infiniteItems")
                .GetArrayElementAtIndex(2).boolValue, Is.True);

            GameObject blacksmith = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BlacksmithPrefabPath);
            Component inventory = blacksmith.GetComponentsInChildren<Component>(true)
                .FirstOrDefault(component => component != null &&
                    component.GetType().FullName ==
                        "ZZ.AICharacterInventoryManager");
            Assert.That(inventory, Is.Not.Null);
            SerializedObject serializedInventory = new(inventory);
            Assert.That(serializedInventory.FindProperty("m_characterShopID")
                .enumValueIndex, Is.EqualTo((int)Shops.Blacksmith));
            Assert.That(serializedInventory.FindProperty("m_characterShop")
                .objectReferenceValue, Is.EqualTo(shop));
        }

        [Test]
        public void ShopInventoryAndGeneratedMarkerSurviveJsonRoundTrip()
        {
            CharacterSaveData saveData = new();
            saveData.SetShopInventory(new SerializableShopInventory(
                Shops.Blacksmith,
                new[] { 1, 12 },
                new[] { 1, 30 },
                new[] { false, true }));

            CharacterSaveData restored = JsonUtility.FromJson<CharacterSaveData>(
                JsonUtility.ToJson(saveData));
            SerializableShopInventory shop =
                restored.GetShopInventory(Shops.Blacksmith);
            Assert.That(shop, Is.Not.Null);
            Assert.That(shop.ItemCount, Is.EqualTo(2));
            Assert.That(shop.GetItemID(1), Is.EqualTo(12));
            Assert.That(shop.GetItemAmount(1), Is.EqualTo(30));
            Assert.That(shop.GetIsInfinite(1), Is.True);
            Assert.That(restored.ShopsGenerated, Contains.Item(1));
        }

        [Test]
        public void ItemValuesAndTransactionRulesMatchTheTutorial()
        {
            Assert.That(GetItemValue(k_SwordPath), Is.EqualTo(400));
            Assert.That(GetItemValue(k_HoodPath), Is.EqualTo(600));
            Assert.That(GetItemValue(k_ArrowPath), Is.EqualTo(20));

            string aiInventorySource = ReadProjectFile(
                "Assets/_Game/Scripts/Characters/Common/Inventory/" +
                "AICharacterInventoryManager.cs");
            Assert.That(aiInventorySource,
                Does.Contain("Mathf.RoundToInt(item.ItemValue / 4f)"));
            Assert.That(aiInventorySource,
                Does.Contain("purchasedItem.SetCurrentItemAmount(1)"));
            Assert.That(aiInventorySource,
                Does.Contain("purchasedItem.SetInfinite(false)"));
            Assert.That(aiInventorySource,
                Does.Contain("shopItem.ShopStockAmount - 1"));
            Assert.That(aiInventorySource,
                Does.Contain("SaveShopInventory(true)"));
        }

        [Test]
        public void RuntimeStockAndSharedInventoryDoNotMutateTemplates()
        {
            string shopSource = ReadProjectFile(
                "Assets/_Game/Scripts/Items/CharacterShop.cs");
            string inventorySource = ReadProjectFile(
                "Assets/_Game/Scripts/Characters/Common/Inventory/" +
                "CharacterInventoryManager.cs");
            Assert.That(shopSource, Does.Contain("Instantiate(template)"));
            Assert.That(shopSource,
                Does.Contain("SetShopStockAmount(m_itemAmounts[stockIndex])"));
            Assert.That(inventorySource,
                Does.Contain("candidate.ItemID == item.ItemID"));
            Assert.That(inventorySource,
                Does.Contain("TryRemoveItemAmount(item.CurrentItemAmount)"));
        }

        [Test]
        public void BuySellShareSlotsAndCategoryRules()
        {
            string shopUISource = ReadProjectFile(
                "Assets/_Game/Scripts/Characters/Player/Player UI/" +
                "PlayerUIShopManager.cs");
            string slotSource = ReadProjectFile(
                "Assets/_Game/Scripts/Characters/Player/Player UI/" +
                "UIShopInventorySlot.cs");
            Assert.That(shopUISource,
                Does.Contain("GetOrCreateSlot(activeSlotCount)"));
            Assert.That(shopUISource,
                Does.Contain("ShopItemCategory.Armor => item is ArmorItem"));
            Assert.That(shopUISource,
                Does.Contain("ShopItemCategory.Weapons => item is WeaponItem"));
            Assert.That(shopUISource, Does.Contain("% categoryCount"));
            Assert.That(slotSource,
                Does.Contain("m_shopManager?.BuyOrSellItem(CurrentItem)"));
        }

        [Test]
        public void CategoryActionsSupportKeyboardAndGamepad()
        {
            string inputJson = ReadProjectFile(k_InputActionsPath);
            Assert.That(inputJson, Does.Contain("Previous Shop Category"));
            Assert.That(inputJson, Does.Contain("Next Shop Category"));
            Assert.That(inputJson, Does.Contain("<Gamepad>/leftShoulder"));
            Assert.That(inputJson, Does.Contain("<Gamepad>/rightShoulder"));
            Assert.That(inputJson, Does.Contain("<Keyboard>/q"));
            Assert.That(inputJson, Does.Contain("<Keyboard>/e"));
        }

        private static int GetItemValue(string assetPath)
        {
            Object item = AssetDatabase.LoadMainAssetAtPath(assetPath);
            return new SerializedObject(item).FindProperty("m_itemValue").intValue;
        }

        private static string ReadProjectFile(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return File.ReadAllText(Path.Combine(projectRoot, projectRelativePath));
        }
    }
}
