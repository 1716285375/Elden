using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>Provides one floor-specific reusable call interaction.</summary>
    public class CallElevatorInteractable : Interactable
    {
        [Header("CALL ELEVATOR")]
        [SerializeField] private ElevatorInteractable m_elevator;
        [SerializeField] private bool m_isAtHighDestination;
        [SerializeField, Min(0.1f)] private float m_maxInteractionDistance = 5f;

        /// <summary>Gets the shared elevator controlled by this station.</summary>
        protected ElevatorInteractable Elevator => m_elevator;

        /// <summary>Gets whether this station recalls the platform upward.</summary>
        public bool IsAtHighDestination => m_isAtHighDestination;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_elevator?.RegisterCallStation(this);
            RefreshElevatorAvailability();
        }

        public override void OnNetworkDespawn()
        {
            m_elevator?.UnregisterCallStation(this);
            base.OnNetworkDespawn();
        }

        /// <inheritdoc />
        public override bool CanInteract(PlayerManager player)
        {
            return player != null &&
                player.IsOwner &&
                enabled &&
                gameObject.activeInHierarchy &&
                InteractableCollider != null &&
                InteractableCollider.enabled &&
                IsSpawned &&
                m_elevator != null &&
                m_elevator.IsSpawned &&
                !m_elevator.IsMoving &&
                !m_elevator.IsAtDestination(m_isAtHighDestination);
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            CallElevatorServerRpc(player.NetworkObjectId);
        }

        /// <summary>Call stations remain reusable and derive availability from elevator state.</summary>
        public override void CompleteInteraction()
        {
        }

        /// <summary>Refreshes any active local prompt after movement state changes.</summary>
        public virtual void RefreshElevatorAvailability()
        {
            SetInteractableText("Call Elevator");
        }

        /// <summary>Validates one owner request against this station's Trigger.</summary>
        protected bool ValidateInteractionRequest(
            ulong playerNetworkObjectId,
            ulong senderClientId)
        {
            if (m_elevator == null ||
                m_elevator.IsMoving ||
                m_elevator.IsAtDestination(m_isAtHighDestination) ||
                !TryResolvePlayer(
                    playerNetworkObjectId,
                    out PlayerManager player) ||
                player.OwnerClientId != senderClientId ||
                player.IsDead ||
                InteractableCollider == null)
            {
                return false;
            }

            Vector3 closestPoint = InteractableCollider.ClosestPoint(
                player.transform.position);
            return Vector3.Distance(closestPoint, player.transform.position) <=
                m_maxInteractionDistance;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void CallElevatorServerRpc(
            ulong playerNetworkObjectId,
            RpcParams rpcParams = default)
        {
            if (ValidateInteractionRequest(
                    playerNetworkObjectId,
                    rpcParams.Receive.SenderClientId))
            {
                m_elevator.ActivateElevatorForDestinationFromServer(
                    m_isAtHighDestination);
            }
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
