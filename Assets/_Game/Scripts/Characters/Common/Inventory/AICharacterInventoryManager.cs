using System.Collections.Generic;
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

        private bool m_hasProcessedDrop;

        /// <summary>Gets the equally weighted item templates authored for this enemy.</summary>
        public IReadOnlyList<Item> DroppableItems => m_droppableItems;

        /// <summary>Gets the exact percentage chance configured for one death.</summary>
        public int DropItemChance => m_dropItemChance;

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
