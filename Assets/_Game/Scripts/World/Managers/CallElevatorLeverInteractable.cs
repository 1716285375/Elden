using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>Adds synchronized lever feedback and mutual exclusion to a call station.</summary>
    public class CallElevatorLeverInteractable : CallElevatorInteractable
    {
        private const string k_PullLeverState = "PullLever";
        private const string k_ReleaseLeverState = "ReleaseLever";

        [Header("CALL ELEVATOR LEVER")]
        [SerializeField] private Animator m_leverAnimator;
        [SerializeField] private CallElevatorLeverInteractable m_oppositeLever;
        [SerializeField, Min(0f)]
        private float m_timeToWaitAfterPullingLeverToMoveElevator = 1f;

        private readonly NetworkVariable<bool> m_leverHasBeenPulled = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Coroutine m_activateElevatorRoutine;
        private bool m_hasAppliedPulledPresentation;

        /// <summary>Gets the synchronized mechanical lock state.</summary>
        public bool LeverHasBeenPulled => m_leverHasBeenPulled.Value;

        protected override void Awake()
        {
            base.Awake();
            m_leverAnimator ??= GetComponentInChildren<Animator>(true);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_leverHasBeenPulled.OnValueChanged += OnLeverStateChanged;
            ApplyLeverPresentation(m_leverHasBeenPulled.Value);
        }

        public override void OnNetworkDespawn()
        {
            m_leverHasBeenPulled.OnValueChanged -= OnLeverStateChanged;
            if (m_activateElevatorRoutine != null)
            {
                StopCoroutine(m_activateElevatorRoutine);
                m_activateElevatorRoutine = null;
            }

            base.OnNetworkDespawn();
        }

        /// <inheritdoc />
        public override bool CanInteract(PlayerManager player)
        {
            return base.CanInteract(player) &&
                !m_leverHasBeenPulled.Value &&
                (m_oppositeLever == null ||
                    !m_oppositeLever.LeverHasBeenPulled);
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

        [ServerRpc(RequireOwnership = false)]
        private void PullLeverServerRpc(
            ulong playerNetworkObjectId,
            ServerRpcParams serverRpcParams = default)
        {
            if (m_leverHasBeenPulled.Value ||
                m_oppositeLever != null &&
                m_oppositeLever.LeverHasBeenPulled ||
                !ValidateInteractionRequest(
                    playerNetworkObjectId,
                    serverRpcParams.Receive.SenderClientId))
            {
                return;
            }

            m_leverHasBeenPulled.Value = true;
            PullLeverClientRpc();
            m_activateElevatorRoutine = StartCoroutine(
                ActivateElevatorAfterLeverDelay());
        }

        [ClientRpc]
        private void PullLeverClientRpc()
        {
            ApplyLeverPresentation(true);
        }

        [ClientRpc]
        private void ReleaseLeverClientRpc()
        {
            ApplyLeverPresentation(false);
        }

        private IEnumerator ActivateElevatorAfterLeverDelay()
        {
            if (m_timeToWaitAfterPullingLeverToMoveElevator > 0f)
            {
                yield return new WaitForSeconds(
                    m_timeToWaitAfterPullingLeverToMoveElevator);
            }

            bool didStart = Elevator != null &&
                Elevator.ActivateElevatorForDestinationFromServer(
                    IsAtHighDestination);
            if (!didStart)
            {
                ReleaseLeverFromServer();
                yield break;
            }

            while (Elevator != null && Elevator.IsMoving)
            {
                yield return null;
            }

            ReleaseLeverFromServer();
        }

        private void ReleaseLeverFromServer()
        {
            if (!IsServer || !m_leverHasBeenPulled.Value)
            {
                return;
            }

            m_leverHasBeenPulled.Value = false;
            ReleaseLeverClientRpc();
            m_activateElevatorRoutine = null;
        }

        private void OnLeverStateChanged(bool wasPulled, bool isPulled)
        {
            ApplyLeverPresentation(isPulled);
        }

        private void ApplyLeverPresentation(bool isPulled)
        {
            if (m_hasAppliedPulledPresentation == isPulled)
            {
                return;
            }

            m_hasAppliedPulledPresentation = isPulled;
            m_leverAnimator?.Play(
                isPulled
                    ? k_PullLeverState
                    : k_ReleaseLeverState);
        }
    }
}
