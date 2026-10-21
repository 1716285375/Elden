using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ.Editor
{
    /// <summary>Checks real menu documents without creating, overwriting, or deleting character saves.</summary>
    public static class NativeMenuToolkitSmoke
    {
        [MenuItem("Tools/ZZ/UI Toolkit/Open Title Settings")]
        public static void OpenTitleSettings()
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("Enter Play Mode in the title scene first.");
            }
            UnityEngine.Object.FindFirstObjectByType<TitleScreenManager>().OpenSettings();
        }

        [MenuItem("Tools/ZZ/UI Toolkit/Verify Native Settings")]
        public static void VerifySettings()
        {
            GameSettingsPanel settings = UnityEngine.Object.FindFirstObjectByType<GameSettingsPanel>();
            if (settings == null || !Application.isPlaying)
            {
                throw new InvalidOperationException("Open settings before running this check.");
            }
            if (settings.GetComponentInChildren<Canvas>() != null ||
                settings.GetComponentInChildren<UnityEngine.UI.Selectable>() != null)
            {
                throw new InvalidOperationException("Settings still owns legacy UI controls.");
            }
            UIDocument document = settings.GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            Button impact = root.Q<Button>("tab-impact");
            impact.Focus();
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = impact;
                impact.SendEvent(submit);
            }
            if (root.Q("page-impact").style.display.value != DisplayStyle.Flex)
            {
                throw new InvalidOperationException("Native submit did not switch the settings category.");
            }
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/native-settings-smoke.txt",
                "PASS: Native controls without Canvas\nPASS: Navigation submit switches category\n" +
                $"Viewport: {Screen.width}x{Screen.height}\n");
            root.schedule.Execute(() => ScreenCapture.CaptureScreenshot(
                Path.GetFullPath(".utmp/native-settings.png"))).StartingIn(300);
        }

        [MenuItem("Tools/ZZ/UI Toolkit/Preview Display Settings")]
        public static void PreviewDisplaySettings()
        {
            OpenTitleSettings();
            GameSettingsPanel settings = UnityEngine.Object.FindFirstObjectByType<GameSettingsPanel>();
            VisualElement root = settings.GetComponent<UIDocument>().rootVisualElement;
            Submit(root.Q<Button>("tab-display"));
            root.schedule.Execute(() =>
            {
                DropdownField display = root.Q<DropdownField>("display");
                display.Focus();
                using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = display;
                    display.SendEvent(submit);
                }
                root.schedule.Execute(() =>
                {
                    File.WriteAllLines(".utmp/ui-popup-tree.txt", root.panel.visualTree.Query<VisualElement>()
                        .ToList().Where(element => element.GetClasses().Any(name => name.Contains("dropdown")))
                        .Select(element => $"{element.GetType().Name} {element.name}: " +
                            $"{string.Join(", ", element.GetClasses())} parent={element.parent?.name}"));
                    File.WriteAllLines(".utmp/ui-dropdown-styles.txt", display.Query<VisualElement>().ToList()
                        .Select(element => $"{element.name} {string.Join(", ", element.GetClasses())} " +
                            $"background={element.resolvedStyle.backgroundColor} inline={element.style.backgroundColor} " +
                            $"image={element.resolvedStyle.backgroundImage}"));
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/native-settings-display.png"));
                }).StartingIn(250);
            }).StartingIn(250);
        }

        [MenuItem("Tools/ZZ/UI Toolkit/Capture Character Menu")]
        public static void CaptureCharacterMenu()
        {
            PlayerUIManager.Instance.PlayerUICharacterMenuManager.OpenCharacterMenu();
            CharacterMenuView view = UnityEngine.Object.FindFirstObjectByType<CharacterMenuView>();
            if (view == null)
            {
                throw new InvalidOperationException("Character menu document did not open.");
            }
            view.GetComponent<UIDocument>().rootVisualElement.schedule.Execute(() =>
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/native-character-menu.png")))
                .StartingIn(300);
        }

        [MenuItem("Tools/ZZ/UI Toolkit/Verify Character Menu Flow")]
        public static void VerifyCharacterMenuFlow()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || PlayerUIManager.Instance?.LocalPlayer == null)
            {
                throw new InvalidOperationException("Load a gameplay session before checking menu navigation.");
            }
            save.StartCoroutine(ObserveCharacterMenu());
        }

        [MenuItem("Tools/ZZ/UI Toolkit/Verify Reference Inventory Grid")]
        public static void VerifyReferenceInventoryGrid()
        {
            if (!Application.isPlaying || PlayerUIManager.Instance?.LocalPlayer == null)
            {
                throw new InvalidOperationException("Load a gameplay session first.");
            }
            PlayerUIManager.Instance.StartCoroutine(ObserveInventoryGrid());
        }

        private static IEnumerator ObserveInventoryGrid()
        {
            const string report = "Docs/UIParity/inventory-grid-smoke.txt";
            Directory.CreateDirectory("Docs/UIParity");
            File.WriteAllText(report, "RUNNING\n");
            PlayerUIManager ui = PlayerUIManager.Instance;
            int ownedCount = ui.LocalPlayer.InventoryManager.ItemsInInventory.Count;
            ui.PlayerUIInventoryManager.OpenInventory();
            yield return null;
            yield return null;
            InventoryMenuView view = UnityEngine.Object.FindFirstObjectByType<InventoryMenuView>();
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            var items = (IList)typeof(InventoryMenuView).GetField("m_items",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            ListView list = root.Q<ListView>("inventory-list");
            Verify(items.Count > 0 && list.itemsSource.Count == (items.Count + 4) / 5,
                "Inventory virtualizes five icons per row");
            Button first = root.Q<Button>("item-grid-cell-0");
            Verify(first != null && first.Q<Image>().sprite != null, "Cells bind actual inventory icons");
            first.Focus();
            using (NavigationMoveEvent move = NavigationMoveEvent.GetPooled(
                NavigationMoveEvent.Direction.Down, EventModifiers.None))
            {
                move.target = first;
                first.SendEvent(move);
            }
            yield return null;
            yield return null;
            int selected = Mathf.Min(5, items.Count - 1);
            Verify(root.Q<Button>("item-grid-cell-" + selected)
                .ClassListContains("item-grid__cell--selected"), "Down moves one complete grid row");
            Verify(root.Query<Button>(className: "item-grid__cell--selected").ToList().Count == 1,
                "Recycled cells preserve exactly one selected item");
            TextField search = root.Q<TextField>("item-search");
            search.value = "__no_inventory_match__";
            Verify(items.Count == 0 && list.itemsSource.Count == 0 && root.Q<Button>("equip") == null,
                "Empty search clears virtual rows; inventory has no equip command");
            search.value = string.Empty;
            yield return null;
            yield return null;
            Verify(items.Count > 0 && list.itemsSource.Count > 0, "Clearing search restores the inventory");
            Verify(items.Cast<Item>().All(item => item is not (WeaponItem or ArmorItem)),
                "Items entry excludes every weapon, shield and armor piece");
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/UIParity/inventory-ui.png"));
            yield return new WaitForEndOfFrame();
            Verify(ui.LocalPlayer.InventoryManager.ItemsInInventory.Count == ownedCount,
                "Browsing and navigation do not mutate owned items");
            File.AppendAllText(report, "ALL PASSED\n");

            void Verify(bool condition, string message)
            {
                File.AppendAllText(report, (condition ? "PASS: " : "FAIL: ") + message + "\n");
                if (!condition)
                {
                    throw new InvalidOperationException(message);
                }
            }
        }

        [MenuItem("Tools/ZZ/UI Toolkit/Verify Three Inventory Categories")]
        public static void VerifyThreeInventoryCategories()
        {
            if (!Application.isPlaying || PlayerUIManager.Instance?.LocalPlayer == null)
            {
                throw new InvalidOperationException("Load a gameplay session first.");
            }
            PlayerUIManager.Instance.StartCoroutine(ObserveThreeInventories());
        }

        private static IEnumerator ObserveThreeInventories()
        {
            const string report = "Docs/UIParity/three-inventories-smoke.txt";
            File.WriteAllText(report, "RUNNING\n");
            PlayerUIManager ui = PlayerUIManager.Instance;
            foreach (string entry in new[] { "weapons", "equipment", "inventory" })
            {
                ui.PlayerUICharacterMenuManager.OpenCharacterMenu();
                yield return null;
                var menu = UnityEngine.Object.FindFirstObjectByType<CharacterMenuView>();
                Submit(menu.GetComponent<UIDocument>().rootVisualElement.Q<Button>(entry));
                yield return null;
                yield return null;
                UIDocument document;
                if (entry == "inventory")
                {
                    var view = UnityEngine.Object.FindFirstObjectByType<InventoryMenuView>();
                    document = view.GetComponent<UIDocument>();
                    var items = (IList)typeof(InventoryMenuView).GetField("m_items",
                        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
                    Assert(items.Count > 0 && items.Cast<Item>().All(item =>
                        ItemInventoryFilter.Matches(item, InventoryCategory.Items)), "物品只显示道具、弹药和材料");
                    for (int categoryIndex = 0; categoryIndex < 8; categoryIndex++)
                    {
                        Submit(document.rootVisualElement.Q<Button>("item-category-" + categoryIndex));
                        yield return null;
                        Assert(items.Cast<Item>().All(item => InventoryMenuView.GetItemCategory(item) == categoryIndex),
                            "道具分类 " + categoryIndex + " 不混入其他分类");
                        Assert(document.rootVisualElement.Q("item-category-" + categoryIndex)
                            .ClassListContains("menu-tab--selected"), "当前分类显示选中游标");
                    }
                    Submit(document.rootVisualElement.Q<Button>("item-category-0"));
                    yield return null;
                    Assert(document.rootVisualElement.Q("category-weapons") == null &&
                        document.rootVisualElement.Q("category-armor") == null, "物品页面没有武器或防具混合分类");
                }
                else
                {
                    var view = UnityEngine.Object.FindFirstObjectByType<EquipmentMenuView>();
                    document = view.GetComponent<UIDocument>();
                    var root = document.rootVisualElement;
                    var slots = root.Query<Button>(className: "equipment-slot").ToList();
                    bool weapons = entry == "weapons";
                    Assert(slots.Count == (weapons ? 6 : 4), weapons ? "武器只显示六个手持栏位" : "装备只显示四个穿戴栏位");
                    Submit(slots[0]);
                    yield return null;
                    var items = (IList)typeof(EquipmentMenuView).GetField("m_items",
                        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
                    var category = weapons ? InventoryCategory.Weapons : InventoryCategory.Armor;
                    Assert(items.Count > 0 && items.Cast<Item>().All(item => ItemInventoryFilter.Matches(item, category)),
                        weapons ? "武器候选不包含衣服或道具" : "装备候选不包含武器或道具");
                    Assert(root.Q<Label>("menu-heading").text == (weapons ? "武器" : "装备"), "页面标题与入口对应");
                }
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/UIParity/" + entry + "-ui.png"));
                yield return new WaitForEndOfFrame();
                Submit(document.rootVisualElement.Q<Button>("back"));
                yield return null;
                if (entry != "inventory")
                {
                    Submit(document.rootVisualElement.Q<Button>("back"));
                    yield return null;
                }
                Assert(ui.PlayerUICharacterMenuManager.IsMenuOpen, "返回恢复游戏菜单");
            }
            File.AppendAllText(report, "ALL PASSED\n");

            void Assert(bool condition, string message)
            {
                File.AppendAllText(report, (condition ? "PASS: " : "FAIL: ") + message + "\n");
                if (!condition)
                {
                    throw new InvalidOperationException(message);
                }
            }
        }

        private static IEnumerator ObserveCharacterMenu()
        {
            File.WriteAllText(".utmp/native-menu-flow-smoke.txt", "RUNNING\n");
            PlayerUIManager ui = PlayerUIManager.Instance;
            ui.PlayerUICharacterMenuManager.OpenCharacterMenu();
            yield return null;
            CharacterMenuView view = UnityEngine.Object.FindFirstObjectByType<CharacterMenuView>();
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            Check(ui.IsMenuWindowOpen, "Native character menu owns gameplay input");
            Submit(root.Q<Button>("status"));
            yield return null;
            Check(view.IsStatusOpen && root.Q("status-attributes").childCount > 7,
                "Status opens real attribute rows within the character menu");
            Directory.CreateDirectory("Docs/UIParity");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/UIParity/status-ui.png"));
            yield return new WaitForEndOfFrame();
            Submit(root.Q<Button>("status-back"));
            yield return null;
            Check(!view.IsStatusOpen && ui.PlayerUICharacterMenuManager.IsMenuOpen,
                "Status return retains character-menu input ownership");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/UIParity/character-menu-ui.png"));
            yield return new WaitForEndOfFrame();
            Submit(root.Q<Button>("inventory"));
            yield return null;
            Check(ui.PlayerUIInventoryManager.IsMenuOpen && !ui.PlayerUIEquipmentManager.IsMenuOpen,
                "Inventory entry owns a separate window while equipment stays closed");
            ui.PlayerUIInventoryManager.ReturnToCharacterMenu();
            yield return null;
            Check(ui.PlayerUICharacterMenuManager.IsMenuOpen && !ui.PlayerUIInventoryManager.IsMenuOpen,
                "Inventory returns directly to the character menu");
            ui.PlayerUICharacterMenuManager.OpenCharacterMenu();
            yield return null;
            view = UnityEngine.Object.FindFirstObjectByType<CharacterMenuView>();
            root = view.GetComponent<UIDocument>().rootVisualElement;
            Submit(root.Q<Button>("settings"));
            yield return null;
            GameSettingsPanel settings = UnityEngine.Object.FindFirstObjectByType<GameSettingsPanel>();
            Check(settings != null && ui.IsMenuWindowOpen, "Native settings command opens modal");
            settings.Close();
            yield return null;
            Check(ui.IsMenuWindowOpen && ui.PlayerUICharacterMenuManager.IsMenuOpen,
                "Settings return retains character menu input ownership");
            Submit(root.Q<Button>("quit"));
            Check(view.IsConfirmationOpen && Application.isPlaying, "Exit command requires confirmation");
            view.CancelExit();
            Check(!view.IsConfirmationOpen && ui.IsMenuWindowOpen, "Cancel exit retains gameplay session");
            Submit(root.Q<Button>("equipment"));
            yield return null;
            Check(ui.PlayerUIEquipmentManager.IsMenuOpen && !ui.PlayerUICharacterMenuManager.IsMenuOpen,
                "Equipment command transfers input ownership");
            ui.PlayerUICharacterMenuManager.OpenCharacterMenu();
            yield return null;
            view = UnityEngine.Object.FindFirstObjectByType<CharacterMenuView>();
            root = view.GetComponent<UIDocument>().rootVisualElement;
            Submit(root.Q<Button>("save"));
            yield return null;
            Check(ui.PlayerUISaveGameManager.IsSaveGameMenuOpen, "Save command opens existing save service");
            ui.CloseAllMenuWindows();
            Check(!ui.IsMenuWindowOpen, "Closing restores gameplay input");
            File.AppendAllText(".utmp/native-menu-flow-smoke.txt", "ALL PASSED\n");
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
            File.AppendAllText(".utmp/native-menu-flow-smoke.txt", (condition ? "PASS: " : "FAIL: ") + message + "\n");
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
