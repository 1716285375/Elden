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
            if (item == null)
            {
                return false;
            }

            InventoryItems.RemoveAll(candidate => candidate == null);
            if (item.IsStackable)
            {
                Item existingStack = InventoryItems.Find(candidate =>
                    candidate.ItemID == item.ItemID &&
                    candidate.GetType() == item.GetType() &&
                    candidate.IsStackable);
                if (existingStack != null)
                {
                    if (existingStack.CurrentItemAmount + item.CurrentItemAmount >
                        existingStack.MaxItemAmount)
                    {
                        Debug.LogWarning(
                            $"Inventory stack {item.ItemName} reached its maximum; " +
                            $"{item.CurrentItemAmount} item(s) could not be added.",
                            this);
                        return false;
                    }

                    existingStack.AddItemAmount(item.CurrentItemAmount);
                    DestroyRuntimeItem(item);
                    return true;
                }
            }

            InventoryItems.Add(item);
            return true;
        }

        /// <summary>Removes one matching runtime item or the requested stack amount.</summary>
        public bool RemoveItemFromInventory(Item item)
        {
            InventoryItems.RemoveAll(candidate => candidate == null);
            if (item == null)
            {
                return false;
            }

            if (!item.IsStackable)
            {
                return InventoryItems.Remove(item);
            }

            Item inventoryStack = InventoryItems.Find(candidate =>
                candidate.ItemID == item.ItemID &&
                candidate.GetType() == item.GetType() &&
                candidate.IsStackable);
            if (inventoryStack == null ||
                !inventoryStack.TryRemoveItemAmount(item.CurrentItemAmount))
            {
                return false;
            }

            if (inventoryStack.CurrentItemAmount <= 0)
            {
                InventoryItems.Remove(inventoryStack);
                DestroyRuntimeItem(inventoryStack);
            }

            return true;
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
            foreach (Item item in InventoryItems)
            {
                DestroyRuntimeItem(item);
            }

            InventoryItems.Clear();
        }

        /// <summary>Releases one transient item created from the item database.</summary>
        public static void DestroyRuntimeItem(Item item)
        {
            if (item != null && (item.hideFlags & HideFlags.DontSave) != 0)
            {
                Destroy(item);
            }
        }
    }
}
