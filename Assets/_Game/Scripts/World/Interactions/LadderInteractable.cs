using System.Collections;
using UnityEngine;

namespace ZZ
{
    /// <summary>Defines one top or bottom entrance on a shared-network ladder.</summary>
    public sealed class LadderInteractable : Interactable
    {
        [Header("LADDER ENTRANCE")]
        [SerializeField] private bool m_isTopEntrance;
        [SerializeField] private Transform m_startPosition;
        [SerializeField] private Transform m_ladderHorizontalPosition;

        [Header("TOP EXIT")]
        [SerializeField] private Transform m_topExitLeftHandPosition;
        [SerializeField] private Transform m_topExitRightHandPosition;
        [SerializeField] private Transform m_maxTopExitPosition;

        private PlayerManager m_localPlayerInTrigger;
        private Coroutine m_waitForClimbCompletionRoutine;

        public bool IsTopEntrance => m_isTopEntrance;
        public Transform StartPosition => m_startPosition;
        public Transform LadderHorizontalPosition => m_ladderHorizontalPosition;
        public float MaxTopExitHeight => m_maxTopExitPosition != null
            ? m_maxTopExitPosition.position.y
            : transform.position.y;

        /// <summary>Gets the hand-specific minimum platform height.</summary>
        public float GetTopExitHeight(LadderHandState handState)
        {
            Transform exitPosition = handState == LadderHandState.Left
                ? m_topExitLeftHandPosition
                : m_topExitRightHandPosition;
            return exitPosition != null
                ? exitPosition.position.y
                : MaxTopExitHeight;
        }

        /// <inheritdoc />
        public override bool CanInteract(PlayerManager player)
        {
            return player?.PlayerNetworkManager?.IsClimbingLadder.Value != true &&
                base.CanInteract(player);
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (CanInteract(player))
            {
                player.LocomotionManager?.BeginLadderClimb(this);
            }
        }

        /// <summary>Ladder entrances remain reusable after every completed climb.</summary>
        public override void CompleteInteraction()
        {
        }

        protected override void OnTriggerEnter(Collider other)
        {
            base.OnTriggerEnter(other);
            PlayerManager player = other.GetComponentInParent<PlayerManager>();
            if (player == null || !player.IsOwner)
            {
                return;
            }

            m_localPlayerInTrigger = player;
            if (player.PlayerNetworkManager?.IsClimbingLadder.Value != true)
            {
                return;
            }

            player.LocomotionManager?.SetLadderExitInteractable(this, true);
            RestartClimbCompletionWait(player);
        }

        protected override void OnTriggerExit(Collider other)
        {
            base.OnTriggerExit(other);
            PlayerManager player = other.GetComponentInParent<PlayerManager>();
            if (player == null || !player.IsOwner ||
                player != m_localPlayerInTrigger)
            {
                return;
            }

            player.LocomotionManager?.SetLadderExitInteractable(this, false);
            m_localPlayerInTrigger = null;
            StopClimbCompletionWait();
        }

        private void RestartClimbCompletionWait(PlayerManager player)
        {
            StopClimbCompletionWait();
            m_waitForClimbCompletionRoutine = StartCoroutine(
                WaitForClimbCompletion(player));
        }

        private IEnumerator WaitForClimbCompletion(PlayerManager player)
        {
            while (m_localPlayerInTrigger == player &&
                player.PlayerNetworkManager?.IsClimbingLadder.Value == true)
            {
                yield return null;
            }

            m_waitForClimbCompletionRoutine = null;
            if (m_localPlayerInTrigger == player)
            {
                player.InteractionManager?.CheckForInteractable();
            }
        }

        private void StopClimbCompletionWait()
        {
            if (m_waitForClimbCompletionRoutine == null)
            {
                return;
            }

            StopCoroutine(m_waitForClimbCompletionRoutine);
            m_waitForClimbCompletionRoutine = null;
        }
    }
}
