using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Checks release integration using an unsaved host session and real gameplay components.</summary>
    public static class ReleaseIntegrationSmoke
    {
        private const string k_Report = ".utmp/release-integration-smoke.txt";

        /// <summary>Opens the title settings so the rendered layout can be inspected.</summary>
        public static void OpenTitleSettings()
        {
            UnityEngine.Object.FindFirstObjectByType<TitleScreenManager>().OpenSettings();
        }

        /// <summary>Exercises menu ownership, transactions, and replacement AI spawning.</summary>
        public static void Verify()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved gameplay smoke session.");
            }
            File.WriteAllText(k_Report, "RUNNING\n");
            save.StartCoroutine(Observe());
        }

        private static IEnumerator Observe()
        {
            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .First(candidate => candidate.IsOwner);
            PlayerUIManager ui = PlayerUIManager.Instance;
            ui.PlayerUICharacterMenuManager.OpenCharacterMenu();
            yield return null;
            Check(ui.IsMenuWindowOpen, "Character menu owns gameplay input");
            Button settings = ui.GetComponentsInChildren<Button>(true).First(button => button.name == "Settings");
            settings.onClick.Invoke();
            yield return null;
            GameSettingsPanel panel = ui.GetComponentInChildren<GameSettingsPanel>();
            Check(panel != null && panel.gameObject.activeInHierarchy, "Gameplay settings opens");
            panel.Close();
            Check(ui.IsMenuWindowOpen, "Returning from settings retains character-menu input ownership");
            ui.CloseAllMenuWindows();
            Check(!ui.IsMenuWindowOpen, "Closing menu restores gameplay ownership");

            GameObject merchantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/AI/Blacksmith NPC.prefab");
            GameObject merchant = UnityEngine.Object.Instantiate(merchantPrefab,
                player.transform.position + player.transform.forward, player.transform.rotation);
            try
            {
                AICharacterInventoryManager inventory = merchant.GetComponent<AICharacterInventoryManager>();
                Check(inventory.EnsureShopInventory(), "Merchant restores runtime inventory");
                Item stock = inventory.ItemsInInventory.First(item => item != null && !item.IsInfinite);
                int initialStock = stock.ShopStockAmount;
                int price = stock.ItemValue;
                int initialRunes = player.PlayerStatsManager.Runes;
                player.PlayerStatsManager.AddRunes(Mathf.Max(0, price - initialRunes));
                int runesBeforePurchase = player.PlayerStatsManager.Runes;
                Check(inventory.TryPurchaseItem(stock, player), "Merchant purchase succeeds in range");
                Check(player.PlayerStatsManager.Runes == runesBeforePurchase - price,
                    "Purchase pays exactly the displayed price");
                Check(stock.ShopStockAmount == initialStock - 1, "Purchase decrements finite stock once");
                merchant.transform.position += Vector3.right * 20f;
                Check(!inventory.CanTradeWith(player) && !inventory.TryPurchaseItem(stock, player),
                    "Distant merchant rejects transactions");
                merchant.transform.position = player.transform.position + player.transform.forward;

                Item material = WorldItemDatabase.Instance.GetRuntimeItemByID(16);
                SerializedObject serialized = new(material);
                serialized.FindProperty("m_itemValue").intValue = 20;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                material.SetCurrentItemAmount(3);
                // A distinct test identity prevents merging with the starting material stack.
                serialized.Update();
                serialized.FindProperty("m_itemID").intValue = 100000;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Check(player.InventoryManager.AddItemToInventory(material), "Inventory accepts a material stack");
                int beforeSale = player.PlayerStatsManager.Runes;
                Check(inventory.TrySellItem(material, player), "Stack sale succeeds");
                Check(player.PlayerStatsManager.Runes == beforeSale + 15, "Three units pay three sell prices");
                Check(player.InventoryManager.GetItemAmount(100000) == 0, "Sold stack leaves inventory");
                Item key = WorldItemDatabase.Instance.GetRuntimeItemByID(19);
                if (key != null)
                {
                    Check(!AICharacterInventoryManager.CanSellItem(key), "Progression item cannot be sold");
                    CharacterInventoryManager.DestroyRuntimeItem(key);
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(merchant);
            }

            AICharacterSpawner spawner = UnityEngine.Object.FindObjectsByType<AICharacterSpawner>(FindObjectsSortMode.None)
                .First(candidate => !candidate.IsBoss && candidate.InstantiatedCharacter != null &&
                    candidate.InstantiatedCharacter.IsSpawned);
            ulong oldIdentity = spawner.InstantiatedCharacter.NetworkObjectId;
            spawner.InstantiatedCharacter.NetworkObject.Despawn(true);
            yield return null;
            Check(spawner.ResetCharacter(), "Rest recreates a missing ordinary enemy");
            Check(spawner.InstantiatedCharacter != null && spawner.InstantiatedCharacter.IsSpawned &&
                spawner.InstantiatedCharacter.NetworkObjectId != oldIdentity,
                "Replacement enemy has a fresh network identity");
            File.AppendAllText(k_Report, "PASSED\n");
        }

        private static void Check(bool condition, string description)
        {
            File.AppendAllText(k_Report, (condition ? "PASS " : "FAIL ") + description + "\n");
            if (!condition)
            {
                throw new InvalidOperationException(description);
            }
        }
    }
}
