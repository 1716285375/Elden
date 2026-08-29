using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class WeaponUpgradeSystemTests
    {
        private const string k_SmallMaterialPath =
            "Assets/Data/Items/Upgrade Materials/Small Smithing Stone.asset";
        private const string k_MediumMaterialPath =
            "Assets/Data/Items/Upgrade Materials/Medium Smithing Stone.asset";
        private const string k_LargeMaterialPath =
            "Assets/Data/Items/Upgrade Materials/Large Smithing Stone.asset";
        private const string k_DatabasePrefabPath =
            "Assets/Data/Prefabs/Word Managers/World Item Database.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";
        private const string k_AnvilPrefabPath =
            "Assets/Data/Prefabs/World Objects/Weapon Upgrade Anvil.prefab";
        private const string k_MaterialPickupPath =
            "Assets/Data/Prefabs/Interactables/Small Smithing Stone Pickup.prefab";
        private const string k_BlacksmithDialoguePath =
            "Assets/Data/Dialogue/Blacksmith/Blacksmith Stage 00.asset";
        private const string k_BlacksmithPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Blacksmith NPC.prefab";
        private const string k_WorldAIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/World AI Manager.prefab";
        private const string k_WorldSaveManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/World Save Game Manager.prefab";
        private const string k_NetworkPrefabsPath =
            "Assets/_Game/Settings/Networking/DefaultNetworkPrefabs.asset";

        [Test]
        public void UpgradeDamageAddsOnlyToAuthoredChannels()
        {
            Type weaponManagerType = GetRuntimeType("ZZ.WeaponManager");
            Type upgradeLevelType = GetRuntimeType("ZZ.UpgradeLevel");
            MethodInfo getDamage = weaponManagerType.GetMethod(
                "GetUpgradedDamage",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(InvokeDamage(getDamage, upgradeLevelType, 100f, "Level1"),
                Is.EqualTo(111f));
            Assert.That(InvokeDamage(getDamage, upgradeLevelType, 35f, "Level5"),
                Is.EqualTo(90f));
            Assert.That(InvokeDamage(getDamage, upgradeLevelType, 0f, "Level10"),
                Is.Zero);
        }

        [Test]
        public void UpgradeCostsProgressAcrossThreeMaterialTiers()
        {
            AssertCost("Level0", "Small", 1);
            AssertCost("Level1", "Small", 2);
            AssertCost("Level2", "Small", 4);
            AssertCost("Level3", "Medium", 1);
            AssertCost("Level6", "Large", 1);
            AssertCost("Level9", "Large", 6);
            Assert.That(TryGetCost("Level10", out _, out _), Is.False);
        }

        [Test]
        public void WeaponUpgradeLevelSurvivesJsonRoundTrip()
        {
            SerializableWeapon source = new(2, 8, 7);
            SerializableWeapon restored = JsonUtility.FromJson<SerializableWeapon>(
                JsonUtility.ToJson(source));

            Assert.That(restored.ItemID, Is.EqualTo(2));
            Assert.That(restored.AshOfWarID, Is.EqualTo(8));
            Assert.That(restored.UpgradeLevel, Is.EqualTo(7));
        }

        [Test]
        public void StackAndBlacksmithProgressSurviveCharacterSaveRoundTrip()
        {
            CharacterSaveData source = new() { BlacksmithStageID = 5 };
            source.StackableItemsInInventory.Add(
                new SerializableItemStack(16, 12));
            CharacterSaveData restored = JsonUtility.FromJson<CharacterSaveData>(
                JsonUtility.ToJson(source));

            Assert.That(restored.BlacksmithStageID, Is.EqualTo(5));
            Assert.That(restored.StackableItemsInInventory, Has.Count.EqualTo(1));
            Assert.That(restored.StackableItemsInInventory[0].ItemID,
                Is.EqualTo(16));
            Assert.That(restored.StackableItemsInInventory[0].ItemAmount,
                Is.EqualTo(12));
        }

        [Test]
        public void ItemStackRemovalIsAtomicAndClamped()
        {
            UnityEngine.Object template = AssetDatabase.LoadMainAssetAtPath(
                k_SmallMaterialPath);
            UnityEngine.Object material = UnityEngine.Object.Instantiate(template);
            try
            {
                SerializedObject serializedMaterial = new(material);
                serializedMaterial.FindProperty("m_maxItemAmount").intValue = 99;
                serializedMaterial.FindProperty("m_currentItemAmount").intValue = 7;
                serializedMaterial.ApplyModifiedPropertiesWithoutUndo();
                MethodInfo removeAmount = material.GetType().GetMethod(
                    "TryRemoveItemAmount",
                    BindingFlags.Instance | BindingFlags.Public);

                Assert.That(removeAmount.Invoke(material, new object[] { 8 }),
                    Is.False);
                Assert.That(GetProperty<int>(material, "CurrentItemAmount"),
                    Is.EqualTo(7));
                Assert.That(removeAmount.Invoke(material, new object[] { 4 }),
                    Is.True);
                Assert.That(GetProperty<int>(material, "CurrentItemAmount"),
                    Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void DatabaseCreatesCatalogBackedIndependentUpgradeCost()
        {
            Component database = LoadComponent(
                k_DatabasePrefabPath,
                "ZZ.WorldItemDatabase");
            UnityEngine.Object template = AssetDatabase.LoadMainAssetAtPath(
                k_SmallMaterialPath);
            Type stoneType = GetRuntimeType("ZZ.UpgradeStone");
            MethodInfo createCost = database.GetType().GetMethod(
                "CreateUpgradeMaterialCost",
                BindingFlags.Instance | BindingFlags.Public);
            UnityEngine.Object cost = (UnityEngine.Object)createCost.Invoke(
                database,
                new[] { Enum.Parse(stoneType, "Small"), (object)4 });
            try
            {
                Assert.That(cost, Is.Not.Null);
                Assert.That(cost, Is.Not.SameAs(template));
                Assert.That(GetProperty<int>(cost, "ItemID"),
                    Is.EqualTo(GetProperty<int>(template, "ItemID")));
                Assert.That(GetProperty<int>(cost, "CurrentItemAmount"),
                    Is.EqualTo(4));
                Assert.That(cost.hideFlags & HideFlags.DontSave, Is.Not.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cost);
            }
        }

        [Test]
        public void AuthoredMaterialsAreStackableAndRegisteredExactlyOnce()
        {
            Component database = LoadComponent(
                k_DatabasePrefabPath,
                "ZZ.WorldItemDatabase");
            UnityEngine.Object[] materials =
            {
                AssetDatabase.LoadMainAssetAtPath(k_SmallMaterialPath),
                AssetDatabase.LoadMainAssetAtPath(k_MediumMaterialPath),
                AssetDatabase.LoadMainAssetAtPath(k_LargeMaterialPath)
            };
            object[] allItems = GetEnumerableProperty(database, "Items");
            object[] upgradeMaterials = GetEnumerableProperty(
                database,
                "UpgradeMaterials");

            Assert.That(materials.All(material => material != null), Is.True);
            Assert.That(materials.All(material =>
                GetProperty<bool>(material, "IsStackable")), Is.True);
            Assert.That(materials.Select(material =>
                GetProperty<int>(material, "ItemID")).Distinct().Count(),
                Is.EqualTo(3));
            foreach (UnityEngine.Object material in materials)
            {
                Assert.That(allItems.Count(item => ReferenceEquals(item, material)),
                    Is.EqualTo(1));
                Assert.That(upgradeMaterials.Count(item =>
                    ReferenceEquals(item, material)), Is.EqualTo(1));
            }
        }

        [Test]
        public void UpgradeMenuOwnsConfirmationFeedbackAndNonSpatialAudio()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_PlayerUIPrefabPath);
            Component upgradeManager = prefab.GetComponent(
                GetRuntimeType("ZZ.PlayerUIWeaponUpgradeManager"));
            SerializedObject serializedUpgrade = new(upgradeManager);

            Assert.That(upgradeManager, Is.Not.Null);
            Assert.That(prefab.GetComponent<AudioSource>().spatialBlend, Is.Zero);
            Assert.That(serializedUpgrade.FindProperty("m_confirmationPopup")
                .objectReferenceValue, Is.Not.Null);
            Assert.That(serializedUpgrade.FindProperty("m_currentMaterialsText")
                .objectReferenceValue, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Transform>(true)
                .Count(transform => transform.name == "Upgrade Weapon Button"),
                Is.EqualTo(1));
        }

        [Test]
        public void AnvilIsReusableAndPickupGrantsCatalogMaterial()
        {
            GameObject anvil = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_AnvilPrefabPath);
            Component interactable = anvil.GetComponent(
                GetRuntimeType("ZZ.AnvilInteractable"));
            SerializedObject serializedAnvil = new(interactable);
            GameObject pickup = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_MaterialPickupPath);
            Component pickupInteractable = pickup.GetComponent(
                GetRuntimeType("ZZ.PickupItemInteractable"));
            object pickupItem = GetProperty<object>(pickupInteractable, "Item");

            Assert.That(anvil.GetComponent(GetRuntimeType(
                "Unity.Netcode.NetworkObject")), Is.Not.Null);
            Assert.That(serializedAnvil.FindProperty(
                "m_shouldDisableColliderAfterInteraction").boolValue, Is.False);
            Assert.That(pickup.GetComponent(GetRuntimeType(
                "Unity.Netcode.NetworkObject")), Is.Not.Null);
            Assert.That(pickupItem.GetType().FullName,
                Is.EqualTo("ZZ.UpgradeMaterial"));
            Assert.That(GetProperty<int>(pickupItem, "CurrentItemAmount"),
                Is.EqualTo(12));
        }

        [Test]
        public void BlacksmithDialogueOpensSharedServiceAndPersistsStage()
        {
            UnityEngine.Object dialogue = AssetDatabase.LoadMainAssetAtPath(
                k_BlacksmithDialoguePath);
            GameObject blacksmith = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BlacksmithPrefabPath);
            Component soundFX = blacksmith.GetComponentInChildren(
                GetRuntimeType("ZZ.AICharacterSoundFXManager"),
                true);
            Component saveManager = LoadComponent(
                k_WorldSaveManagerPrefabPath,
                "ZZ.WorldSaveGameManager");
            SerializedProperty dialogues = new SerializedObject(saveManager)
                .FindProperty("m_blacksmithDialogues");

            Assert.That(GetProperty<object>(dialogue, "DialogueEndEvent").ToString(),
                Is.EqualTo("Blacksmith"));
            Assert.That(GetProperty<object>(soundFX, "CharacterDialogueID").ToString(),
                Is.EqualTo("Blacksmith"));
            Assert.That(dialogues.arraySize, Is.EqualTo(1));
            Assert.That(dialogues.GetArrayElementAtIndex(0).objectReferenceValue,
                Is.SameAs(dialogue));
        }

        [Test]
        public void WorldPrefabsExposeAllThreeUpgradeEntries()
        {
            GameObject worldManager = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_WorldAIManagerPrefabPath);
            Assert.That(worldManager.transform.Find("Blacksmith NPC Spawner"),
                Is.Not.Null);
            Assert.That(worldManager.transform.Find("Weapon Upgrade Anvil Spawner"),
                Is.Not.Null);
            Assert.That(worldManager.transform.Find(
                "Small Smithing Stone Pickup Spawner"), Is.Not.Null);
        }

        [Test]
        public void UpgradeNetworkPrefabsAreRegisteredExactlyOnce()
        {
            AssertNetworkPrefabRegisteredExactlyOnce(k_BlacksmithPrefabPath);
            AssertNetworkPrefabRegisteredExactlyOnce(k_AnvilPrefabPath);
            AssertNetworkPrefabRegisteredExactlyOnce(k_MaterialPickupPath);
        }

        [Test]
        public void InventoryAndDialogueSourcesPreserveRequiredFlowBoundaries()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string inventorySource = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets/Script/Character/Inventory/PlayerInventoryManager.cs"));
            string dialogueSource = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets/Script/Dialogue/CharacterDialogue.cs"));
            string upgradeSource = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets/Script/Character/Player/Player UI/" +
                "PlayerUIWeaponUpgradeManager.cs"));

            Assert.That(inventorySource,
                Does.Contain("candidate.ItemID == item.ItemID"));
            Assert.That(inventorySource,
                Does.Contain("TryRemoveItemAmount(item.CurrentItemAmount)"));
            Assert.That(upgradeSource, Does.Contain("AttemptToUpgradeWeapon"));
            Assert.That(upgradeSource,
                Does.Contain("RemoveItemFromInventory(m_currentUpgradeCost)"));
            Assert.That(dialogueSource,
                Does.Contain("OpenMenuAfterFixedFrame"));
            Assert.That(dialogueSource,
                Does.Contain("CloseMenuAfterFixedFrame"));
        }

        public static void RunAllFocusedTests()
        {
            WeaponUpgradeSystemTests tests = new();
            tests.UpgradeDamageAddsOnlyToAuthoredChannels();
            tests.UpgradeCostsProgressAcrossThreeMaterialTiers();
            tests.WeaponUpgradeLevelSurvivesJsonRoundTrip();
            tests.StackAndBlacksmithProgressSurviveCharacterSaveRoundTrip();
            tests.ItemStackRemovalIsAtomicAndClamped();
            tests.DatabaseCreatesCatalogBackedIndependentUpgradeCost();
            tests.AuthoredMaterialsAreStackableAndRegisteredExactlyOnce();
            tests.UpgradeMenuOwnsConfirmationFeedbackAndNonSpatialAudio();
            tests.AnvilIsReusableAndPickupGrantsCatalogMaterial();
            tests.BlacksmithDialogueOpensSharedServiceAndPersistsStage();
            tests.WorldPrefabsExposeAllThreeUpgradeEntries();
            tests.UpgradeNetworkPrefabsAreRegisteredExactlyOnce();
            tests.InventoryAndDialogueSourcesPreserveRequiredFlowBoundaries();
            Debug.Log("[WeaponUpgradeSystemTests] 13 focused tests passed.");
        }

        public static void RunRegressionTests()
        {
            ComplexItemSaveSystemTests complexItemTests = new();
            complexItemTests.ComplexEquipmentAndInventorySurviveJsonRoundTrip();
            complexItemTests.DatabaseCreatesIsolatedWeaponInstancesAndUnarmedFallback();
            CharacterDialogueSystemTests.RuntimeCopyOwnsIndependentDialogueProgress();
            CharacterDialogueSystemTests.DialogueStageSurvivesSaveJsonRoundTrip();
            CharacterDialogueSystemTests.SaveManagerOwnsStageLookupAndRuntimeCopyCreation();
            EquipmentUISystemTests equipmentTests = new();
            equipmentTests.EquipmentFilterAcceptsOnlyItemsForTheSelectedSlot();
            equipmentTests.PlayerUIPrefabContainsTheCompleteEquipmentMenuWorkflow();
            Debug.Log("[WeaponUpgradeSystemTests] 7 regression tests passed.");
        }

        private static float InvokeDamage(
            MethodInfo method,
            Type upgradeLevelType,
            float baseDamage,
            string levelName)
        {
            return (float)method.Invoke(
                null,
                new[] { (object)baseDamage, Enum.Parse(upgradeLevelType, levelName) });
        }

        private static void AssertCost(
            string levelName,
            string expectedStone,
            int expectedAmount)
        {
            Assert.That(TryGetCost(levelName, out string stone, out int amount),
                Is.True);
            Assert.That(stone, Is.EqualTo(expectedStone));
            Assert.That(amount, Is.EqualTo(expectedAmount));
        }

        private static bool TryGetCost(
            string levelName,
            out string stoneName,
            out int amount)
        {
            Type rulesType = GetRuntimeType("ZZ.WeaponUpgradeRules");
            Type levelType = GetRuntimeType("ZZ.UpgradeLevel");
            MethodInfo method = rulesType.GetMethod(
                "TryGetUpgradeCost",
                BindingFlags.Public | BindingFlags.Static);
            object[] arguments =
            {
                Enum.Parse(levelType, levelName),
                null,
                null
            };
            bool result = (bool)method.Invoke(null, arguments);
            stoneName = arguments[1]?.ToString();
            amount = arguments[2] is int requiredAmount ? requiredAmount : 0;
            return result;
        }

        private static Component LoadComponent(
            string prefabPath,
            string componentTypeName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Component component = prefab?.GetComponent(GetRuntimeType(componentTypeName));
            return component != null
                ? component
                : throw new AssertionException(
                    $"{prefabPath} is missing {componentTypeName}.");
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            object value = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(target);
            return value is T typedValue
                ? typedValue
                : (T)value;
        }

        private static object[] GetEnumerableProperty(
            object target,
            string propertyName)
        {
            IEnumerable values = GetProperty<IEnumerable>(target, propertyName);
            return values.Cast<object>().ToArray();
        }

        private static void AssertNetworkPrefabRegisteredExactlyOnce(
            string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            UnityEngine.Object prefabsList =
                AssetDatabase.LoadMainAssetAtPath(k_NetworkPrefabsPath);
            SerializedProperty entries = new SerializedObject(prefabsList)
                .FindProperty("List");
            int matches = 0;
            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                if (entries.GetArrayElementAtIndex(entryIndex)
                        .FindPropertyRelative("Prefab").objectReferenceValue == prefab)
                {
                    matches++;
                }
            }

            Assert.That(matches, Is.EqualTo(1), prefabPath);
        }

        private static Type GetRuntimeType(string fullName)
        {
            string assemblyName = fullName.StartsWith(
                "Unity.Netcode.",
                StringComparison.Ordinal)
                    ? "Unity.Netcode.Runtime"
                    : "Assembly-CSharp";
            return Type.GetType($"{fullName}, {assemblyName}") ??
                throw new AssertionException($"Could not resolve {fullName}.");
        }
    }
}
