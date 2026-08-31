using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ZZ.Tests
{
    public class ItemStorageSystemTests
    {
        [Test]
        public void StorageAndInventoryRemainIndependentAcrossJsonRoundTrip()
        {
            CharacterSaveData saveData = new();
            saveData.WeaponsInInventory.Add(new SerializableWeapon(1, -1));
            saveData.StorageInventory.Weapons.Add(
                new SerializableWeapon(2, 8));
            saveData.StorageInventory.Projectiles.Add(
                new SerializableRangeProjectile(12, 24));
            saveData.StorageInventory.QuickSlotItems.Add(
                new SerializableQuickSlotItem(14, 2));
            saveData.StorageInventory.StackableItems.Add(
                new SerializableItemStack(9, 7));
            saveData.StorageInventory.HeadEquipment.Add(4);

            CharacterSaveData restored = JsonUtility.FromJson<CharacterSaveData>(
                JsonUtility.ToJson(saveData));

            Assert.That(restored.WeaponsInInventory, Has.Count.EqualTo(1));
            Assert.That(restored.StorageInventory.Weapons, Has.Count.EqualTo(1));
            Assert.That(restored.StorageInventory.Weapons[0].AshOfWarID,
                Is.EqualTo(8));
            Assert.That(restored.StorageInventory.Projectiles[0].ItemAmount,
                Is.EqualTo(24));
            Assert.That(restored.StorageInventory.QuickSlotItems[0].ItemAmount,
                Is.EqualTo(2));
            Assert.That(restored.StorageInventory.StackableItems[0].ItemAmount,
                Is.EqualTo(7));
            Assert.That(restored.StorageInventory.HeadEquipment,
                Is.EqualTo(new[] { 4 }));
        }

        [Test]
        public void ClearingStoragePreventsDuplicateSaveEntries()
        {
            SerializableInventoryData storage = new();
            storage.Weapons.Add(new SerializableWeapon(1, 8));
            storage.StackableItems.Add(new SerializableItemStack(9, 3));

            storage.Clear();
            storage.Weapons.Add(new SerializableWeapon(2, -1));

            Assert.That(storage.Weapons, Has.Count.EqualTo(1));
            Assert.That(storage.Weapons[0].ItemID, Is.EqualTo(2));
            Assert.That(storage.StackableItems, Is.Empty);
            Assert.That(storage.Projectiles, Is.Empty);
        }

        [Test]
        public void VersionFifteenSaveMigratesToEmptyStorage()
        {
            string testDirectory = Path.Combine(
                Path.GetTempPath(),
                "EldenStorageSaveTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            try
            {
                File.WriteAllText(
                    Path.Combine(testDirectory, "CharacterSlot01.json"),
                    "{\"m_dataVersion\":15,\"m_characterName\":\"Stored Hero\"}");
                SaveFileDataWriter writer = new(
                    testDirectory,
                    "CharacterSlot01");

                CharacterSaveData restored = writer.LoadSaveFile();

                Assert.That(restored.CharacterName, Is.EqualTo("Stored Hero"));
                Assert.That(restored.StorageInventory, Is.Not.Null);
                Assert.That(restored.StorageInventory.Weapons, Is.Empty);
                Assert.That(restored.StorageInventory.StackableItems, Is.Empty);
            }
            finally
            {
                Directory.Delete(testDirectory, true);
            }
        }

        [Test]
        public void TransferUsesSharedStackRulesAndAtomicContainerMove()
        {
            string baseInventorySource = ReadProjectFile(
                "Assets/_Game/Scripts/Characters/Common/Inventory/" +
                "CharacterInventoryManager.cs");
            string playerInventorySource = ReadProjectFile(
                "Assets/_Game/Scripts/Characters/Common/Inventory/" +
                "PlayerInventoryManager.cs");

            Assert.That(baseInventorySource,
                Does.Contain("TransferItemBetweenCollections"));
            Assert.That(baseInventorySource,
                Does.Contain("CanAddItemToCollection(destinationItems, item)"));
            Assert.That(baseInventorySource,
                Does.Contain("FindCompatibleStack(items, item)"));
            Assert.That(playerInventorySource,
                Does.Contain("MoveItemToStorage"));
            Assert.That(playerInventorySource,
                Does.Contain("MoveItemToInventory"));
            Assert.That(playerInventorySource,
                Does.Contain("AddItemToStorage"));
            Assert.That(playerInventorySource,
                Does.Contain("RemoveItemFromStorage"));
        }

        [Test]
        public void StorageUIRefreshesBothSidesAndFallsBackToStorageSelection()
        {
            string storageUISource = ReadProjectFile(
                "Assets/_Game/Scripts/Characters/Player/Player UI/" +
                "PlayerUIStorageManager.cs");
            string slotSource = ReadProjectFile(
                "Assets/_Game/Scripts/Characters/Player/Player UI/" +
                "UIStorageInventorySlot.cs");

            Assert.That(storageUISource,
                Does.Contain("inventory.ItemsInInventory"));
            Assert.That(storageUISource,
                Does.Contain("inventory.ItemsInStorage"));
            Assert.That(storageUISource,
                Does.Contain("FindFirstActiveSlot(m_storageSlots)"));
            Assert.That(storageUISource,
                Does.Contain("slots[slotIndex].gameObject.SetActive(false)"));
            Assert.That(storageUISource,
                Does.Contain("PlayerUIShopManager.MatchesCategory"));
            Assert.That(slotSource,
                Does.Contain("IsSelectingFromPlayerInventory"));
        }

        [Test]
        public void SiteOfGraceAndGlobalMenuManagerExposeStorage()
        {
            string siteSource = ReadProjectFile(
                "Assets/_Game/Scripts/Characters/Player/Player UI/" +
                "PlayerUISiteOfGraceManager.cs");
            string uiManagerSource = ReadProjectFile(
                "Assets/_Game/Scripts/Characters/Player/Player UI/" +
                "PlayerUIManager.cs");
            string playerSource = ReadProjectFile(
                "Assets/_Game/Scripts/Characters/Player/PlayerManager.cs");

            Assert.That(siteSource, Does.Contain("Storage Button"));
            Assert.That(siteSource, Does.Contain("OpenStorageMenu"));
            Assert.That(uiManagerSource,
                Does.Contain("m_playerUIStorageManager?.CloseMenu()"));
            Assert.That(uiManagerSource,
                Does.Contain("m_playerUIStorageManager?.IsMenuOpen == true"));
            Assert.That(playerSource, Does.Contain("SaveStorageData(currentData)"));
            Assert.That(playerSource,
                Does.Contain("InventoryManager.RestoreStorage(currentData)"));
        }

        private static string ReadProjectFile(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return File.ReadAllText(Path.Combine(projectRoot, projectRelativePath));
        }
    }
}
