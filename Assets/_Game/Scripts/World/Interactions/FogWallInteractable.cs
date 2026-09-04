using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Authorizes every player to traverse an active Boss fog wall with synchronized feedback.
    /// </summary>
    public class FogWallInteractable : Interactable
    {
        [Header("FOG WALL")]
        [SerializeField] private Collider m_fogWallCollider;
        [SerializeField] private Renderer[] m_fogWallRenderers =
            System.Array.Empty<Renderer>();
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField] private AudioClip m_passThroughSound;
        [SerializeField, Min(0.1f)] private float m_passThroughDuration = 3f;
        [SerializeField, Min(0.1f)] private float m_maxInteractionDistance = 5f;

        private readonly Dictionary<ulong, Coroutine> m_passThroughRoutines = new();
        private readonly Dictionary<ulong, PlayerManager> m_passingPlayers = new();
        private bool m_isFogWallActive;

        public bool IsFogWallActive => m_isFogWallActive;
        public Collider FogWallCollider => m_fogWallCollider;
        public float PassThroughDuration => m_passThroughDuration;

        protected override void Awake()
        {
            base.Awake();
            m_audioSource ??= GetComponent<AudioSource>();
            SetFogWallActive(false);
        }

        public override void OnNetworkDespawn()
        {
            RestoreAllPassingPlayers();
            base.OnNetworkDespawn();
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (!CanInteract(player) || !IsSpawned)
            {
                return;
            }

            RequestPassThroughServerRpc(player.NetworkObjectId);
        }

        /// <summary>Keeps the scene NetworkObject spawned while toggling its presentation.</summary>
        public void SetFogWallActive(bool isActive)
        {
            m_isFogWallActive = isActive;
            if (m_fogWallCollider != null)
            {
                m_fogWallCollider.enabled = isActive;
            }

            SetInteractionColliderEnabled(isActive);
            foreach (Renderer fogWallRenderer in m_fogWallRenderers)
            {
                if (fogWallRenderer != null)
                {
                    fogWallRenderer.enabled = isActive;
                }
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestPassThroughServerRpc(
            ulong playerNetworkObjectId,
            RpcParams rpcParams = default)
        {
            if (!m_isFogWallActive ||
                !TryResolvePlayer(playerNetworkObjectId, out PlayerManager player) ||
                player.OwnerClientId != rpcParams.Receive.SenderClientId ||
                player.IsDead ||
                !IsPlayerWithinInteractionDistance(player))
            {
                return;
            }

            BeginPassThrough(player);
            BeginPassThroughClientRpc(playerNetworkObjectId);
        }

        [ClientRpc]
        private void BeginPassThroughClientRpc(ulong playerNetworkObjectId)
        {
            if (IsServer ||
                !TryResolvePlayer(playerNetworkObjectId, out PlayerManager player))
            {
                return;
            }

            BeginPassThrough(player);
        }

        private void BeginPassThrough(PlayerManager player)
        {
            ulong playerNetworkObjectId = player.NetworkObjectId;
            if (m_passThroughRoutines.TryGetValue(
                    playerNetworkObjectId,
                    out Coroutine existingRoutine))
            {
                StopCoroutine(existingRoutine);
                RestorePlayerPassThrough(player);
            }

            FacePlayerThroughFogWall(player);
            player.SetInvulnerable(true);
            player.PlayerAnimatorManager?.PlayTargetActionAnimation(
                CharacterActionAnimation.PassThroughFog,
                true,
                true,
                false,
                false);
            PlayPassThroughSound();
            m_passingPlayers[playerNetworkObjectId] = player;
            m_passThroughRoutines[playerNetworkObjectId] = StartCoroutine(
                MaintainPassThroughWindow(playerNetworkObjectId, player));
        }

        private IEnumerator MaintainPassThroughWindow(
            ulong playerNetworkObjectId,
            PlayerManager player)
        {
            CharacterController characterController =
                player.GetComponent<CharacterController>();
            if (m_fogWallCollider != null && characterController != null)
            {
                Physics.IgnoreCollision(
                    characterController,
                    m_fogWallCollider,
                    true);
            }

            yield return new WaitForSeconds(m_passThroughDuration);

            RestorePlayerPassThrough(player);
            m_passThroughRoutines.Remove(playerNetworkObjectId);
            m_passingPlayers.Remove(playerNetworkObjectId);
        }

        private void RestorePlayerPassThrough(PlayerManager player)
        {
            if (player == null)
            {
                return;
            }

            CharacterController characterController =
                player.GetComponent<CharacterController>();
            if (m_fogWallCollider != null && characterController != null)
            {
                Physics.IgnoreCollision(
                    characterController,
                    m_fogWallCollider,
                    false);
            }

            player.SetInvulnerable(false);
            player.ResetActionFlags();
        }

        private void RestoreAllPassingPlayers()
        {
            StopAllCoroutines();
            foreach (PlayerManager player in m_passingPlayers.Values)
            {
                RestorePlayerPassThrough(player);
            }

            m_passThroughRoutines.Clear();
            m_passingPlayers.Clear();
        }

        private bool IsPlayerWithinInteractionDistance(PlayerManager player)
        {
            Vector3 closestPoint = InteractableCollider.ClosestPoint(
                player.transform.position);
            return Vector3.Distance(closestPoint, player.transform.position) <=
                m_maxInteractionDistance;
        }

        private void FacePlayerThroughFogWall(PlayerManager player)
        {
            float playerSide = Vector3.Dot(
                player.transform.position - transform.position,
                transform.forward);
            Vector3 passDirection = playerSide <= 0f
                ? transform.forward
                : -transform.forward;
            player.transform.rotation = Quaternion.LookRotation(
                passDirection,
                Vector3.up);
        }

        private void PlayPassThroughSound()
        {
            if (m_audioSource != null && m_passThroughSound != null)
            {
                m_audioSource.PlayOneShot(m_passThroughSound);
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
