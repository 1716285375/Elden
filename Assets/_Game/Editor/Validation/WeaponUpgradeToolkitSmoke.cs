using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ZZ.Editor
{
    /// <summary>Checks native strengthening without writing a character save.</summary>
    public static class WeaponUpgradeToolkitSmoke
    {
        private const string k_Report = ".utmp/weapon-upgrade-toolkit-smoke.txt";

        [MenuItem("Tools/ZZ/UI Toolkit/Verify Weapon Upgrade")]
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
            WeaponItem fixture = WorldItemDatabase.Instance.GetRuntimeItemByID(1) as WeaponItem;
            Check(fixture != null && inventory.AddItemToInventory(fixture) &&
                inventory.EquipItemInSlot(EquipmentSlotType.RightWeapon01, fixture), "Unsaved right-hand weapon equipped");
            yield return null;
            WeaponItem weapon = inventory.CurrentRightHandWeapon;
            weapon.SetUpgradeLevel(UpgradeLevel.Level0);
            PlayerUIWeaponUpgradeManager manager = ui.PlayerUIWeaponUpgradeManager;
            manager.OpenWeaponUpgradeMenu();
            yield return null;
            WeaponUpgradeView view = UnityEngine.Object.FindFirstObjectByType<WeaponUpgradeView>();
            Check(view != null && manager.IsMenuOpen, "Native upgrade document opens");
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            int materialID = manager.CurrentUpgradeCost.ItemID;
            int required = manager.CurrentUpgradeCost.CurrentItemAmount;
            int owned = inventory.GetItemAmount(materialID);
            if (owned < required * 2)
            {
                Item material = WorldItemDatabase.Instance.GetRuntimeItemByID(materialID);
                material.SetCurrentItemAmount(required * 2 - owned);
                Check(inventory.AddItemToInventory(material), "Temporary upgrade materials added");
            }
            Submit(root.Q<Button>("right"));
            owned = inventory.GetItemAmount(materialID);
            Check(root.Q<Button>("upgrade").enabledSelf, "Sufficient materials enable upgrade");
            Submit(root.Q<Button>("upgrade"));
            yield return null;
            Check(manager.IsConfirmationOpen && !root.Q("upgrade-content").enabledSelf,
                "Confirmation disables underlying weapon commands");
            Check(root.panel.focusController.focusedElement == root.Q<Button>("cancel"),
                "Confirmation initially focuses Cancel");
            Check(inventory.GetItemAmount(materialID) == owned && weapon.UpgradeLevel == UpgradeLevel.Level0,
                "Opening confirmation consumes nothing");
            typeof(PlayerUICharacterMenuManager).GetMethod("OnCloseMenuPerformed",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui.PlayerUICharacterMenuManager,
                    new object[] { default(InputAction.CallbackContext) });
            Check(!manager.IsConfirmationOpen && manager.IsMenuOpen && inventory.GetItemAmount(materialID) == owned,
                "Global cancel closes only confirmation and preserves materials");
            Submit(root.Q<Button>("upgrade"));
            Submit(root.Q<Button>("confirm"));
            yield return null;
            Check((int)weapon.UpgradeLevel == 1 && inventory.GetItemAmount(materialID) == owned - required,
                "Native confirmation consumes one cost and advances one level");
            Check(!manager.UpgradeWeapon() && (int)weapon.UpgradeLevel == 1 &&
                inventory.GetItemAmount(materialID) == owned - required, "Duplicate commit is rejected");
            yield return new WaitForSecondsRealtime(0.25f);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/weapon-upgrade-toolkit.png"));
            yield return new WaitForEndOfFrame();
            weapon.SetUpgradeLevel(UpgradeLevel.Level10);
            Submit(root.Q<Button>("right"));
            Check(!root.Q<Button>("upgrade").enabledSelf && !manager.AttemptToUpgradeWeapon(),
                "Maximum-level weapon cannot open confirmation");
            manager.CloseMenu();
            manager.OpenWeaponUpgradeMenu();
            yield return null;
            Check(manager.IsMenuOpen && !manager.IsConfirmationOpen, "Reopening does not retain a pending commit");
            manager.CloseMenu();
            Check(!ui.IsMenuWindowOpen, "Closing returns gameplay input");
            File.AppendAllText(k_Report, "ALL PASSED\n");
        }

        private static void Submit(Button button)
        {
            button.Focus();
            using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        private static void Check(bool passed, string message)
        {
            File.AppendAllText(k_Report, $"{(passed ? "PASS" : "FAIL")}: {message}\n");
            if (!passed)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
