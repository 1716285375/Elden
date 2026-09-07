using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>Opens an authored animated chest once and persists its host-owned reward.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class TreasureChestInteractable : Interactable
    {
        private static readonly int s_openState = Animator.StringToHash("Base Layer.Open_Chest_01");

        [SerializeField, Min(0)] private int m_worldItemID;
        [SerializeField] private Item m_reward;
        [SerializeField] private Animator m_chestAnimator;

        public readonly NetworkVariable<bool> IsOpened = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Gets the chest's unique world-save key.</summary>
        public int WorldItemID => m_worldItemID;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer && WorldSaveGameManager.Instance?.CurrentCharacterData != null &&
                WorldSaveGameManager.Instance.CurrentCharacterData.TryGetWorldItemLooted(m_worldItemID, out bool opened))
            {
                IsOpened.Value = opened;
            }
            IsOpened.OnValueChanged += OnOpenedChanged;
            ApplyOpenPresentation(IsOpened.Value, true);
        }

        public override void OnNetworkDespawn()
        {
            IsOpened.OnValueChanged -= OnOpenedChanged;
            base.OnNetworkDespawn();
        }

        public override bool CanInteract(PlayerManager player)
        {
            return base.CanInteract(player) && IsSpawned && IsServer && !IsOpened.Value &&
                m_reward != null && !player.IsDead && !player.IsPerformingAction;
        }

        public override void Interact(PlayerManager player)
        {
            if (!CanInteract(player) || !PickupItemInteractable.TryGrantItemToPlayer(player, m_reward))
            {
                return;
            }
            IsOpened.Value = true;
            WorldSaveGameManager.Instance?.CurrentCharacterData?.SetWorldItemLooted(m_worldItemID, true);
            SaveGameAfterInteraction(player);
        }

        public override void CompleteInteraction()
        {
            // Replicated open state disables the trigger only after a successful reward grant.
        }

        private void OnOpenedChanged(bool previousValue, bool currentValue)
        {
            ApplyOpenPresentation(currentValue, false);
        }

        private void ApplyOpenPresentation(bool isOpened, bool isRestoring)
        {
            SetInteractionAvailable(!isOpened);
            if (isOpened && m_chestAnimator != null && m_chestAnimator.HasState(0, s_openState))
            {
                m_chestAnimator.Play(s_openState, 0, isRestoring ? 1f : 0f);
            }
        }
    }
}
