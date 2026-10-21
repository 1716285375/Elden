using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ZZ.Editor
{
    /// <summary>Exercises native equipment controls against an unsaved local player.</summary>
    public static class EquipmentToolkitSmoke
    {
        private const string k_Report = ".utmp/equipment-toolkit-smoke.txt";

        [MenuItem("Tools/ZZ/UI Toolkit/Verify Equipment Inventory")]
        public static void Verify()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved gameplay smoke session.");
            }
            save.StartCoroutine(Observe());
        }

        private static IEnumerator Observe()
        {
            File.WriteAllText(k_Report, "RUNNING\n");
            PlayerUIManager ui = PlayerUIManager.Instance;
            PlayerInventoryManager inventory = ui.LocalPlayer.InventoryManager;
            WeaponItem weapon = WorldItemDatabase.Instance.GetRuntimeItemByID(1) as WeaponItem;
            Check(weapon != null && inventory.AddItemToInventory(weapon), "Inventory accepts a runtime weapon fixture");
            weapon.SetUpgradeLevel((UpgradeLevel)2);
            int weaponID = weapon.ItemID;
            int beforeCount = CountOwnedWeapons(inventory, weaponID);
            PlayerUIEquipmentManager manager = ui.PlayerUIEquipmentManager;
            manager.OpenWeaponMenu();
            yield return null;
            yield return null;
            EquipmentMenuView view = UnityEngine.Object.FindFirstObjectByType<EquipmentMenuView>();
            Check(view != null, "Equipment uses a native document");
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            Check(root.Query<Button>(className: "equipment-slot").ToList().Count == 6, "Only six handheld weapon slots exist");
            Submit(root.Q<Button>("equipment-slot-0"));
            yield return null;
            ListView list = root.Q<ListView>("inventory-list");
            var candidates = (IList)typeof(EquipmentMenuView).GetField("m_items",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            Check(manager.IsEquipmentInventoryOpen && candidates.Contains(weapon),
                "Weapon slot filters compatible inventory candidates");
            TextField search = root.Q<TextField>("item-search");
            search.value = "__no_such_inventory_item__";
            Check(candidates.Count == 0 && !root.Q<Button>("equip").enabledSelf,
                "Empty search disables equipment command");
            search.value = weapon.ItemName;
            Check(candidates.Contains(weapon), "Search restores a matching item");
            search.Focus();
            yield return null;
            Check(manager.IsEditingSearchText, "Search field reports text editing focus");
            PlayerUIEquipmentManagerInputManager input = ui.GetComponentInChildren<PlayerUIEquipmentManagerInputManager>(true);
            Item beforeInput = inventory.GetEquipmentSlotItem(EquipmentSlotType.RightWeapon01);
            typeof(PlayerUIEquipmentManagerInputManager).GetMethod("OnUnequipItemPerformed",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(input,
                    new object[] { default(InputAction.CallbackContext) });
            typeof(PlayerUIEquipmentManagerInputManager).GetMethod("Update",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(input, null);
            Check(inventory.GetEquipmentSlotItem(EquipmentSlotType.RightWeapon01) == beforeInput,
                "Typing cannot trigger unequip shortcut");
            root.Q<Button>("item-grid-cell-" + candidates.IndexOf(weapon)).Focus();
            root.Q<Button>("equip").Focus();
            Submit(root.Q<Button>("equip"));
            yield return null;
            Check(!manager.IsEquipmentInventoryOpen &&
                inventory.GetEquipmentSlotItem(EquipmentSlotType.RightWeapon01) is WeaponItem equippedWeapon &&
                equippedWeapon.ItemID == weaponID && (int)equippedWeapon.UpgradeLevel == 2,
                "Native equipment command equips and returns to slots");
            Check(CountOwnedWeapons(inventory, weaponID) == beforeCount, "Equipment preserves total item count");
            Submit(root.Q<Button>("unequip"));
            yield return null;
            Item equipped = inventory.GetEquipmentSlotItem(EquipmentSlotType.RightWeapon01);
            Check(equipped == null || equipped is WeaponItem unarmed && unarmed.IsUnarmed,
                "Native unequip restores an empty or unarmed slot");
            Check(CountOwnedWeapons(inventory, weaponID) == beforeCount, "Unequip preserves total item count");
            File.AppendAllText(k_Report, "ALL PASSED\n");
        }

        private static int CountOwnedWeapons(PlayerInventoryManager inventory, int itemID)
        {
            int count = inventory.GetItemAmount(itemID);
            for (int index = 0; index < 6; index++)
            {
                if (inventory.GetEquipmentSlotItem((EquipmentSlotType)index)?.ItemID == itemID)
                {
                    count++;
                }
            }
            return count;
        }

        private static void Submit(Button button)
        {
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        private static void Check(bool condition, string message)
        {
            File.AppendAllText(k_Report, (condition ? "PASS: " : "FAIL: ") + message + "\n");
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
