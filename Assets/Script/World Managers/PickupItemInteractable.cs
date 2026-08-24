using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Persists fixed world loot and resolves server-spawned creature drops for every peer.
    /// </summary>
    public class PickupItemInteractable : Interactable
    {
        private const float k_MaximumPickupDistance = 4f;
        private const ulong k_NoDroppingCreature = ulong.MaxValue;

        [Header("Pickup Data")]
        [SerializeField] private ItemPickupType m_pickupType;
        [SerializeField, Min(-1)] private int m_itemID = -1;
        [SerializeField] private bool m_hasBeenLooted;
        [SerializeField] private Item m_item;

        [Header("Creature Drop")]
        [SerializeField] private bool m_trackDroppingCreaturePosition = true;
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField] private AudioClip m_itemDropSoundEffect;

        /// <summary>Replicates the stable item catalog identifier selected by the server.</summary>
        public NetworkVariable<int> NetworkItemID = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        /// <summary>
        /// Replicates the fallback world position used when the source corpse is absent.
        /// </summary>
        public NetworkVariable<Vector3> NetworkPosition =
            new NetworkVariable<Vector3>(
                Vector3.zero,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        /// <summary>
        /// Replicates the NetworkObject identifier of the creature that dropped this item.
        /// </summary>
        public NetworkVariable<ulong> DroppingCreatureID =
            new NetworkVariable<ulong>(
                k_NoDroppingCreature,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private Coroutine m_trackCreatureRoutine;
        private bool m_hasBeenClaimed;
        private bool m_localPickupRequested;

        public ItemPickupType PickupType => m_pickupType;
        public int ItemID => m_itemID;
        /// <summary>Gets the dynamic stable item identifier for a creature drop.</summary>
        public int DroppedItemID => NetworkItemID.Value;
        public bool HasBeenLooted => m_hasBeenLooted;
        /// <summary>Gets whether this pickup follows its source creature locally.</summary>
        public bool TracksDroppingCreature => m_trackDroppingCreaturePosition;
        public Item Item => m_item;

        protected override void Awake()
        {
            base.Awake();
            m_audioSource ??= GetComponent<AudioSource>();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            m_audioSource ??= GetComponent<AudioSource>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_hasBeenClaimed = false;
            m_localPickupRequested = false;
            NetworkItemID.OnValueChanged += OnItemIDChanged;
            NetworkPosition.OnValueChanged += OnNetworkPositionChanged;
            DroppingCreatureID.OnValueChanged += OnDroppingCreatureIDChanged;

            if (m_pickupType != ItemPickupType.CharacterDrop)
            {
                return;
            }

            if (NetworkItemID.Value >= 0)
            {
                OnNetworkPositionChanged(
                    NetworkPosition.Value,
                    NetworkPosition.Value);
                OnItemIDChanged(NetworkItemID.Value, NetworkItemID.Value);
                OnDroppingCreatureIDChanged(
                    DroppingCreatureID.Value,
                    DroppingCreatureID.Value);
            }
            else
            {
                m_item = null;
                SetInteractionColliderEnabled(false);
            }
            if (m_audioSource != null && m_itemDropSoundEffect != null)
            {
                m_audioSource.PlayOneShot(m_itemDropSoundEffect);
            }
        }

        public override void OnNetworkDespawn()
        {
            NetworkItemID.OnValueChanged -= OnItemIDChanged;
            NetworkPosition.OnValueChanged -= OnNetworkPositionChanged;
            DroppingCreatureID.OnValueChanged -= OnDroppingCreatureIDChanged;
            StopTrackingDroppingCreature();
            base.OnNetworkDespawn();
        }

        private void Start()
        {
            if (m_pickupType == ItemPickupType.WorldSpawn)
            {
                CheckIfWorldItemWasAlreadyLooted();
            }
        }

        /// <inheritdoc />
        public override bool CanInteract(PlayerManager player)
        {
            return base.CanInteract(player) &&
                player != null &&
                !player.IsDead &&
                !player.IsPerformingAction &&
                !m_localPickupRequested &&
                m_item != null &&
                (m_pickupType != ItemPickupType.WorldSpawn ||
                    NetworkManager.Singleton?.IsHost == true);
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            base.Interact(player);
            if (m_pickupType == ItemPickupType.CharacterDrop)
            {
                m_localPickupRequested = true;
                DestroyThisNetworkObjectServerRpc();
                return;
            }

            GrantItemToPlayer(player, m_item);
            SaveWorldItemAsLooted();
            RemovePickupFromWorld();
        }

        /// <summary>
        /// Applies the server-selected creature drop state after network spawning.
        /// </summary>
        public bool InitializeCharacterDrop(
            int itemID,
            Vector3 networkPosition,
            ulong droppingCreatureID)
        {
            if (m_pickupType != ItemPickupType.CharacterDrop ||
                !IsSpawned ||
                !IsServer ||
                WorldItemDatabase.Instance?.GetItemByID(itemID) == null)
            {
                return false;
            }

            NetworkPosition.Value = networkPosition;
            NetworkItemID.Value = itemID;
            DroppingCreatureID.Value = droppingCreatureID;
            return true;
        }

        /// <summary>
        /// Atomically validates one non-owner pickup request, grants it to that client,
        /// and despawns it.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void DestroyThisNetworkObjectServerRpc(
            ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer ||
                m_pickupType != ItemPickupType.CharacterDrop ||
                m_hasBeenClaimed ||
                !TryResolveRequestingPlayer(
                    serverRpcParams.Receive.SenderClientId,
                    out PlayerManager player) ||
                player.IsDead ||
                player.IsPerformingAction ||
                Vector3.SqrMagnitude(
                    player.transform.position - transform.position) >
                    k_MaximumPickupDistance * k_MaximumPickupDistance)
            {
                return;
            }

            Item droppedItem = WorldItemDatabase.Instance?.GetItemByID(
                NetworkItemID.Value);
            if (droppedItem == null)
            {
                return;
            }

            m_hasBeenClaimed = true;
            SetInteractionColliderEnabled(false);
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[]
                    {
                        serverRpcParams.Receive.SenderClientId
                    }
                }
            };
            GrantCharacterDropClientRpc(
                droppedItem.ItemID,
                clientRpcParams);
            StartCoroutine(DespawnCharacterDropAtEndOfFrame());
        }

        [ClientRpc]
        private void GrantCharacterDropClientRpc(
            int itemID,
            ClientRpcParams clientRpcParams = default)
        {
            NetworkObject localPlayerObject =
                NetworkManager.Singleton?.LocalClient?.PlayerObject;
            PlayerManager player =
                localPlayerObject?.GetComponent<PlayerManager>();
            Item item = WorldItemDatabase.Instance?.GetItemByID(itemID);
            if (player == null || !player.IsOwner || item == null)
            {
                return;
            }

            GrantItemToPlayer(player, item);
        }

        private static void GrantItemToPlayer(PlayerManager player, Item item)
        {
            if (player?.InventoryManager?.AddItemToInventory(item) != true)
            {
                return;
            }

            player.CharacterSoundFXManager?.PlayPickupItemSound();
            player.CharacterAnimatorManager?.PlayTargetActionAnimation(
                CharacterActionAnimation.PickupItem,
                true);
            player.CharacterNetworkManager
                ?.NotifyServerOfActionAnimationServerRpc(
                    CharacterActionAnimation.PickupItem,
                    true,
                    false,
                    false,
                    false);
            PlayerUIManager.Instance?.PlayerUIPopUpManager
                ?.SendItemPopup(item, 1);
        }

        private bool TryResolveRequestingPlayer(
            ulong clientID,
            out PlayerManager player)
        {
            player = null;
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.ConnectedClients.TryGetValue(
                    clientID,
                    out NetworkClient networkClient))
            {
                return false;
            }

            player = networkClient.PlayerObject?.GetComponent<PlayerManager>();
            return player != null && player.OwnerClientId == clientID;
        }

        private IEnumerator DespawnCharacterDropAtEndOfFrame()
        {
            yield return null;
            if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        private void OnItemIDChanged(int previousItemID, int currentItemID)
        {
            if (m_pickupType != ItemPickupType.CharacterDrop)
            {
                return;
            }

            m_item = WorldItemDatabase.Instance?.GetItemByID(currentItemID);
            SetInteractableText(
                m_item != null
                    ? $"Pick Up {m_item.ItemName}"
                    : "Pick Up Item");
            SetInteractionColliderEnabled(m_item != null && !m_hasBeenClaimed);
        }

        private void OnNetworkPositionChanged(
            Vector3 previousPosition,
            Vector3 currentPosition)
        {
            if (m_pickupType == ItemPickupType.CharacterDrop)
            {
                transform.position = currentPosition;
            }
        }

        private void OnDroppingCreatureIDChanged(
            ulong previousCreatureID,
            ulong currentCreatureID)
        {
            if (m_pickupType != ItemPickupType.CharacterDrop)
            {
                return;
            }

            StopTrackingDroppingCreature();
            if (!m_trackDroppingCreaturePosition ||
                currentCreatureID == k_NoDroppingCreature)
            {
                return;
            }

            m_trackCreatureRoutine = StartCoroutine(
                TrackDroppingCreaturePosition(currentCreatureID));
        }

        private IEnumerator TrackDroppingCreaturePosition(ulong creatureID)
        {
            WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
            while (IsSpawned && DroppingCreatureID.Value == creatureID)
            {
                if (NetworkManager.Singleton?.SpawnManager.SpawnedObjects
                        .TryGetValue(creatureID, out NetworkObject creature) == true)
                {
                    CharacterManager character =
                        creature.GetComponent<CharacterManager>();
                    transform.position = character != null
                        ? character.LockOnTransform.position
                        : creature.transform.position;
                }

                yield return waitForEndOfFrame;
            }

            m_trackCreatureRoutine = null;
        }

        private void StopTrackingDroppingCreature()
        {
            if (m_trackCreatureRoutine == null)
            {
                return;
            }

            StopCoroutine(m_trackCreatureRoutine);
            m_trackCreatureRoutine = null;
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
                Debug.LogError(
                    "World Spawn pickups require a non-negative unique item ID.",
                    this);
                gameObject.SetActive(false);
                return;
            }

            CharacterSaveData saveData =
                WorldSaveGameManager.Instance?.CurrentCharacterData;
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
                Debug.LogError(
                    "World Spawn pickups require a non-negative unique item ID.",
                    this);
                return;
            }

            CharacterSaveData saveData =
                WorldSaveGameManager.Instance?.CurrentCharacterData;
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
