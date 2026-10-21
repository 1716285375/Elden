using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class EquipmentUISystemTests
    {
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_WeaponPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset";
        private const string k_HeadArmorPath =
            "Assets/_Game/Data/Items/Armor/Starter Hood.asset";
        private const string k_InventorySlotPrefabPath =
            "Assets/_Game/Prefabs/UI/Equipment Inventory Slot.prefab";

        [Test]
        public void EquipmentFilterAcceptsOnlyItemsForTheSelectedSlot()
        {
            Type managerType = GetRuntimeType("ZZ.PlayerUIEquipmentManager");
            Type slotType = GetRuntimeType("ZZ.EquipmentSlotType");
            MethodInfo filterMethod = managerType.GetMethod(
                "IsItemCompatibleWithSlot",
                BindingFlags.Public | BindingFlags.Static);
            ScriptableObject weapon = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                k_WeaponPath);
            ScriptableObject headArmor = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                k_HeadArmorPath);

            Assert.That(filterMethod, Is.Not.Null);
            Assert.That(weapon, Is.Not.Null);
            Assert.That(headArmor, Is.Not.Null);
            Assert.That(
                InvokeFilter(filterMethod, slotType, weapon, "RightWeapon01"),
                Is.True);
            Assert.That(
                InvokeFilter(filterMethod, slotType, weapon, "Head"),
                Is.False);
            Assert.That(
                InvokeFilter(filterMethod, slotType, headArmor, "Head"),
                Is.True);
            Assert.That(
                InvokeFilter(filterMethod, slotType, headArmor, "Body"),
                Is.False);
        }

        [Test]
        public void PlayerUIPrefabContainsTheCompleteEquipmentMenuWorkflow()
        {
            GameObject playerUIRoot = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Component[] components = playerUIRoot.GetComponentsInChildren<Component>(
                    true);
                Assert.That(
                    components.Any(component =>
                        component?.GetType().Name == "PlayerUICharacterMenuManager"),
                    Is.True);
                Assert.That(
                    components.Any(component =>
                        component?.GetType().Name == "PlayerUIEquipmentManager"),
                    Is.True);
                Transform slotsGrid = FindTransform(playerUIRoot.transform, "Slots Grid");
                Assert.That(slotsGrid, Is.Not.Null);
                Assert.That(slotsGrid.childCount, Is.EqualTo(15));
                GameObject inventorySlotPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        k_InventorySlotPrefabPath);
                Assert.That(inventorySlotPrefab, Is.Not.Null);
                Assert.That(
                    inventorySlotPrefab.GetComponent("UIEquipmentInventorySlot"),
                    Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerUIRoot);
            }
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Could not resolve {fullName}.");
            return type;
        }

        private static bool InvokeFilter(
            MethodInfo filterMethod,
            Type slotType,
            ScriptableObject item,
            string slotName)
        {
            object equipmentSlot = Enum.Parse(slotType, slotName);
            return (bool)filterMethod.Invoke(
                null,
                new object[] { item, equipmentSlot });
        }

        private static Transform FindTransform(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform match = FindTransform(child, objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
