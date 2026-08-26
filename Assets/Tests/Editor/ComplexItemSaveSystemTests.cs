using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class ComplexItemSaveSystemTests
    {
        private const string k_DatabasePrefabPath =
            "Assets/Data/Prefabs/Word Managers/World Item Database.prefab";

        [Test]
        public void ComplexEquipmentAndInventorySurviveJsonRoundTrip()
        {
            CharacterSaveData saveData = new CharacterSaveData
            {
                RightHandWeaponSlot01 = new SerializableWeapon(1, 8),
                RightHandWeaponSlot02 = new SerializableWeapon(1, -1),
                RightHandWeaponIndex = 1,
                MainProjectile = new SerializableRangeProjectile(12, 17),
                SecondaryProjectile = new SerializableRangeProjectile(-1, 0),
                QuickSlotItem01 = new SerializableQuickSlotItem(14, 2),
                QuickSlotItem02 = new SerializableQuickSlotItem(15, 1),
                QuickSlotItemIndex = 1,
                CurrentHealthFlasksRemaining = 2,
                CurrentFocusPointFlasksRemaining = 1
            };
            PopulateInventory(saveData);

            CharacterSaveData restored = JsonUtility.FromJson<CharacterSaveData>(
                JsonUtility.ToJson(saveData));

            Assert.That(restored.RightHandWeaponSlot01.ItemID, Is.EqualTo(1));
            Assert.That(restored.RightHandWeaponSlot01.AshOfWarID, Is.EqualTo(8));
            Assert.That(restored.RightHandWeaponSlot02.ItemID, Is.EqualTo(1));
            Assert.That(restored.RightHandWeaponSlot02.AshOfWarID, Is.EqualTo(-1));
            Assert.That(restored.RightHandWeaponIndex, Is.EqualTo(1));
            Assert.That(restored.MainProjectile.ItemAmount, Is.EqualTo(17));
            Assert.That(restored.SecondaryProjectile.ItemID, Is.EqualTo(-1));
            Assert.That(restored.QuickSlotItemIndex, Is.EqualTo(1));
            Assert.That(restored.CurrentHealthFlasksRemaining, Is.EqualTo(2));
            Assert.That(restored.CurrentFocusPointFlasksRemaining, Is.EqualTo(1));
            Assert.That(restored.WeaponsInInventory, Has.Count.EqualTo(1));
            Assert.That(restored.ProjectilesInInventory, Has.Count.EqualTo(1));
            Assert.That(restored.QuickSlotItemsInInventory, Has.Count.EqualTo(1));
            Assert.That(restored.HeadEquipmentInInventory, Is.EqualTo(new[] { 4 }));
            Assert.That(restored.BodyEquipmentInInventory, Is.EqualTo(new[] { 5 }));
            Assert.That(restored.HandEquipmentInInventory, Is.EqualTo(new[] { 6 }));
            Assert.That(restored.LegEquipmentInInventory, Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void ClearingInventoryBeforeEachSavePreventsDuplicates()
        {
            CharacterSaveData saveData = new CharacterSaveData();
            PopulateInventory(saveData);
            PopulateInventory(saveData);

            Assert.That(saveData.WeaponsInInventory, Has.Count.EqualTo(1));
            Assert.That(saveData.ProjectilesInInventory, Has.Count.EqualTo(1));
            Assert.That(saveData.QuickSlotItemsInInventory, Has.Count.EqualTo(1));
            Assert.That(saveData.HeadEquipmentInInventory, Has.Count.EqualTo(1));
            Assert.That(saveData.BodyEquipmentInInventory, Has.Count.EqualTo(1));
            Assert.That(saveData.HandEquipmentInInventory, Has.Count.EqualTo(1));
            Assert.That(saveData.LegEquipmentInInventory, Has.Count.EqualTo(1));
        }

        [Test]
        public void VersionSevenEquipmentMigratesToComplexItemState()
        {
            string testDirectory = Path.Combine(
                Path.GetTempPath(),
                "EldenComplexSaveTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            try
            {
                File.WriteAllText(
                    Path.Combine(testDirectory, "CharacterSlot01.json"),
                    "{\"m_dataVersion\":7," +
                    "\"m_rightHandWeaponSlot01ID\":2," +
                    "\"m_leftHandWeaponSlot01ID\":3," +
                    "\"m_mainProjectileID\":12," +
                    "\"m_mainProjectileAmount\":19}");
                SaveFileDataWriter writer = new SaveFileDataWriter(
                    testDirectory,
                    "CharacterSlot01");

                CharacterSaveData restored = writer.LoadSaveFile();

                Assert.That(restored.RightHandWeaponSlot01.ItemID, Is.EqualTo(2));
                Assert.That(restored.LeftHandWeaponSlot01.ItemID, Is.EqualTo(3));
                Assert.That(restored.LeftHandWeaponSlot01.AshOfWarID, Is.EqualTo(8));
                Assert.That(restored.MainProjectile.ItemID, Is.EqualTo(12));
                Assert.That(restored.MainProjectile.ItemAmount, Is.EqualTo(19));
                Assert.That(restored.CurrentHealthFlasksRemaining, Is.EqualTo(3));
                Assert.That(restored.CurrentFocusPointFlasksRemaining, Is.EqualTo(1));
            }
            finally
            {
                Directory.Delete(testDirectory, true);
            }
        }

        [Test]
        public void DatabaseCreatesIsolatedWeaponInstancesAndUnarmedFallback()
        {
            Component database = LoadDatabase();
            MethodInfo getWeapon = database.GetType().GetMethod(
                "GetWeaponFromSerializedData",
                BindingFlags.Instance | BindingFlags.Public);

            UnityEngine.Object withAsh = (UnityEngine.Object)getWeapon.Invoke(
                database,
                new object[] { new SerializableWeapon(3, 8) });
            UnityEngine.Object withoutAsh = (UnityEngine.Object)getWeapon.Invoke(
                database,
                new object[] { new SerializableWeapon(3, -1) });
            UnityEngine.Object fallback = (UnityEngine.Object)getWeapon.Invoke(
                database,
                new object[] { new SerializableWeapon(999, 8) });
            try
            {
                Assert.That(withAsh, Is.Not.SameAs(withoutAsh));
                Assert.That(GetItemID(withAsh), Is.EqualTo(3));
                Assert.That(GetAshOfWarID(withAsh), Is.EqualTo(8));
                Assert.That(GetAshOfWarID(withoutAsh), Is.EqualTo(-1));
                Assert.That(GetItemID(fallback), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(withAsh);
                UnityEngine.Object.DestroyImmediate(withoutAsh);
                UnityEngine.Object.DestroyImmediate(fallback);
            }
        }

        [Test]
        public void DatabaseRestoresAmmoAndLeavesEmptySlotNull()
        {
            Component database = LoadDatabase();
            MethodInfo getProjectile = database.GetType().GetMethod(
                "GetProjectileFromSerializedData",
                BindingFlags.Instance | BindingFlags.Public);
            UnityEngine.Object projectile = (UnityEngine.Object)getProjectile.Invoke(
                database,
                new object[] { new SerializableRangeProjectile(12, 17) });
            object emptyProjectile = getProjectile.Invoke(
                database,
                new object[] { new SerializableRangeProjectile(-1, 0) });
            try
            {
                int amount = (int)projectile.GetType()
                    .GetProperty("CurrentAmmoAmount")
                    .GetValue(projectile);
                Assert.That(amount, Is.EqualTo(17));
                Assert.That(emptyProjectile, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(projectile);
            }
        }

        [Test]
        public void DatabaseCreatesQuickSlotRuntimeItemWithSavedAmount()
        {
            Component database = LoadDatabase();
            MethodInfo getQuickSlotItem = database.GetType().GetMethod(
                "GetQuickSlotItemFromSerializedData",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo getQuickSlotTemplate = database.GetType().GetMethod(
                "GetQuickSlotItemByID",
                BindingFlags.Instance | BindingFlags.Public);
            UnityEngine.Object template = (UnityEngine.Object)getQuickSlotTemplate
                .Invoke(database, new object[] { 14 });
            UnityEngine.Object runtimeItem = (UnityEngine.Object)getQuickSlotItem
                .Invoke(
                    database,
                    new object[] { new SerializableQuickSlotItem(14, 2) });
            try
            {
                SerializedProperty currentAmount = new SerializedObject(runtimeItem)
                    .FindProperty("m_currentAmount");
                Assert.That(runtimeItem, Is.Not.SameAs(template));
                Assert.That(currentAmount.intValue, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeItem);
            }
        }

        [Test]
        public void OwnerAndRemoteWeaponReconstructionPathsRemainDistinct()
        {
            string inventorySource = File.ReadAllText(
                "Assets/Script/Character/Inventory/PlayerInventoryManager.cs");
            string networkSource = File.ReadAllText(
                "Assets/Script/Character/Player/PlayerNetworkManager.cs");

            Assert.That(inventorySource, Does.Contain("ResolveOwnedWeaponSlot"));
            Assert.That(inventorySource, Does.Contain("m_player?.IsOwner != true"));
            Assert.That(networkSource, Does.Contain(
                "EquipRightWeaponFromID(currentWeaponID)"));
            Assert.That(networkSource, Does.Contain(
                "EquipLeftWeaponFromID(currentWeaponID)"));
        }

        private static void PopulateInventory(CharacterSaveData saveData)
        {
            saveData.ClearInventoryData();
            saveData.WeaponsInInventory.Add(new SerializableWeapon(1, 8));
            saveData.ProjectilesInInventory.Add(
                new SerializableRangeProjectile(12, 11));
            saveData.QuickSlotItemsInInventory.Add(
                new SerializableQuickSlotItem(14, 2));
            saveData.HeadEquipmentInInventory.Add(4);
            saveData.BodyEquipmentInInventory.Add(5);
            saveData.HandEquipmentInInventory.Add(6);
            saveData.LegEquipmentInInventory.Add(7);
        }

        private static Component LoadDatabase()
        {
            GameObject databasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_DatabasePrefabPath);
            Assert.That(databasePrefab, Is.Not.Null);
            foreach (Component component in databasePrefab.GetComponents<Component>())
            {
                if (component != null &&
                    component.GetType().FullName == "ZZ.WorldItemDatabase")
                {
                    return component;
                }
            }

            Assert.Fail("World Item Database prefab has no WorldItemDatabase component.");
            return null;
        }

        private static int GetItemID(UnityEngine.Object item)
        {
            return (int)item.GetType().GetProperty("ItemID").GetValue(item);
        }

        private static int GetAshOfWarID(UnityEngine.Object weapon)
        {
            object ashOfWar = weapon.GetType()
                .GetProperty("AshOfWarAction")
                .GetValue(weapon);
            return ashOfWar == null ? -1 : GetItemID((UnityEngine.Object)ashOfWar);
        }
    }
}
