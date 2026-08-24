using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Adds an authored item to the local player's runtime inventory and persists fixed loot.
    /// </summary>
    public class PickupItemInteractable : Interactable
    {
        [SerializeField] private ItemPickupType m_pickupType;
        [SerializeField, Min(-1)] private int m_itemID = -1;
        [SerializeField] private bool m_hasBeenLooted;
        [SerializeField] private Item m_item;

        public ItemPickupType PickupType => m_pickupType;
        public int ItemID => m_itemID;
        public bool HasBeenLooted => m_hasBeenLooted;
        public Item Item => m_item;

        private void Start()
        {
            if (m_pickupType == ItemPickupType.WorldSpawn)
            {
                CheckIfWorldItemWasAlreadyLooted();
            }
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (player == null ||
                m_item == null ||
                (m_pickupType == ItemPickupType.WorldSpawn &&
                    NetworkManager.Singleton?.IsHost != true))
            {
                return;
            }

            base.Interact(player);
            player.CharacterSoundFXManager?.PlayPickupItemSound();
            if (!player.InventoryManager.AddItemToInventory(m_item))
            {
                return;
            }

            PlayerUIManager.Instance?.PlayerUIPopUpManager?.SendItemPopup(m_item, 1);
            if (m_pickupType == ItemPickupType.WorldSpawn)
            {
                SaveWorldItemAsLooted();
            }

            RemovePickupFromWorld();
        }

        private void CheckIfWorldItemWasAlreadyLooted()
        {
            if (NetworkManager.Singleton?.IsHost != true)
            {
                gameObject.SetActive(false);
                return;
            }

            if (m_itemID < 0)
            {
                Debug.LogError("World Spawn pickups require a non-negative unique item ID.", this);
                gameObject.SetActive(false);
                return;
            }

            CharacterSaveData saveData = WorldSaveGameManager.Instance?.CurrentCharacterData;
            if (saveData == null)
            {
                Debug.LogWarning(
                    $"World pickup {m_itemID} could not access current character data.",
                    this);
                return;
            }

            if (!saveData.TryGetWorldItemLooted(m_itemID, out m_hasBeenLooted))
            {
                saveData.SetWorldItemLooted(m_itemID, false);
                m_hasBeenLooted = false;
            }

            if (m_hasBeenLooted)
            {
                gameObject.SetActive(false);
            }
        }

        private void SaveWorldItemAsLooted()
        {
            if (m_itemID < 0)
            {
                Debug.LogError("World Spawn pickups require a non-negative unique item ID.", this);
                return;
            }

            CharacterSaveData saveData = WorldSaveGameManager.Instance?.CurrentCharacterData;
            if (saveData == null)
            {
                Debug.LogWarning(
                    $"World pickup {m_itemID} could not update current character data.",
                    this);
                return;
            }

            saveData.SetWorldItemLooted(m_itemID, true);
            m_hasBeenLooted = true;
        }

        private void RemovePickupFromWorld()
        {
            NetworkObject networkObject = NetworkObject;
            if (networkObject != null && networkObject.IsSpawned)
            {
                if (IsServer)
                {
                    networkObject.Despawn(true);
                }
                else
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            Destroy(gameObject);
        }
    }
}
