using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Rolls one simple loot entry when its server-owned AI dies and spawns the shared pickup.
    /// </summary>
    [RequireComponent(typeof(AICharacterManager))]
    public class AICharacterInventoryManager : CharacterInventoryManager
    {
        [Header("Creature Drops")]
        [SerializeField] private Item[] m_droppableItems = new Item[0];
        [SerializeField, Range(0, 100)] private int m_dropItemChance = 10;

        [Header("Shop")]
        [SerializeField] private Shops m_characterShopID = Shops.NoShop;
        [SerializeField] private CharacterShop m_characterShop;

        private bool m_hasProcessedDrop;
        private bool m_shopHasBeenGenerated;

        /// <summary>Gets the equally weighted item templates authored for this enemy.</summary>
        public IReadOnlyList<Item> DroppableItems => m_droppableItems;

        /// <summary>Gets the exact percentage chance configured for one death.</summary>
        public int DropItemChance => m_dropItemChance;

        /// <summary>Gets the stable identity used to persist this merchant's stock.</summary>
        public Shops CharacterShopID => m_characterShopID;

        /// <summary>Gets whether this character exposes Buy and Sell services.</summary>
        public bool IsShop =>
            m_characterShopID != Shops.NoShop && m_characterShop != null;

        /// <summary>Gets whether runtime stock has been generated or loaded once.</summary>
        public bool ShopHasBeenGenerated => m_shopHasBeenGenerated;

        /// <summary>Generates default stock once or restores its saved replacement.</summary>
        public bool EnsureShopInventory()
        {
            if (!IsShop)
            {
                return false;
            }

            if (m_shopHasBeenGenerated)
            {
                return true;
            }

            CharacterSaveData saveData =
                WorldSaveGameManager.Instance?.CurrentCharacterData;
            SerializableShopInventory savedInventory =
                saveData?.GetShopInventory(m_characterShopID);
            List<Item> stock = savedInventory != null
                ? CreateRuntimeInventory(savedInventory)
                : m_characterShop.CreateRuntimeInventory(
                    WorldItemDatabase.Instance);

            ClearRuntimeInventory();
            foreach (Item item in stock)
            {
                AddItemToInventory(item);
            }

            m_shopHasBeenGenerated = true;
            if (savedInventory == null)
            {
                SaveShopInventory(false);
            }

            return true;
        }

        /// <summary>Purchases one item copy and commits finite stock after payment.</summary>
        public bool TryPurchaseItem(Item shopItem, PlayerManager player)
        {
            if (!EnsureShopInventory() ||
                player == null ||
                player.IsSpawned && !player.IsOwner ||
                shopItem == null ||
                !InventoryItems.Contains(shopItem) ||
                !shopItem.IsInfinite && shopItem.ShopStockAmount <= 0 ||
                player.PlayerStatsManager == null ||
                player.PlayerStatsManager.Runes < shopItem.ItemValue)
            {
                return false;
            }

            Item purchasedItem = WorldItemDatabase.Instance
                ?.GetRuntimeItemByID(shopItem.ItemID);
            if (purchasedItem == null)
            {
                return false;
            }

            purchasedItem.SetCurrentItemAmount(1);
            purchasedItem.SetShopStockAmount(1);
            purchasedItem.SetInfinite(false);
            if (player.InventoryManager?.AddItemToInventory(purchasedItem) != true)
            {
                DestroyRuntimeItem(purchasedItem);
                return false;
            }

            player.PlayerStatsManager.AddRunes(-shopItem.ItemValue);
            if (!shopItem.IsInfinite)
            {
                shopItem.SetShopStockAmount(shopItem.ShopStockAmount - 1);
                if (shopItem.ShopStockAmount <= 0)
                {
                    InventoryItems.Remove(shopItem);
                    DestroyRuntimeItem(shopItem);
                }
            }

            SaveShopInventory(true);
            return true;
        }

        /// <summary>Sells one complete player stack for one quarter of its buy value.</summary>
        public bool TrySellItem(Item playerItem, PlayerManager player)
        {
            PlayerInventoryManager inventory = player?.InventoryManager;
            if (!EnsureShopInventory() ||
                player == null ||
                player.IsSpawned && !player.IsOwner ||
                playerItem == null ||
                inventory == null ||
                !inventory.ItemsInInventory.Contains(playerItem))
            {
                return false;
            }

            int sellValue = CalculateSellValue(playerItem);
            bool shouldReleaseItem = !playerItem.IsStackable;
            if (!inventory.RemoveItemFromInventory(playerItem))
            {
                return false;
            }

            if (shouldReleaseItem)
            {
                DestroyRuntimeItem(playerItem);
            }

            player.PlayerStatsManager?.AddRunes(sellValue);
            SaveShopInventory(true);
            return true;
        }

        /// <summary>Returns the tutorial sell price without merchant cash limits.</summary>
        public static int CalculateSellValue(Item item)
        {
            return item == null
                ? 0
                : Mathf.Max(0, Mathf.RoundToInt(item.ItemValue / 4f));
        }

        /// <summary>Writes current stock after a successful mutation.</summary>
        public bool SaveShopInventory(bool saveImmediately)
        {
            CharacterSaveData saveData =
                WorldSaveGameManager.Instance?.CurrentCharacterData;
            List<int> itemIDs = new();
            List<int> itemAmounts = new();
            List<bool> infiniteItems = new();
            foreach (Item item in ItemsInInventory)
            {
                if (item == null || item.ItemID < 0 ||
                    !item.IsInfinite && item.ShopStockAmount <= 0)
                {
                    continue;
                }

                itemIDs.Add(item.ItemID);
                itemAmounts.Add(Mathf.Max(1, item.ShopStockAmount));
                infiniteItems.Add(item.IsInfinite);
            }

            if (!m_shopHasBeenGenerated ||
                saveData == null ||
                !saveData.SetShopInventory(new SerializableShopInventory(
                    m_characterShopID,
                    itemIDs,
                    itemAmounts,
                    infiniteItems)))
            {
                return false;
            }

            WorldSaveGameManager saveManager = WorldSaveGameManager.Instance;
            if (saveImmediately && saveManager?.CanSaveGame == true)
            {
                saveManager.SaveGame();
            }

            return true;
        }

        private static List<Item> CreateRuntimeInventory(
            SerializableShopInventory savedInventory)
        {
            List<Item> runtimeInventory = new();
            WorldItemDatabase database = WorldItemDatabase.Instance;
            if (savedInventory == null || database == null)
            {
                return runtimeInventory;
            }

            for (int itemIndex = 0;
                itemIndex < savedInventory.ItemCount;
                itemIndex++)
            {
                Item runtimeItem = database.GetRuntimeItemByID(
                    savedInventory.GetItemID(itemIndex));
                if (runtimeItem == null)
                {
                    continue;
                }

                runtimeItem.name =
                    $"{runtimeItem.name} (Loaded Shop Stock)";
                runtimeItem.SetCurrentItemAmount(
                    savedInventory.GetItemAmount(itemIndex));
                runtimeItem.SetShopStockAmount(
                    savedInventory.GetItemAmount(itemIndex));
                runtimeItem.SetInfinite(
                    savedInventory.GetIsInfinite(itemIndex));
                runtimeInventory.Add(runtimeItem);
            }

            return runtimeInventory;
        }

        /// <summary>
        /// Performs one server-only percentage roll and spawns at most one network pickup.
        /// </summary>
        public void DropItem()
        {
            if (m_hasProcessedDrop ||
                Character is not AICharacterManager aiCharacter ||
                !aiCharacter.IsSpawned ||
                !aiCharacter.IsServer ||
                !aiCharacter.IsOwner)
            {
                return;
            }

            m_hasProcessedDrop = true;
            if (m_droppableItems == null ||
                m_droppableItems.Length == 0 ||
                !DidDropRollSucceed(
                    Random.Range(0, 100),
                    m_dropItemChance))
            {
                return;
            }

            Item droppedItem = SelectRandomValidItem();
            GameObject pickupPrefab =
                WorldItemDatabase.Instance?.CreatureDropPickupPrefab;
            if (droppedItem == null || pickupPrefab == null)
            {
                Debug.LogWarning(
                    $"{name} cannot drop loot because its item list or pickup " +
                    "prefab is incomplete.",
                    this);
                return;
            }

            Vector3 dropPosition = aiCharacter.LockOnTransform.position;
            GameObject pickupObject = Instantiate(
                pickupPrefab,
                dropPosition,
                Quaternion.identity);
            NetworkObject networkObject =
                pickupObject.GetComponent<NetworkObject>();
            PickupItemInteractable pickup =
                pickupObject.GetComponent<PickupItemInteractable>();
            if (networkObject == null || pickup == null)
            {
                Debug.LogError(
                    "The creature drop prefab requires NetworkObject and PickupItemInteractable.",
                    pickupObject);
                Destroy(pickupObject);
                return;
            }

            networkObject.Spawn(true);
            if (!pickup.InitializeCharacterDrop(
                    droppedItem.ItemID,
                    dropPosition,
                    aiCharacter.NetworkObjectId))
            {
                networkObject.Despawn(true);
            }
        }

        /// <summary>Allows a revived server-owned AI to roll again on its next death.</summary>
        public void ResetDropState()
        {
            m_hasProcessedDrop = false;
        }

        internal static bool DidDropRollSucceed(int roll, int chance)
        {
            return roll >= 0 && roll < Mathf.Clamp(chance, 0, 100);
        }

        private Item SelectRandomValidItem()
        {
            int validItemCount = 0;
            foreach (Item item in m_droppableItems)
            {
                if (item != null)
                {
                    validItemCount++;
                }
            }

            if (validItemCount == 0)
            {
                return null;
            }

            int selectedValidIndex = Random.Range(0, validItemCount);
            foreach (Item item in m_droppableItems)
            {
                if (item == null)
                {
                    continue;
                }

                if (selectedValidIndex == 0)
                {
                    return item;
                }

                selectedValidIndex--;
            }

            return null;
        }
    }
}
