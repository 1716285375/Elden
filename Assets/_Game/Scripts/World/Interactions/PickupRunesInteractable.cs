using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>Owns one replicated Rune recovery point until its original owner reclaims it.</summary>
    public class PickupRunesInteractable : Interactable
    {
        private const float k_MaximumPickupDistance = 4f;
        private const ulong k_NoOwnerClientId = ulong.MaxValue;

        private readonly NetworkVariable<int> m_runeCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> m_ownerClientId = new(
            k_NoOwnerClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool m_hasBeenClaimed;
        private bool m_localPickupRequested;

        /// <summary>Gets the synchronized Rune balance held by this recovery point.</summary>
        public int RuneCount => Mathf.Max(0, m_runeCount.Value);

        /// <summary>Gets the client that created and may reclaim this recovery point.</summary>
        public ulong DeadSpotOwnerClientId => m_ownerClientId.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_hasBeenClaimed = false;
            m_localPickupRequested = false;
            m_runeCount.OnValueChanged += OnRuneCountChanged;
            OnRuneCountChanged(m_runeCount.Value, m_runeCount.Value);
        }

        public override void OnNetworkDespawn()
        {
            m_runeCount.OnValueChanged -= OnRuneCountChanged;
            base.OnNetworkDespawn();
        }

        /// <summary>Initializes server-owned recovery state after the prefab is spawned.</summary>
        public bool InitializeDeadSpot(int runeCount, ulong ownerClientId)
        {
            if (!IsSpawned || !IsServer || runeCount <= 0)
            {
                return false;
            }

            m_ownerClientId.Value = ownerClientId;
            m_runeCount.Value = runeCount;
            return true;
        }

        /// <inheritdoc />
        public override bool CanInteract(PlayerManager player)
        {
            return base.CanInteract(player) &&
                player != null &&
                !player.IsDead &&
                !player.IsPerformingAction &&
                !m_localPickupRequested &&
                RuneCount > 0 &&
                player.OwnerClientId == DeadSpotOwnerClientId;
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            base.Interact(player);
            m_localPickupRequested = true;
            ReclaimRunesServerRpc(player.NetworkObjectId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReclaimRunesServerRpc(
            ulong playerNetworkObjectId,
            RpcParams rpcParams = default)
        {
            if (!IsServer ||
                m_hasBeenClaimed ||
                rpcParams.Receive.SenderClientId != DeadSpotOwnerClientId ||
                !TryResolvePlayer(playerNetworkObjectId, out PlayerManager player) ||
                player.OwnerClientId != DeadSpotOwnerClientId ||
                player.IsDead ||
                Vector3.SqrMagnitude(player.transform.position - transform.position) >
                    k_MaximumPickupDistance * k_MaximumPickupDistance)
            {
                return;
            }

            m_hasBeenClaimed = true;
            SetInteractionColliderEnabled(false);
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { DeadSpotOwnerClientId }
                }
            };
            RestoreRunesClientRpc(RuneCount, clientRpcParams);
            StartCoroutine(DespawnAtEndOfFrame());
        }

        [ClientRpc]
        private void RestoreRunesClientRpc(
            int runeCount,
            ClientRpcParams clientRpcParams = default)
        {
            NetworkObject localPlayerObject =
                NetworkManager.Singleton?.LocalClient?.PlayerObject;
            PlayerManager player = localPlayerObject?.GetComponent<PlayerManager>();
            if (player == null || !player.IsOwner)
            {
                return;
            }

            player.PlayerStatsManager?.AddRunes(runeCount);
            WorldSaveGameManager saveGameManager = WorldSaveGameManager.Instance;
            saveGameManager?.ClearDeadSpot(false);
            if (saveGameManager?.CanSaveGame == true)
            {
                saveGameManager.SaveGame();
            }
        }

        private IEnumerator DespawnAtEndOfFrame()
        {
            yield return null;
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        private void OnRuneCountChanged(int previousRuneCount, int currentRuneCount)
        {
            SetInteractableText(
                currentRuneCount > 0
                    ? $"Reclaim {currentRuneCount} Runes"
                    : "Reclaim Runes");
            SetInteractionColliderEnabled(currentRuneCount > 0);
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
