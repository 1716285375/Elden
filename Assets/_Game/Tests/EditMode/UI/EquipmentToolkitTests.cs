using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace ZZ.Tests
{
    public class EquipmentToolkitTests
    {
        [Test]
        public void ItemCategoryActionsSupportKeyboardAndGamepadShoulders()
        {
            Type type = Type.GetType("PlayerControls, Assembly-CSharp", false) ??
                Type.GetType("ZZ.PlayerControls, Assembly-CSharp", true);
            object controls = Activator.CreateInstance(type);
            var asset = (InputActionAsset)type.GetProperty("asset").GetValue(controls);
            try
            {
                foreach (var pair in new[]
                {
                    ("Previous Shop Category", "<Keyboard>/q", "<Gamepad>/leftShoulder"),
                    ("Next Shop Category", "<Keyboard>/e", "<Gamepad>/rightShoulder")
                })
                {
                    InputAction action = asset.FindAction("UI/" + pair.Item1, true);
                    Assert.That(action.bindings.Any(binding => binding.effectivePath == pair.Item2), Is.True);
                    Assert.That(action.bindings.Any(binding => binding.effectivePath == pair.Item3), Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void EveryItemTabExcludesWeaponsAndArmorAndClassifiesOwnedItems()
        {
            Type view = Type.GetType("ZZ.InventoryMenuView, Assembly-CSharp", true);
            var method = view.GetMethod("GetItemCategory");
            Assert.That(method.Invoke(null, new object[] { null }), Is.EqualTo(-1));
            foreach (string guid in AssetDatabase.FindAssets("t:Item", new[] { "Assets/_Game/Data/Items" }))
            {
                UnityEngine.Object item = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                int category = (int)method.Invoke(null, new object[] { item });
                bool equipment = Type.GetType("ZZ.EquipmentItem, Assembly-CSharp", true).IsInstanceOfType(item);
                Assert.That(category, equipment ? Is.EqualTo(-1) : Is.InRange(0, 7), item.name);
            }
        }

        [Test]
        public void InventoryCanCloseBeforeItsWindowHasEverBeenCreated()
        {
            var instance = new GameObject("Unopened inventory lifecycle");
            try
            {
                Type type = Type.GetType("ZZ.PlayerUIInventoryManager, Assembly-CSharp", true);
                Component menu = instance.AddComponent(type);
                Assert.DoesNotThrow(() => type.GetMethod("CloseMenu").Invoke(menu, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void InventoryUsesItsOwnDocumentAndHasNoEquipmentCommands()
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/InventoryMenu");
            Assert.That(layout, Is.Not.Null);
            VisualElement root = layout.CloneTree();
            Assert.That(root.Q<ListView>("inventory-list"), Is.Not.Null);
            Assert.That(root.Q<Button>("back"), Is.Not.Null);
            foreach (string equipmentControl in new[] { "equip", "unequip", "equipment-slots", "tab-equipment" })
            {
                Assert.That(root.Q(equipmentControl), Is.Null, equipmentControl);
            }
            Assert.That(root.Q("category-weapons"), Is.Null);
            Assert.That(root.Q("category-armor"), Is.Null);
            Assert.That(root.Q("category-all"), Is.Null);
            Assert.That(root.Q("item-categories"), Is.Not.Null);
            Assert.That(root.Q("item-category-name"), Is.Not.Null);
            Assert.That(Type.GetType("ZZ.PlayerUIInventoryManager, Assembly-CSharp", true),
                Is.Not.EqualTo(Type.GetType("ZZ.PlayerUIEquipmentManager, Assembly-CSharp", true)));
        }

        [Test]
        public void CatalogItemsBelongToExactlyOneOfWeaponsArmorAndItems()
        {
            Type filter = Type.GetType("ZZ.ItemInventoryFilter, Assembly-CSharp", true);
            Type category = Type.GetType("ZZ.InventoryCategory, Assembly-CSharp", true);
            Type weapon = Type.GetType("ZZ.WeaponItem, Assembly-CSharp", true);
            Type armor = Type.GetType("ZZ.ArmorItem, Assembly-CSharp", true);
            int checkedItems = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Item", new[] { "Assets/_Game/Data/Items" }))
            {
                UnityEngine.Object item = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                bool unarmed = weapon.IsInstanceOfType(item) &&
                    (bool)weapon.GetProperty("IsUnarmed").GetValue(item);
                int matches = 0;
                foreach (object candidate in Enum.GetValues(category))
                {
                    bool actual = (bool)filter.GetMethod("Matches").Invoke(null, new[] { item, candidate });
                    bool expected = !unarmed && (candidate.ToString() switch
                    {
                        "Weapons" => weapon.IsInstanceOfType(item),
                        "Armor" => armor.IsInstanceOfType(item),
                        "Items" => !weapon.IsInstanceOfType(item) && !armor.IsInstanceOfType(item),
                        _ => false
                    });
                    Assert.That(actual, Is.EqualTo(expected), item.name + ": " + candidate);
                    matches += actual ? 1 : 0;
                }
                Assert.That(matches, Is.EqualTo(unarmed ? 0 : 1), item.name);
                checkedItems++;
            }
            Assert.That(checkedItems, Is.GreaterThan(10));
            Assert.That(filter.GetMethod("Matches").Invoke(null,
                new[] { null, Enum.Parse(category, "Items") }), Is.False);
        }

        [Test]
        public void EquipmentLayoutProvidesSearchVirtualizedListAndExplicitCommands()
        {
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("UI/EquipmentMenu");
            Assert.That(layout, Is.Not.Null);
            VisualElement root = layout.CloneTree();
            Assert.That(root.Q<TextField>("item-search"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("item-sort"), Is.Not.Null);
            Assert.That(root.Q<ListView>("inventory-list"), Is.Not.Null);
            foreach (string command in new[] { "equip", "unequip", "back" })
            {
                Assert.That(root.Q<Button>(command), Is.Not.Null, command);
            }
            Assert.That(root.Q("comparison-stats"), Is.Not.Null);
            Assert.That(root.Q<Button>("browse-all"), Is.Null, "Equipment must not contain an inventory switch");
        }

        [Test]
        public void InventoryNotificationsExcludeFailedMutationsAndEmptyClears()
        {
            Type type = Type.GetType("ZZ.CharacterInventoryManager, Assembly-CSharp", true);
            GameObject owner = new GameObject("Inventory Notification Test");
            ScriptableObject template = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset");
            ScriptableObject item = UnityEngine.Object.Instantiate(template);
            try
            {
                Component inventory = owner.AddComponent(type);
                int changes = 0;
                type.GetEvent("InventoryChanged").AddEventHandler(inventory, (Action)(() => changes++));
                Assert.That(type.GetMethod("AddItemToInventory").Invoke(inventory, new object[] { null }), Is.False);
                Assert.That(changes, Is.Zero);
                Assert.That(type.GetMethod("AddItemToInventory").Invoke(inventory, new object[] { item }), Is.True);
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(type.GetMethod("RemoveItemFromInventory").Invoke(inventory, new object[] { item }), Is.True);
                Assert.That(changes, Is.EqualTo(2));
                Assert.That(type.GetMethod("RemoveItemFromInventory").Invoke(inventory, new object[] { item }), Is.False);
                type.GetMethod("ClearRuntimeInventory").Invoke(inventory, null);
                Assert.That(changes, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(item);
            }
        }
    }
}
