using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Opens a level-design shortcut only from its authored far side and replicates presentation.
    /// </summary>
    public class DungeonOneWayGate : Interactable
    {
        private readonly NetworkVariable<bool> m_isOpen = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        [Header("GATE")]
        [SerializeField] private Transform m_gateVisual;
        [SerializeField] private Collider m_blockingCollider;
        [SerializeField] private Vector3 m_openLocalOffset = new(0f, 5f, 0f);
        [SerializeField] private bool m_allowedFromPositiveForwardSide = true;

        private Vector3 m_closedLocalPosition;

        /// <summary>Gets the server-owned replicated open state.</summary>
        public NetworkVariable<bool> IsOpen => m_isOpen;

        protected override void Awake()
        {
            base.Awake();
            m_gateVisual ??= transform;
            m_blockingCollider ??= GetComponent<Collider>();
            m_closedLocalPosition = m_gateVisual.localPosition;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_isOpen.OnValueChanged += OnOpenStateChanged;
            ApplyOpenState(m_isOpen.Value);
        }

        public override void OnNetworkDespawn()
        {
            m_isOpen.OnValueChanged -= OnOpenStateChanged;
            base.OnNetworkDespawn();
        }

        /// <inheritdoc />
        public override bool CanInteract(PlayerManager player)
        {
            if (!base.CanInteract(player) || m_isOpen.Value)
            {
                return false;
            }

            SetInteractableText(IsPlayerOnAllowedSide(player)
                ? "Open gate"
                : "Locked from the other side");
            return true;
        }

        /// <inheritdoc />
        public override void Interact(PlayerManager player)
        {
            if (!IsServer || m_isOpen.Value || !IsPlayerOnAllowedSide(player))
            {
                return;
            }

            m_isOpen.Value = true;
            ApplyOpenState(true);
            SaveGameAfterInteraction(player);
        }

        /// <summary>Returns whether one position lies on the authored opening side.</summary>
        public static bool IsOnAllowedSide(
            Vector3 gatePosition,
            Vector3 gateForward,
            Vector3 playerPosition,
            bool allowedFromPositiveForwardSide)
        {
            float side = Vector3.Dot(
                gateForward.normalized,
                playerPosition - gatePosition);
            return allowedFromPositiveForwardSide ? side > 0f : side < 0f;
        }

        private bool IsPlayerOnAllowedSide(PlayerManager player)
        {
            return player != null && IsOnAllowedSide(
                transform.position,
                transform.forward,
                player.transform.position,
                m_allowedFromPositiveForwardSide);
        }

        private void OnOpenStateChanged(bool previousIsOpen, bool currentIsOpen)
        {
            ApplyOpenState(currentIsOpen);
        }

        private void ApplyOpenState(bool isOpen)
        {
            if (m_gateVisual != null)
            {
                m_gateVisual.localPosition = m_closedLocalPosition +
                    (isOpen ? m_openLocalOffset : Vector3.zero);
            }

            if (m_blockingCollider != null)
            {
                m_blockingCollider.enabled = !isOpen;
            }

            SetInteractionColliderEnabled(!isOpen);
        }
    }
}
