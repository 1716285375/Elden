using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Owns one permanently opened, server-authoritative door or gate.
    /// </summary>
    public class DoorInteractable : Interactable
    {
        [Header("DOOR IDENTITY")]
        [SerializeField, HideInInspector] private string m_doorID;

        [Header("DOOR PRESENTATION")]
        [SerializeField] private Animator m_doorAnimator;
        [SerializeField] private string m_openAnimationState = "DoorOpen";
        [SerializeField] private string m_openedAnimationState = "DoorOpened";
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField] private AudioClip m_openingSound;

        [Header("ITEM REQUIREMENT")]
        [SerializeField] private bool m_requiresItem;
        [SerializeField] private Item m_itemRequiredToOpen;

        [Header("LINKED INTERACTIONS")]
        [SerializeField] private Interactable[] m_interactionsToDisable =
            System.Array.Empty<Interactable>();
        [SerializeField, Min(0.1f)] private float m_maxInteractionDistance = 5f;

        public readonly NetworkVariable<bool> IsOpen = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>Gets the stable build-index and object-name save identifier.</summary>
        public string DoorID => m_doorID;

        /// <summary>Gets whether this door requires a catalog item.</summary>
        public bool RequiresItem => m_requiresItem;

        protected override void Awake()
        {
            base.Awake();
            m_doorAnimator ??= GetComponentInChildren<Animator>(true);
            m_audioSource ??= GetComponent<AudioSource>();
            RefreshDoorID();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            m_doorAnimator ??= GetComponentInChildren<Animator>(true);
            m_audioSource ??= GetComponent<AudioSource>();
            RefreshDoorID();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            RefreshDoorID();
            IsOpen.OnValueChanged += OnOpenStateChanged;
            if (IsServer &&
                WorldSaveGameManager.Instance?.IsDoorOpened(m_doorID) == true)
            {
                IsOpen.Value = true;
            }

            if (IsOpen.Value)
            {
                ApplyOpenedPresentation();
                DisableDoorInteractions();
            }
        }

        public override void OnNetworkDespawn()
        {
            IsOpen.OnValueChanged -= OnOpenStateChanged;
            DisableDoorInteractions();
            base.OnNetworkDespawn();
        }

        /// <inheritdoc />
        public override bool CanInteract(PlayerManager player)
        {
            return !IsOpen.Value && base.CanInteract(player);
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            if (!HasRequiredItem(player))
            {
                string requiredItemName = m_itemRequiredToOpen?.ItemName;
                string message = string.IsNullOrWhiteSpace(requiredItemName)
                    ? "Locked"
                    : $"Requires {requiredItemName}";
                PlayerUIManager.Instance?.PlayerUIPopUpManager
                    ?.SendPlayerMessagePopup(message);
                return;
            }

            OpenDoorServerRpc(player.NetworkObjectId);
        }

        /// <inheritdoc />
        public override bool ActivateFromServer(PlayerManager player)
        {
            if (!IsServer ||
                IsOpen.Value ||
                player == null ||
                player.IsDead ||
                !HasRequiredItem(player))
            {
                return false;
            }

            OpenDoorFromServer();
            return true;
        }

        /// <summary>Disables the door, reverse-side prompt, and linked mechanisms.</summary>
        public void DisableDoorInteractions()
        {
            SetInteractionAvailable(false);
            foreach (Interactable linkedInteraction in m_interactionsToDisable)
            {
                linkedInteraction?.SetInteractionAvailable(false);
            }
        }

        /// <summary>The replicated open state owns the complete interaction lifecycle.</summary>
        public override void CompleteInteraction()
        {
            DisableDoorInteractions();
        }

        [ServerRpc(RequireOwnership = false)]
        private void OpenDoorServerRpc(
            ulong playerNetworkObjectId,
            ServerRpcParams serverRpcParams = default)
        {
            if (IsOpen.Value ||
                !TryResolvePlayer(
                    playerNetworkObjectId,
                    out PlayerManager player) ||
                player.OwnerClientId != serverRpcParams.Receive.SenderClientId ||
                player.IsDead ||
                !IsPlayerWithinInteractionDistance(player) ||
                !HasRequiredItem(player))
            {
                return;
            }

            OpenDoorFromServer();
        }

        private void OpenDoorFromServer()
        {
            IsOpen.Value = true;
            WorldSaveGameManager.Instance?.RecordOpenedDoor(
                m_doorID,
                true);
            OpenDoorClientRpc();
        }

        [ClientRpc]
        private void OpenDoorClientRpc()
        {
            if (m_doorAnimator != null &&
                !string.IsNullOrWhiteSpace(m_openAnimationState))
            {
                m_doorAnimator.Play(m_openAnimationState, 0, 0f);
            }

            if (m_audioSource != null && m_openingSound != null)
            {
                m_audioSource.PlayOneShot(m_openingSound);
            }

            DisableDoorInteractions();
        }

        private void ApplyOpenedPresentation()
        {
            if (m_doorAnimator == null ||
                string.IsNullOrWhiteSpace(m_openedAnimationState))
            {
                return;
            }

            m_doorAnimator.Play(m_openedAnimationState, 0, 0f);
            m_doorAnimator.Update(0f);
        }

        private void OnOpenStateChanged(bool wasOpen, bool isOpen)
        {
            if (isOpen)
            {
                DisableDoorInteractions();
            }
        }

        private bool HasRequiredItem(PlayerManager player)
        {
            if (!m_requiresItem)
            {
                return true;
            }

            if (m_itemRequiredToOpen == null ||
                player?.InventoryManager?.ItemsInInventory == null)
            {
                return false;
            }

            foreach (Item item in player.InventoryManager.ItemsInInventory)
            {
                if (item != null &&
                    item.ItemID == m_itemRequiredToOpen.ItemID)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPlayerWithinInteractionDistance(PlayerManager player)
        {
            if (InteractableCollider == null)
            {
                return false;
            }

            Vector3 closestPoint = InteractableCollider.ClosestPoint(
                player.transform.position);
            return Vector3.Distance(closestPoint, player.transform.position) <=
                m_maxInteractionDistance;
        }

        private void RefreshDoorID()
        {
            m_doorID = $"{gameObject.scene.buildIndex}_{gameObject.name}";
        }

        private static bool TryResolvePlayer(
            ulong playerNetworkObjectId,
            out PlayerManager player)
        {
            player = null;
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.SpawnManager.SpawnedObjects.TryGetValue(
                    playerNetworkObjectId,
                    out NetworkObject playerNetworkObject))
            {
                return false;
            }

            player = playerNetworkObject.GetComponent<PlayerManager>();
            return player != null;
        }
    }
}
