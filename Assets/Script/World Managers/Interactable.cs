using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Defines the shared prompt, authority policy, and trigger lifecycle for a world interaction.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class Interactable : NetworkBehaviour
    {
        [SerializeField] private string m_interactableText = "Interact";
        [SerializeField] private Collider m_interactableCollider;
        [SerializeField] private bool m_hostOnlyInteractable = true;
        [SerializeField] private bool m_shouldDisableColliderAfterInteraction = true;

        private Rigidbody m_rigidbody;
        private readonly HashSet<PlayerInteractionManager> m_registeredPlayers = new();

        public string InteractableText => m_interactableText;
        public Collider InteractableCollider => m_interactableCollider;
        public bool IsHostOnlyInteractable => m_hostOnlyInteractable;

        protected virtual void Awake()
        {
            m_rigidbody = GetComponent<Rigidbody>();
            m_interactableCollider ??= GetComponentInChildren<Collider>(true);
            ConfigurePhysicsComponents();
        }

        protected virtual void OnValidate()
        {
            m_rigidbody = GetComponent<Rigidbody>();
            m_interactableCollider ??= GetComponentInChildren<Collider>(true);
            ConfigurePhysicsComponents();
        }

        private void OnDisable()
        {
            ClearInteractionRegistrations();
        }

        /// <summary>Returns whether the supplied local player may use this interaction.</summary>
        public virtual bool CanInteract(PlayerManager player)
        {
            return player != null &&
                player.IsOwner &&
                enabled &&
                gameObject.activeInHierarchy &&
                m_interactableCollider != null &&
                m_interactableCollider.enabled &&
                (!m_hostOnlyInteractable || player.IsServer);
        }

        /// <summary>Performs the object-specific interaction after local eligibility is verified.</summary>
        public virtual void Interact(PlayerManager player)
        {
        }

        /// <summary>Saves local character state after a persistent world interaction.</summary>
        protected static void SaveGameAfterInteraction(PlayerManager player)
        {
            WorldSaveGameManager saveGameManager =
                WorldSaveGameManager.Instance;
            if (player?.IsOwner == true && saveGameManager?.CanSaveGame == true)
            {
                saveGameManager.SaveGame();
            }
        }

        /// <summary>Applies the configured one-shot collider policy after a successful interaction.</summary>
        public void CompleteInteraction()
        {
            if (m_shouldDisableColliderAfterInteraction && m_interactableCollider != null)
            {
                m_interactableCollider.enabled = false;
                ClearInteractionRegistrations();
            }
        }

        /// <summary>Updates the shared prompt and refreshes every overlapping local player.</summary>
        protected void SetInteractableText(string interactableText)
        {
            m_interactableText = interactableText ?? string.Empty;
            foreach (PlayerInteractionManager interactionManager in m_registeredPlayers)
            {
                interactionManager?.RefreshInteractionPrompt(this);
            }
        }

        /// <summary>Restores or suspends trigger detection for reusable derived interactions.</summary>
        protected void SetInteractionColliderEnabled(bool isEnabled)
        {
            if (m_interactableCollider != null)
            {
                m_interactableCollider.enabled = isEnabled;
            }

            if (!isEnabled)
            {
                ClearInteractionRegistrations();
            }
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            PlayerInteractionManager interactionManager =
                other.GetComponentInParent<PlayerInteractionManager>();
            if (interactionManager != null &&
                m_registeredPlayers.Add(interactionManager))
            {
                interactionManager.AddInteractionToList(this);
            }
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            PlayerInteractionManager interactionManager =
                other.GetComponentInParent<PlayerInteractionManager>();
            if (interactionManager != null &&
                m_registeredPlayers.Remove(interactionManager))
            {
                interactionManager.RemoveInteractionFromList(this);
            }
        }

        private void ClearInteractionRegistrations()
        {
            foreach (PlayerInteractionManager interactionManager in m_registeredPlayers)
            {
                interactionManager?.RemoveInteractionFromList(this);
            }

            m_registeredPlayers.Clear();
        }

        private void ConfigurePhysicsComponents()
        {
            if (m_rigidbody != null)
            {
                m_rigidbody.isKinematic = true;
                m_rigidbody.useGravity = false;
            }

            if (m_interactableCollider != null)
            {
                m_interactableCollider.isTrigger = true;
            }
        }
    }
}
