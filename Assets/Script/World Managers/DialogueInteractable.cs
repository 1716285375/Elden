using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>Provides a reusable networked Talk trigger for one dialogue-capable AI.</summary>
    public class DialogueInteractable : Interactable
    {
        public NetworkVariable<bool> IsDialogueAvailable =
            new NetworkVariable<bool>(
                true,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private AICharacterManager m_aiCharacter;
        private AICharacterSoundFXManager m_soundFXManager;

        /// <inheritdoc />
        protected override void Awake()
        {
            base.Awake();
            ResolveOwningCharacter();
        }

        /// <inheritdoc />
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            IsDialogueAvailable.OnValueChanged +=
                OnDialogueAvailabilityChanged;
            ResolveOwningCharacter();
            m_soundFXManager?.RegisterDialogueInteractable(this);
            ApplyDialogueAvailability(IsDialogueAvailable.Value);
        }

        /// <inheritdoc />
        public override void OnNetworkDespawn()
        {
            IsDialogueAvailable.OnValueChanged -=
                OnDialogueAvailabilityChanged;
            m_soundFXManager?.CancelCurrentDialogueEvent();
            m_soundFXManager?.UnregisterDialogueInteractable(this);
            base.OnNetworkDespawn();
        }

        /// <inheritdoc />
        public override bool CanInteract(PlayerManager player)
        {
            ResolveOwningCharacter();
            return base.CanInteract(player) &&
                IsDialogueAvailable.Value &&
                m_aiCharacter != null &&
                !m_aiCharacter.IsDead &&
                m_aiCharacter.CurrentTarget == null &&
                m_soundFXManager?.CurrentDialogue != null &&
                PlayerUIManager.Instance?.IsMenuWindowOpen != true;
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            SaveGameAfterInteraction(player);
            m_soundFXManager.PlayCurrentDialogueEvent(player);
        }

        /// <summary>Connects a newly instantiated trigger before network parenting.</summary>
        public void SetOwningCharacter(AICharacterManager aiCharacter)
        {
            m_aiCharacter = aiCharacter;
            m_soundFXManager =
                m_aiCharacter?.GetComponentInChildren<AICharacterSoundFXManager>(
                    true);
        }

        /// <summary>Publishes combat and death availability from the server.</summary>
        public void SetDialogueAvailability(bool isAvailable)
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            if (IsDialogueAvailable.Value != isAvailable)
            {
                IsDialogueAvailable.Value = isAvailable;
            }
            else
            {
                ApplyDialogueAvailability(isAvailable);
            }
        }

        /// <inheritdoc />
        protected override void OnTriggerExit(Collider other)
        {
            base.OnTriggerExit(other);
            PlayerManager player = other.GetComponentInParent<PlayerManager>();
            if (player?.IsOwner == true)
            {
                m_soundFXManager?.CancelCurrentDialogueEvent();
            }
        }

        private void OnTransformParentChanged()
        {
            ResolveOwningCharacter();
            m_soundFXManager?.RegisterDialogueInteractable(this);
        }

        private void OnDialogueAvailabilityChanged(
            bool previousValue,
            bool newValue)
        {
            ApplyDialogueAvailability(newValue);
        }

        private void ApplyDialogueAvailability(bool isAvailable)
        {
            SetInteractionColliderEnabled(isAvailable);
            if (!isAvailable)
            {
                m_soundFXManager?.CancelCurrentDialogueEvent();
            }
        }

        private void ResolveOwningCharacter()
        {
            m_aiCharacter ??= GetComponentInParent<AICharacterManager>();
            m_soundFXManager ??=
                m_aiCharacter?.GetComponentInChildren<AICharacterSoundFXManager>(
                    true);
        }
    }
}
