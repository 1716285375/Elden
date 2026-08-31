using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Provides shared character ownership context to inventory implementations.
    /// </summary>
    public class CharacterInventoryManager : MonoBehaviour
    {
        [Header("Runtime Inventory")]
        [SerializeField] private List<Item> m_itemsInInventory = new();

        protected CharacterManager Character { get; private set; }

        /// <summary>Gets the runtime item instances owned by this character.</summary>
        public IReadOnlyList<Item> ItemsInInventory => InventoryItems;

        protected List<Item> InventoryItems =>
            m_itemsInInventory ??= new List<Item>();

        protected virtual void Awake()
        {
            Character = GetComponent<CharacterManager>();
        }

        protected virtual void OnDestroy()
        {
            ClearRuntimeInventory();
        }

        /// <summary>Adds one runtime item, merging a compatible stack when possible.</summary>
        public bool AddItemToInventory(Item item)
        {
            return AddItemToCollection(InventoryItems, item, "Inventory");
        }

        /// <summary>Removes one matching runtime item or the requested stack amount.</summary>
        public bool RemoveItemFromInventory(Item item)
        {
            return RemoveItemFromCollection(InventoryItems, item);
        }

        /// <summary>Returns the total owned amount for one stable catalog item.</summary>
        public int GetItemAmount(int itemID)
        {
            if (itemID < 0)
            {
                return 0;
            }

            int totalAmount = 0;
            foreach (Item item in InventoryItems)
            {
                if (item?.ItemID == itemID)
                {
                    totalAmount += item.IsStackable
                        ? item.CurrentItemAmount
                        : 1;
                }
            }

            return totalAmount;
        }

        /// <summary>Destroys every owned runtime item and empties the inventory.</summary>
        public void ClearRuntimeInventory()
        {
            ClearRuntimeCollection(InventoryItems);
        }

        /// <summary>Releases one transient item created from the item database.</summary>
        public static void DestroyRuntimeItem(Item item)
        {
            if (item != null && (item.hideFlags & HideFlags.DontSave) != 0)
            {
                Destroy(item);
            }
        }

        /// <summary>Adds one item to a runtime container using shared stack rules.</summary>
        protected bool AddItemToCollection(
            List<Item> items,
            Item item,
            string collectionName)
        {
            if (items == null || item == null)
            {
                return false;
            }

            items.RemoveAll(candidate => candidate == null);
            Item existingStack = FindCompatibleStack(items, item);
            if (existingStack != null)
            {
                if (existingStack.CurrentItemAmount + item.CurrentItemAmount >
                    existingStack.MaxItemAmount)
                {
                    Debug.LogWarning(
                        $"{collectionName} stack {item.ItemName} reached its maximum; " +
                        $"{item.CurrentItemAmount} item(s) could not be added.",
                        this);
                    return false;
                }

                existingStack.AddItemAmount(item.CurrentItemAmount);
                DestroyRuntimeItem(item);
                return true;
            }

            items.Add(item);
            return true;
        }

        /// <summary>Removes one item or requested stack amount from a runtime container.</summary>
        protected static bool RemoveItemFromCollection(
            List<Item> items,
            Item item)
        {
            if (items == null || item == null)
            {
                return false;
            }

            items.RemoveAll(candidate => candidate == null);
            if (!item.IsStackable)
            {
                return items.Remove(item);
            }

            Item existingStack = FindCompatibleStack(items, item);
            if (existingStack == null ||
                !existingStack.TryRemoveItemAmount(item.CurrentItemAmount))
            {
                return false;
            }

            if (existingStack.CurrentItemAmount <= 0)
            {
                items.Remove(existingStack);
                DestroyRuntimeItem(existingStack);
            }

            return true;
        }

        /// <summary>Moves one complete runtime entry between containers without cloning it.</summary>
        protected bool TransferItemBetweenCollections(
            List<Item> sourceItems,
            List<Item> destinationItems,
            Item item,
            string destinationName)
        {
            if (sourceItems == null ||
                destinationItems == null ||
                item == null)
            {
                return false;
            }

            sourceItems.RemoveAll(candidate => candidate == null);
            destinationItems.RemoveAll(candidate => candidate == null);
            if (!sourceItems.Contains(item) ||
                !CanAddItemToCollection(destinationItems, item))
            {
                return false;
            }

            sourceItems.Remove(item);
            if (AddItemToCollection(destinationItems, item, destinationName))
            {
                return true;
            }

            sourceItems.Add(item);
            return false;
        }

        /// <summary>Destroys every transient item owned by one runtime container.</summary>
        protected static void ClearRuntimeCollection(List<Item> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (Item item in items)
            {
                DestroyRuntimeItem(item);
            }

            items.Clear();
        }

        private static bool CanAddItemToCollection(
            List<Item> items,
            Item item)
        {
            Item existingStack = FindCompatibleStack(items, item);
            return existingStack == null ||
                existingStack.CurrentItemAmount + item.CurrentItemAmount <=
                    existingStack.MaxItemAmount;
        }

        private static Item FindCompatibleStack(
            List<Item> items,
            Item item)
        {
            if (item?.IsStackable != true)
            {
                return null;
            }

            return items.Find(candidate =>
                candidate != null &&
                candidate.ItemID == item.ItemID &&
                candidate.GetType() == item.GetType() &&
                candidate.IsStackable);
        }
    }
}
