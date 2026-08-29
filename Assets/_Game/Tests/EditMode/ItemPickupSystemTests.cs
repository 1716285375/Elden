using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class ItemPickupSystemTests
    {
        private const string k_PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_ItemPath =
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset";

        [Test]
        public void RuntimeInventoryAddsAndRemovesItemsThroughItsPublicBoundary()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                ScriptableObject item = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    k_ItemPath);
                Assert.That(item, Is.Not.Null);
                Component inventory = playerRoot.GetComponent("PlayerInventoryManager");
                Assert.That(inventory, Is.Not.Null);
                PropertyInfo itemsProperty = inventory.GetType().GetProperty("ItemsInInventory");
                MethodInfo addMethod = inventory.GetType().GetMethod("AddItemToInventory");
                MethodInfo removeMethod = inventory.GetType().GetMethod("RemoveItemFromInventory");
                Assert.That(itemsProperty, Is.Not.Null);
                Assert.That(addMethod, Is.Not.Null);
                Assert.That(removeMethod, Is.Not.Null);
                IList items = (IList)itemsProperty.GetValue(inventory);
                int initialCount = items.Count;

                Assert.That(addMethod.Invoke(inventory, new object[] { item }), Is.True);
                Assert.That(items.Count, Is.EqualTo(initialCount + 1));
                Assert.That(removeMethod.Invoke(inventory, new object[] { item }), Is.True);
                Assert.That(items.Count, Is.EqualTo(initialCount));
                Assert.That(addMethod.Invoke(inventory, new object[] { null }), Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }
    }
}
