using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class QuickSlotUISystemTests
    {
        private const string k_HealthFlaskPath =
            "Assets/_Game/Data/Items/Quick Slot Items/Flask of Crimson Tears.asset";
        private const string k_FocusFlaskPath =
            "Assets/_Game/Data/Items/Quick Slot Items/Flask of Cerulean Tears.asset";
        private const string k_ArrowPath =
            "Assets/_Game/Data/Items/Projectiles/Standard Arrow.asset";
        private const string k_PlayerPrefabPath =
            "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_QuickSlotPrefabPath =
            "Assets/_Game/Prefabs/UI/Quick Slot UI.prefab";
        private const string k_UIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        [Test]
        public void QuickSlotEquipmentEnumsUseStableTrailingValues()
        {
            Type slotType = GetRuntimeType("ZZ.EquipmentSlotType");

            Assert.That(GetEnumValue(slotType, "QuickSlot01"), Is.EqualTo(12));
            Assert.That(GetEnumValue(slotType, "QuickSlot02"), Is.EqualTo(13));
            Assert.That(GetEnumValue(slotType, "QuickSlot03"), Is.EqualTo(14));
        }

        [Test]
        public void SwitchInputSupportsControllerAndKeyboard()
        {
            InputActionsDocument inputAsset = JsonUtility.FromJson<
                InputActionsDocument>(
                File.ReadAllText("Assets/_Game/Settings/Input/PlayerControls.inputactions"));
            InputBindingData[] bindings = inputAsset.maps
                .Single(map => map.name == "Player Movement")
                .bindings
                .Where(binding => binding.action == "Switch Quick Slot Item")
                .ToArray();

            Assert.That(
                bindings.Select(binding => binding.path),
                Does.Contain("<Gamepad>/dpad/down"));
            Assert.That(
                bindings.Select(binding => binding.path),
                Does.Contain("<Keyboard>/downArrow"));
            Assert.That(
                GetRuntimeType("ZZ.PlayerInputManager").GetMethod(
                    "HandleSwitchQuickSlotItemInput",
                    BindingFlags.NonPublic | BindingFlags.Instance),
                Is.Not.Null);
        }

        [Test]
        public void PlayerStartsWithHealthFocusAndEmptyQuickSlots()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                Component inventory = FindComponent(
                    root,
                    "PlayerInventoryManager");
                SerializedObject serializedInventory = new SerializedObject(
                    inventory);
                SerializedProperty quickSlots = serializedInventory.FindProperty(
                    "m_quickSlotItemsInQuickSlots");

                Assert.That(quickSlots.arraySize, Is.EqualTo(3));
                Assert.That(
                    quickSlots.GetArrayElementAtIndex(0).objectReferenceValue,
                    Is.EqualTo(LoadAsset(k_HealthFlaskPath)));
                Assert.That(
                    quickSlots.GetArrayElementAtIndex(1).objectReferenceValue,
                    Is.EqualTo(LoadAsset(k_FocusFlaskPath)));
                Assert.That(
                    quickSlots.GetArrayElementAtIndex(2).objectReferenceValue,
                    Is.Null);
                Assert.That(
                    serializedInventory.FindProperty("m_quickSlotItemIndex")
                        .intValue,
                    Is.Zero);

                inventory.GetType().GetMethod(
                    "InitializeCurrentQuickSlotItemFromID")
                    ?.Invoke(inventory, new object[] { -1 });
                Assert.That(
                    GetProperty<object>(inventory, "CurrentQuickSlotItem"),
                    Is.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void EquipmentMenuContainsThreeQuickSlotsWithCounts()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_UIManagerPrefabPath);
            try
            {
                Component manager = FindComponent(
                    root,
                    "PlayerUIEquipmentManager");
                SerializedObject serializedManager = new SerializedObject(manager);
                SerializedProperty icons = serializedManager.FindProperty(
                    "m_equipmentSlotIcons");
                SerializedProperty buttons = serializedManager.FindProperty(
                    "m_equipmentSlotButtons");
                SerializedProperty quantities = serializedManager.FindProperty(
                    "m_equipmentSlotQuantityTexts");
                string[] transformNames = root
                    .GetComponentsInChildren<Transform>(true)
                    .Select(transform => transform.name)
                    .ToArray();

                Assert.That(icons.arraySize, Is.EqualTo(15));
                Assert.That(buttons.arraySize, Is.EqualTo(15));
                Assert.That(quantities.arraySize, Is.EqualTo(15));
                for (int slotIndex = 12; slotIndex < 15; slotIndex++)
                {
                    Assert.That(
                        quantities.GetArrayElementAtIndex(slotIndex)
                            .objectReferenceValue,
                        Is.Not.Null);
                }

                Assert.That(transformNames, Does.Contain("Quick Slot 01"));
                Assert.That(transformNames, Does.Contain("Quick Slot 02"));
                Assert.That(transformNames, Does.Contain("Quick Slot 03"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void EquipmentFilterAcceptsOnlyQuickSlotItemsForItemSlots()
        {
            Type managerType = GetRuntimeType("ZZ.PlayerUIEquipmentManager");
            Type slotType = GetRuntimeType("ZZ.EquipmentSlotType");
            MethodInfo compatibility = managerType.GetMethod(
                "IsItemCompatibleWithSlot",
                BindingFlags.Public | BindingFlags.Static);
            object quickSlot = Enum.Parse(slotType, "QuickSlot01");

            Assert.That(
                compatibility.Invoke(
                    null,
                    new[] { LoadAsset(k_HealthFlaskPath), quickSlot }),
                Is.True);
            Assert.That(
                compatibility.Invoke(
                    null,
                    new[] { LoadAsset(k_ArrowPath), quickSlot }),
                Is.False);
        }

        [Test]
        public void SwitchingSkipsEmptySlotsButSingleItemTogglesThroughEmpty()
        {
            Type inventoryType = GetRuntimeType("ZZ.PlayerInventoryManager");
            Type itemType = GetRuntimeType("ZZ.QuickSlotItem");
            MethodInfo selectNext = inventoryType.GetMethod(
                "SelectNextQuickSlotItem",
                BindingFlags.NonPublic | BindingFlags.Static);
            UnityEngine.Object healthFlask = LoadAsset(k_HealthFlaskPath);
            UnityEngine.Object focusFlask = LoadAsset(k_FocusFlaskPath);
            Array twoItems = Array.CreateInstance(itemType, 3);
            twoItems.SetValue(healthFlask, 0);
            twoItems.SetValue(focusFlask, 2);

            object[] switchToFocus = { twoItems, healthFlask, 0 };
            Assert.That(
                selectNext.Invoke(null, switchToFocus),
                Is.EqualTo(focusFlask));
            Assert.That(switchToFocus[2], Is.EqualTo(2));

            Array oneItem = Array.CreateInstance(itemType, 3);
            oneItem.SetValue(healthFlask, 0);
            object[] switchToEmpty = { oneItem, healthFlask, 0 };
            Assert.That(selectNext.Invoke(null, switchToEmpty), Is.Null);
            object[] switchBack = { oneItem, null, 0 };
            Assert.That(
                selectNext.Invoke(null, switchBack),
                Is.EqualTo(healthFlask));
        }

        [Test]
        public void ReplacingAndUnequippingCurrentSlotRefreshesCurrentItem()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                Component player = FindComponent(root, "PlayerManager");
                Component inventory = FindComponent(
                    root,
                    "PlayerInventoryManager");
                InvokeAwake(player);
                InvokeAwake(inventory);
                UnityEngine.Object focusFlask = LoadAsset(k_FocusFlaskPath);
                inventory.GetType().GetMethod("AddItemToInventory")?.Invoke(
                    inventory,
                    new[] { focusFlask });
                Type slotType = GetRuntimeType("ZZ.EquipmentSlotType");
                object firstSlot = Enum.Parse(slotType, "QuickSlot01");

                Assert.That(
                    inventory.GetType().GetMethod("EquipItemInSlot")?.Invoke(
                        inventory,
                        new[] { firstSlot, focusFlask }),
                    Is.True);
                Assert.That(
                    GetProperty<object>(inventory, "CurrentQuickSlotItem"),
                    Is.EqualTo(focusFlask));

                Assert.That(
                    inventory.GetType().GetMethod("UnequipItemInSlot")?.Invoke(
                        inventory,
                        new[] { firstSlot }),
                    Is.True);
                Assert.That(
                    GetProperty<object>(inventory, "CurrentQuickSlotItem"),
                    Is.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void FlaskCountIsPolymorphicAndHudShowsZeroThroughThree()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(
                k_PlayerPrefabPath);
            GameObject quickSlotRoot = PrefabUtility.LoadPrefabContents(
                k_QuickSlotPrefabPath);
            try
            {
                Component player = FindComponent(playerRoot, "PlayerManager");
                Component network = FindComponent(
                    playerRoot,
                    "PlayerNetworkManager");
                InvokeAwake(player);
                UnityEngine.Object healthFlask = LoadAsset(k_HealthFlaskPath);
                MethodInfo getCurrentAmount = healthFlask.GetType().GetMethod(
                    "GetCurrentAmount");

                Assert.That(
                    GetProperty<bool>(healthFlask, "IsConsumable"),
                    Is.True);
                Assert.That(
                    getCurrentAmount.Invoke(healthFlask, new object[] { player }),
                    Is.EqualTo(3));

                SetNetworkVariableValue(network, "RemainingHealthFlasks", 0);
                Component quickSlot = FindComponent(quickSlotRoot, "UIQuickSlot");
                quickSlot.GetType().GetMethod("SetQuickSlotItem")?.Invoke(
                    quickSlot,
                    new[] { healthFlask, player });
                AssertQuantity(quickSlot, "0", true);

                quickSlot.GetType().GetMethod("SetQuickSlotItem")?.Invoke(
                    quickSlot,
                    new object[] { null, player });
                AssertQuantity(quickSlot, string.Empty, false);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(quickSlotRoot);
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        [Test]
        public void SwitchingGuardsActionsItemsAndModalMenus()
        {
            string inventorySource = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/Common/Inventory/PlayerInventoryManager.cs");
            string inputSource = File.ReadAllText(
                "Assets/_Game/Scripts/World/Managers/PlayerInputManager.cs");

            Assert.That(inventorySource, Does.Contain("!m_player.IsPerformingAction"));
            Assert.That(inventorySource, Does.Contain("IsUsingItem != true"));
            Assert.That(inventorySource, Does.Contain("IsMenuWindowOpen != true"));
            Assert.That(inputSource, Does.Contain("SwitchQuickSlotItem()"));
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static UnityEngine.Object LoadAsset(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private static Component FindComponent(GameObject root, string typeName)
        {
            Component component = root.GetComponentsInChildren<Component>(true)
                .Single(candidate => candidate.GetType().Name == typeName);
            Assert.That(component, Is.Not.Null);
            return component;
        }

        private static int GetEnumValue(Type enumType, string valueName)
        {
            return Convert.ToInt32(Enum.Parse(enumType, valueName));
        }

        private static void InvokeAwake(Component component)
        {
            component.GetType().GetMethod(
                "Awake",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(component, null);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target?.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(target);
        }

        private static void SetNetworkVariableValue(
            Component network,
            string propertyName,
            int value)
        {
            object networkVariable = network.GetType()
                .GetProperty(propertyName)
                ?.GetValue(network);
            networkVariable?.GetType().GetProperty("Value")
                ?.SetValue(networkVariable, value);
        }

        private static void AssertQuantity(
            Component quickSlot,
            string expectedText,
            bool expectedActive)
        {
            SerializedObject serializedQuickSlot = new SerializedObject(quickSlot);
            Component quantity = serializedQuickSlot.FindProperty("m_quantityText")
                .objectReferenceValue as Component;

            Assert.That(quantity, Is.Not.Null);
            Assert.That(
                GetProperty<string>(quantity, "text"),
                Is.EqualTo(expectedText));
            Assert.That(quantity.gameObject.activeSelf, Is.EqualTo(expectedActive));
        }

        [Serializable]
        private sealed class InputActionsDocument
        {
            public InputActionMapData[] maps;
        }

        [Serializable]
        private sealed class InputActionMapData
        {
            public string name;
            public InputBindingData[] bindings;
        }

        [Serializable]
        private sealed class InputBindingData
        {
            public string action;
            public string path;
        }
    }
}
