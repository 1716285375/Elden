using Unity.Collections;
using Unity.Netcode;

namespace ZZ
{
    public class PlayerNetworkManager : CharacterNetworkManager
    {
        private const int k_DefaultRightHandWeaponID = 1;
        private const int k_DefaultLeftHandWeaponID = 3;

        private readonly NetworkVariable<FixedString64Bytes> m_characterName =
            new NetworkVariable<FixedString64Bytes>(
                new FixedString64Bytes("Unnamed"),
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> m_currentRightHandWeaponID =
            new NetworkVariable<int>(
                k_DefaultRightHandWeaponID,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> m_currentLeftHandWeaponID =
            new NetworkVariable<int>(
                k_DefaultLeftHandWeaponID,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_isUsingRightHand =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_isUsingLeftHand =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> m_currentWeaponIDBeingUsed =
            new NetworkVariable<int>(
                k_DefaultRightHandWeaponID,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        private PlayerInventoryManager m_playerInventoryManager;

        /// <summary>
        /// Gets the owner-written character name replicated to every client.
        /// </summary>
        public NetworkVariable<FixedString64Bytes> CharacterName => m_characterName;

        /// <summary>
        /// Gets the owner-written right-hand weapon identifier replicated to every client.
        /// </summary>
        public NetworkVariable<int> CurrentRightHandWeaponID => m_currentRightHandWeaponID;

        /// <summary>
        /// Gets the owner-written left-hand weapon identifier replicated to every client.
        /// </summary>
        public NetworkVariable<int> CurrentLeftHandWeaponID => m_currentLeftHandWeaponID;

        /// <summary>Gets whether the owner is currently using the right hand for actions.</summary>
        public NetworkVariable<bool> IsUsingRightHand => m_isUsingRightHand;

        /// <summary>Gets whether the owner is currently using the left hand for actions.</summary>
        public NetworkVariable<bool> IsUsingLeftHand => m_isUsingLeftHand;

        /// <summary>Gets the equipped weapon driving the current action animation set.</summary>
        public NetworkVariable<int> CurrentWeaponIDBeingUsed =>
            m_currentWeaponIDBeingUsed;

        public NetworkVariable<bool> IsSprinting = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_currentRightHandWeaponID.OnValueChanged += OnRightHandWeaponIDChanged;
            m_currentLeftHandWeaponID.OnValueChanged += OnLeftHandWeaponIDChanged;
            m_currentWeaponIDBeingUsed.OnValueChanged +=
                OnCurrentWeaponIDBeingUsedChanged;
            m_playerInventoryManager?.InitializeRightWeaponFromID(
                m_currentRightHandWeaponID.Value);
            m_playerInventoryManager?.InitializeLeftWeaponFromID(
                m_currentLeftHandWeaponID.Value);
            if (!IsOwner)
            {
                UpdateRemoteAnimatorController(m_currentWeaponIDBeingUsed.Value);
            }

            RefreshBlockingPresentation();

            ResetOwnedSprintState();
        }

        public override void OnNetworkDespawn()
        {
            m_currentRightHandWeaponID.OnValueChanged -= OnRightHandWeaponIDChanged;
            m_currentLeftHandWeaponID.OnValueChanged -= OnLeftHandWeaponIDChanged;
            m_currentWeaponIDBeingUsed.OnValueChanged -=
                OnCurrentWeaponIDBeingUsedChanged;
            base.OnNetworkDespawn();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            ResetOwnedSprintState();
        }

        /// <summary>
        /// Sets which hand the owner is currently using for weapon actions.
        /// </summary>
        public void SetCharacterActionHand(bool isRightHandAction)
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            m_isUsingRightHand.Value = isRightHandAction;
            m_isUsingLeftHand.Value = !isRightHandAction;
            m_currentWeaponIDBeingUsed.Value = isRightHandAction
                ? m_currentRightHandWeaponID.Value
                : m_currentLeftHandWeaponID.Value;
        }

        private void ResetOwnedSprintState()
        {
            if (IsOwner && IsSpawned)
            {
                IsSprinting.Value = false;
            }
        }

        private void OnRightHandWeaponIDChanged(int previousWeaponID, int currentWeaponID)
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.EquipRightWeaponFromID(currentWeaponID);
        }

        private void OnLeftHandWeaponIDChanged(int previousWeaponID, int currentWeaponID)
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.EquipLeftWeaponFromID(currentWeaponID);
        }

        private void OnCurrentWeaponIDBeingUsedChanged(
            int previousWeaponID,
            int currentWeaponID)
        {
            if (!IsOwner)
            {
                UpdateRemoteAnimatorController(currentWeaponID);
            }
        }

        private void UpdateRemoteAnimatorController(int weaponID)
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            WeaponItem weapon = m_playerInventoryManager
                ?.GetEquippedWeaponByID(weaponID);
            GetComponent<PlayerManager>()?.PlayerAnimatorManager
                ?.UpdateAnimatorController(weapon);
        }
    }
}
