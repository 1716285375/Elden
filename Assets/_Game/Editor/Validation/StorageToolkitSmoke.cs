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
    /// <summary>Checks native storage transfers against temporary player inventory, without saving.</summary>
    public static class StorageToolkitSmoke
    {
        private const string k_Report = ".utmp/storage-toolkit-smoke.txt";

        [MenuItem("Tools/ZZ/UI Toolkit/Verify Storage Transfers")]
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
            weapon.SetUpgradeLevel((UpgradeLevel)2);
            Check(inventory.AddItemToInventory(weapon), "Temporary upgraded weapon added");
            PlayerUIStorageManager manager = ui.PlayerUIStorageManager;
            manager.OpenStorageMenu();
            yield return null;
            yield return null;
            StorageMenuView view = UnityEngine.Object.FindFirstObjectByType<StorageMenuView>();
            Check(view != null && manager.IsMenuOpen, "Native storage document opens");
            Check(view.transform.parent.GetComponentInChildren<Canvas>(true) == null, "Storage creates no Canvas");
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            ListView backpack = root.Q<ListView>("inventory-items");
            ListView storage = root.Q<ListView>("storage-items");
            backpack.SetSelection(backpack.itemsSource.IndexOf(weapon));
            Check(manager.CurrentItem == weapon && manager.IsSelectingFromPlayerInventory &&
                inventory.ItemsInInventory.Contains(weapon), "Selecting a row does not transfer it");
            Submit(root.Q<Button>("transfer"));
            yield return null;
            Check(!inventory.ItemsInInventory.Contains(weapon) && inventory.ItemsInStorage.Contains(weapon) &&
                (int)weapon.UpgradeLevel == 2, "Deposit keeps the exact upgraded weapon");
            Check(!backpack.itemsSource.Contains(weapon) && storage.itemsSource.Contains(weapon),
                "Both lists refresh after depositing");
            storage.SetSelection(storage.itemsSource.IndexOf(weapon));
            Check(!manager.IsSelectingFromPlayerInventory && backpack.selectedIndex == -1,
                "Selecting storage clears the backpack selection");
            Submit(root.Q<Button>("transfer"));
            yield return null;
            Check(inventory.ItemsInInventory.Contains(weapon) && !inventory.ItemsInStorage.Contains(weapon),
                "Withdraw restores the same weapon without duplication");
            Submit(root.Q<Button>("armor"));
            Check(backpack.itemsSource.Cast<Item>().All(item => item is ArmorItem) &&
                storage.itemsSource.Cast<Item>().All(item => item is ArmorItem), "Category filters both columns");
            Submit(root.Q<Button>("all"));
            yield return new WaitForSecondsRealtime(0.25f);
            File.WriteAllLines(".utmp/storage-empty-labels.txt", root.Query<Label>().ToList()
                .Where(label => label.text == "List is empty")
                .Select(label => $"{string.Join(", ", label.GetClasses())}: {label.resolvedStyle.display}"));
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/storage-toolkit.png"));
            yield return new WaitForEndOfFrame();
            typeof(PlayerUICharacterMenuManager).GetMethod("OnOpenCharacterMenuPerformed",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui.PlayerUICharacterMenuManager,
                    new object[] { default(InputAction.CallbackContext) });
            Check(!manager.IsMenuOpen && ui.PlayerUISiteOfGraceManager.IsMenuOpen,
                "Escape returns to grace instead of closing all menus");
            manager.OpenStorageMenu();
            yield return null;
            Check(manager.IsMenuOpen && UnityEngine.Object.FindFirstObjectByType<StorageMenuView>() != null,
                "Storage reopens with rebuilt controls");
            ui.CloseAllMenuWindows();
            Check(!ui.IsMenuWindowOpen, "Closing storage releases gameplay input");
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
