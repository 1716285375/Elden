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
    /// <summary>Exercises actual native shop controls in a disposable, unsaved gameplay session.</summary>
    public static class ShopToolkitSmoke
    {
        private const string k_Report = ".utmp/shop-toolkit-smoke.txt";

        [MenuItem("Tools/ZZ/UI Toolkit/Verify Shop Transactions")]
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
            PlayerManager player = ui.LocalPlayer;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/AI/Blacksmith NPC.prefab");
            GameObject merchant = UnityEngine.Object.Instantiate(prefab,
                player.transform.position + player.transform.forward, player.transform.rotation);
            try
            {
                AICharacterInventoryManager inventory = merchant.GetComponent<AICharacterInventoryManager>();
                Check(inventory.EnsureShopInventory(), "Merchant stock loads");
                PlayerUIShopManager manager = ui.PlayerUIShopManager;
                manager.OpenInteractionMenu(merchant.GetComponentInChildren<AICharacterSoundFXManager>());
                yield return null;
                ShopMenuView view = UnityEngine.Object.FindFirstObjectByType<ShopMenuView>();
                Check(view != null && manager.IsMenuOpen, "Native merchant context opens");
                Check(view.transform.parent.GetComponentInChildren<Canvas>(true) == null, "Shop creates no legacy Canvas");
                VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
                Submit(root.Q<Button>("buy"));
                yield return null;
                ListView list = root.Q<ListView>("shop-items");
                Item stock = inventory.ItemsInInventory.First(item => item != null && !item.IsInfinite);
                manager.SelectItem(stock);
                int stockBefore = stock.ShopStockAmount;
                int price = stock.ItemValue;
                player.PlayerStatsManager.AddRunes(-player.PlayerStatsManager.Runes);
                manager.SelectItem(stock);
                Check(!root.Q<Button>("trade").enabledSelf, "Unaffordable purchase is disabled");
                Check(!manager.BuyOrSellItem(stock) && stock.ShopStockAmount == stockBefore,
                    "Backend rejects an unaffordable purchase without changing stock");
                player.PlayerStatsManager.AddRunes(price + 100);
                manager.SelectItem(stock);
                int runesBefore = player.PlayerStatsManager.Runes;
                Check(stock.ShopStockAmount == stockBefore && root.Q<Button>("trade").enabledSelf,
                    "Selection only previews and does not buy");
                Submit(root.Q<Button>("trade"));
                yield return null;
                Check(view.IsConfirmationOpen && player.PlayerStatsManager.Runes == runesBefore,
                    "Opening transaction confirmation does not spend or sell");
                Submit(root.Q<Button>("confirm-trade"));
                yield return null;
                Check(player.PlayerStatsManager.Runes == runesBefore - price,
                    "Native purchase debits the displayed price once");
                Check(stock == null || stock.ShopStockAmount == stockBefore - 1,
                    "Native purchase removes one finite stock unit");
                Submit(root.Q<Button>("armor"));
                Check(((System.Collections.IList)typeof(ShopMenuView).GetField("m_items",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view)).Cast<Item>().All(item => item is ArmorItem), "Armor category excludes weapons");
                Submit(root.Q<Button>("weapons"));
                Check(((System.Collections.IList)typeof(ShopMenuView).GetField("m_items",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view)).Cast<Item>().All(item => item is WeaponItem), "Weapon category excludes armor");
                typeof(PlayerUICharacterMenuManager).GetMethod("OnCloseMenuPerformed",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui.PlayerUICharacterMenuManager,
                        new object[] { default(InputAction.CallbackContext) });
                Check(manager.IsMenuOpen && root.Q("shop-context").style.display == DisplayStyle.Flex,
                    "Global cancel returns exactly one level to merchant context");
                yield return null;
                Item material = WorldItemDatabase.Instance.GetRuntimeItemByID(16);
                SerializedObject serialized = new SerializedObject(material);
                serialized.FindProperty("m_itemValue").intValue = 20;
                serialized.FindProperty("m_itemID").intValue = 100001;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                material.SetCurrentItemAmount(3);
                Check(player.InventoryManager.AddItemToInventory(material), "Unsaved sale fixture added");
                Submit(root.Q<Button>("sell"));
                list.SetSelection(list.itemsSource.IndexOf(material));
                Check(root.Q<Button>("trade").text.Contains("3") &&
                    root.Q<Label>("item-price").text.Contains("15"), "Sale preview states whole-stack quantity and price");
                runesBefore = player.PlayerStatsManager.Runes;
                Submit(root.Q<Button>("trade"));
                yield return null;
                Check(view.IsConfirmationOpen && player.PlayerStatsManager.Runes == runesBefore,
                    "Opening transaction confirmation does not spend or sell");
                Submit(root.Q<Button>("confirm-trade"));
                yield return null;
                Check(player.PlayerStatsManager.Runes == runesBefore + 15 &&
                    player.InventoryManager.GetItemAmount(100001) == 0,
                    "Native stack sale removes three units and pays exactly fifteen runes");
                yield return new WaitForSecondsRealtime(0.25f);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/shop-toolkit.png"));
                yield return new WaitForEndOfFrame();
                merchant.transform.position += Vector3.right * 20f;
                yield return null;
                yield return null;
                Check(!manager.IsMenuOpen && !ui.IsMenuWindowOpen, "Leaving merchant range releases menu input");
                File.AppendAllText(k_Report, "ALL PASSED\n");
            }
            finally
            {
                ui.CloseAllMenuWindows();
                UnityEngine.Object.Destroy(merchant);
            }
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
