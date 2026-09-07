using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Maintains the locally owned player's nearby interactions and presents the active prompt.
    /// </summary>
    [RequireComponent(typeof(PlayerManager))]
    public class PlayerInteractionManager : MonoBehaviour
    {
        private readonly List<Interactable> m_currentInteractableActions = new();

        private PlayerManager m_player;
        private Interactable m_activeInteractable;
        private bool m_hasOpenPrompt;
        private bool m_isRefreshingPrompt;

        public IReadOnlyList<Interactable> CurrentInteractableActions =>
            m_currentInteractableActions;

        private void Awake()
        {
            m_player = GetComponent<PlayerManager>();
        }

        private void OnDisable()
        {
            ClearInteractions();
        }

        private void FixedUpdate()
        {
            CheckForInteractable();
        }

        /// <summary>Adds one nearby interaction without creating duplicate entries.</summary>
        public void AddInteractionToList(Interactable interactable)
        {
            if (interactable != null &&
                !m_currentInteractableActions.Contains(interactable))
            {
                m_currentInteractableActions.Add(interactable);
            }
        }

        /// <summary>Removes one interaction when its trigger no longer overlaps the player.</summary>
        public void RemoveInteractionFromList(Interactable interactable)
        {
            m_currentInteractableActions.Remove(interactable);
            if (m_activeInteractable == interactable)
            {
                ClearActiveInteraction();
            }
        }

        /// <summary>Removes destroyed interaction references by iterating backwards.</summary>
        public void RefreshInteractionList()
        {
            for (int index = m_currentInteractableActions.Count - 1;
                index >= 0;
                index--)
            {
                if (m_currentInteractableActions[index] == null)
                {
                    m_currentInteractableActions.RemoveAt(index);
                }
            }
        }

        /// <summary>Updates the active interaction and its local prompt.</summary>
        public void CheckForInteractable()
        {
            if (m_player == null || !m_player.IsOwner)
            {
                return;
            }

            RefreshInteractionList();
            if (m_currentInteractableActions.Count == 0)
            {
                ClearActiveInteraction();
                return;
            }

            PlayerUIPopUpManager popUpManager =
                PlayerUIManager.Instance?.PlayerUIPopUpManager;
            if (popUpManager?.IsDialoguePopupOpen == true)
            {
                popUpManager.ClosePlayerMessagePopup();
                m_hasOpenPrompt = false;
                return;
            }

            if (m_currentInteractableActions[0] == null)
            {
                m_currentInteractableActions.RemoveAt(0);
                CheckForInteractable();
                return;
            }

            if (PlayerInputManager.Instance?.IsMovementInputEnabled != true)
            {
                ClearActiveInteraction();
                return;
            }

            Interactable candidate = FindFirstEligibleInteraction();
            if (candidate == null)
            {
                ClearActiveInteraction();
                return;
            }

            if (m_activeInteractable == candidate && m_hasOpenPrompt)
            {
                return;
            }

            m_activeInteractable = candidate;
            SendPlayerMessagePopup(candidate.InteractableText);
            m_hasOpenPrompt = true;
        }

        /// <summary>Uses the first eligible interaction and applies its one-shot policy.</summary>
        public void HandleInteractionInput()
        {
            if (m_player == null || !m_player.IsOwner || m_player.IsDead ||
                m_player.IsPerformingAction || m_player.PlayerCombatManager?.IsUsingItem == true)
            {
                return;
            }

            CloseAllPopUpWindows();
            m_hasOpenPrompt = false;
            CheckForInteractable();
            Interactable interactable = m_activeInteractable;
            if (interactable == null || !interactable.CanInteract(m_player))
            {
                return;
            }

            interactable.Interact(m_player);
            interactable.CompleteInteraction();
            CheckForInteractable();
        }

        /// <summary>Refreshes a changed prompt while the interaction remains active.</summary>
        public void RefreshInteractionPrompt(Interactable interactable)
        {
            if (m_player == null ||
                !m_player.IsOwner ||
                m_activeInteractable != interactable ||
                m_isRefreshingPrompt)
            {
                return;
            }

            m_isRefreshingPrompt = true;
            try
            {
                if (!interactable.CanInteract(m_player))
                {
                    return;
                }

                SendPlayerMessagePopup(interactable.InteractableText);
                m_hasOpenPrompt = true;
            }
            finally
            {
                m_isRefreshingPrompt = false;
            }
        }

        /// <summary>Clears every candidate and closes the local interaction prompt.</summary>
        public void ClearInteractions()
        {
            m_currentInteractableActions.Clear();
            ClearActiveInteraction();
        }

        private static void SendPlayerMessagePopup(string message)
        {
            PlayerUIManager.Instance?.PlayerUIPopUpManager
                ?.SendPlayerMessagePopup(message);
        }

        private static void CloseAllPopUpWindows()
        {
            PlayerUIManager.Instance?.PlayerUIPopUpManager
                ?.CloseAllPopUpWindows();
        }

        private static void ClosePlayerMessagePopup()
        {
            PlayerUIManager.Instance?.PlayerUIPopUpManager
                ?.ClosePlayerMessagePopup();
        }

        private Interactable FindFirstEligibleInteraction()
        {
            Interactable nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (Interactable interactable in m_currentInteractableActions)
            {
                if (interactable != null && interactable.CanInteract(m_player))
                {
                    float distance = (interactable.transform.position - transform.position).sqrMagnitude;
                    if (distance < nearestDistance)
                    {
                        nearest = interactable;
                        nearestDistance = distance;
                    }
                }
            }

            return nearest;
        }

        private void ClearActiveInteraction()
        {
            if (!m_hasOpenPrompt)
            {
                return;
            }

            m_activeInteractable = null;
            m_hasOpenPrompt = false;
            ClosePlayerMessagePopup();
        }
    }
}
