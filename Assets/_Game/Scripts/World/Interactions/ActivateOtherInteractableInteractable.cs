using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Provides a reusable synchronized lever that activates another interaction.
    /// </summary>
    public class ActivateOtherInteractableInteractable : Interactable
    {
        [Header("LINKED ACTIVATION")]
        [SerializeField] private Interactable m_interactableToActivate;
        [SerializeField] private bool m_useOnce = true;
        [SerializeField, Min(0.1f)] private float m_reuseDelay = 2f;
        [SerializeField, Min(0.1f)] private float m_maxInteractionDistance = 5f;

        [Header("LEVER PRESENTATION")]
        [SerializeField] private Animator m_leverAnimator;
        [SerializeField] private string m_pullAnimationState = "LeverPull";
        [SerializeField] private string m_pulledAnimationState = "LeverPulled";
        [SerializeField] private string m_resetAnimationState = "LeverReset";

        public readonly NetworkVariable<bool> LeverHasBeenPulled = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Coroutine m_resetRoutine;

        /// <summary>Gets the generic interaction targeted by this mechanism.</summary>
        public Interactable InteractableToActivate => m_interactableToActivate;

        /// <summary>Gets whether this mechanism is permanently consumed.</summary>
        public bool UseOnce => m_useOnce;

        protected override void Awake()
        {
            base.Awake();
            m_leverAnimator ??= GetComponentInChildren<Animator>(true);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            LeverHasBeenPulled.OnValueChanged += OnLeverStateChanged;
            SetInteractionAvailable(!LeverHasBeenPulled.Value);
            if (LeverHasBeenPulled.Value)
            {
                ApplyStaticLeverPresentation();
            }
        }

        public override void OnNetworkDespawn()
        {
            LeverHasBeenPulled.OnValueChanged -= OnLeverStateChanged;
            if (m_resetRoutine != null)
            {
                StopCoroutine(m_resetRoutine);
                m_resetRoutine = null;
            }

            SetInteractionAvailable(false);
            base.OnNetworkDespawn();
        }

        /// <inheritdoc />
        public override bool CanInteract(PlayerManager player)
        {
            return !LeverHasBeenPulled.Value && base.CanInteract(player);
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            PullLeverServerRpc(player.NetworkObjectId);
        }

        /// <summary>The synchronized lever state owns its reusable collider lifecycle.</summary>
        public override void CompleteInteraction()
        {
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PullLeverServerRpc(
            ulong playerNetworkObjectId,
            RpcParams rpcParams = default)
        {
            if (LeverHasBeenPulled.Value ||
                !TryResolvePlayer(
                    playerNetworkObjectId,
                    out PlayerManager player) ||
                player.OwnerClientId != rpcParams.Receive.SenderClientId ||
                player.IsDead ||
                !IsPlayerWithinInteractionDistance(player) ||
                m_interactableToActivate == null ||
                !m_interactableToActivate.ActivateFromServer(player))
            {
                return;
            }

            LeverHasBeenPulled.Value = true;
            PullLeverClientRpc();
            if (!m_useOnce)
            {
                m_resetRoutine = StartCoroutine(ResetLeverAfterDelay());
            }
        }

        [ClientRpc]
        private void PullLeverClientRpc()
        {
            m_leverAnimator?.Play(m_pullAnimationState, 0, 0f);
        }

        [ClientRpc]
        private void ResetLeverClientRpc()
        {
            m_leverAnimator?.Play(m_resetAnimationState, 0, 0f);
        }

        private IEnumerator ResetLeverAfterDelay()
        {
            yield return new WaitForSeconds(m_reuseDelay);
            LeverHasBeenPulled.Value = false;
            ResetLeverClientRpc();
            m_resetRoutine = null;
        }

        private void ApplyStaticLeverPresentation()
        {
            if (m_leverAnimator == null ||
                string.IsNullOrWhiteSpace(m_pulledAnimationState))
            {
                return;
            }

            m_leverAnimator.Play(m_pulledAnimationState, 0, 0f);
            m_leverAnimator.Update(0f);
        }

        private void OnLeverStateChanged(bool wasPulled, bool isPulled)
        {
            SetInteractionAvailable(!isPulled);
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
