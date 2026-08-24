using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    public class PlayerNetworkManager : CharacterNetworkManager
    {
        private const int k_DefaultRightHandWeaponID = 1;
        private const int k_DefaultLeftHandWeaponID = 3;
        private const int k_NoWeaponID = -1;
        private const int k_NoEquipmentID = -1;
        private const int k_NoSpellID = -1;
        private const int k_DefaultMainProjectileID = 12;
        private const int k_DefaultSecondaryProjectileID = 13;
        private const int k_NoProjectileID = -1;

        [Header("Two-Hand Effect")]
        [SerializeField] private StaticCharacterEffect m_twoHandingEffect;

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
        private readonly NetworkVariable<bool> m_isTwoHandingWeapon =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_isTwoHandingRightWeapon =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_isTwoHandingLeftWeapon =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> m_currentWeaponBeingTwoHanded =
            new NetworkVariable<int>(
                k_NoWeaponID,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> m_currentHeadEquipmentID =
            new NetworkVariable<int>(
                k_NoEquipmentID,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> m_currentBodyEquipmentID =
            new NetworkVariable<int>(
                k_NoEquipmentID,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> m_currentHandEquipmentID =
            new NetworkVariable<int>(
                k_NoEquipmentID,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> m_currentLegEquipmentID =
            new NetworkVariable<int>(
                k_NoEquipmentID,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_isMale =
            new NetworkVariable<bool>(
                true,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> m_currentSpellID =
            new NetworkVariable<int>(
                k_NoSpellID,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_isChargingRightSpell =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_isChargingLeftSpell =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_isSpellFullyCharged =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> m_mainProjectileID =
            new NetworkVariable<int>(
                k_DefaultMainProjectileID,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> m_secondaryProjectileID =
            new NetworkVariable<int>(
                k_DefaultSecondaryProjectileID,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> m_currentProjectileID =
            new NetworkVariable<int>(
                k_NoProjectileID,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<ProjectileSlot> m_currentProjectileSlot =
            new NetworkVariable<ProjectileSlot>(
                ProjectileSlot.Main,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_hasArrowNotched =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_isHoldingArrow =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_isAiming =
            new NetworkVariable<bool>(
                false,
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

        /// <summary>Gets whether either equipped weapon is in the replicated two-hand stance.</summary>
        public NetworkVariable<bool> IsTwoHandingWeapon => m_isTwoHandingWeapon;

        /// <summary>Gets whether the right-hand item owns the two-hand stance.</summary>
        public NetworkVariable<bool> IsTwoHandingRightWeapon =>
            m_isTwoHandingRightWeapon;

        /// <summary>Gets whether the left-hand item owns the two-hand stance.</summary>
        public NetworkVariable<bool> IsTwoHandingLeftWeapon =>
            m_isTwoHandingLeftWeapon;

        /// <summary>Gets the item identifier currently presented with two hands.</summary>
        public NetworkVariable<int> CurrentWeaponBeingTwoHanded =>
            m_currentWeaponBeingTwoHanded;

        /// <summary>Gets the owner-written head-equipment identifier.</summary>
        public NetworkVariable<int> CurrentHeadEquipmentID => m_currentHeadEquipmentID;

        /// <summary>Gets the owner-written body-equipment identifier.</summary>
        public NetworkVariable<int> CurrentBodyEquipmentID => m_currentBodyEquipmentID;

        /// <summary>Gets the owner-written hand-equipment identifier.</summary>
        public NetworkVariable<int> CurrentHandEquipmentID => m_currentHandEquipmentID;

        /// <summary>Gets the owner-written leg-equipment identifier.</summary>
        public NetworkVariable<int> CurrentLegEquipmentID => m_currentLegEquipmentID;

        /// <summary>Gets the owner-written body type replicated to every client.</summary>
        public NetworkVariable<bool> IsMale => m_isMale;

        /// <summary>Gets the owner-selected spell occupying the single replicated slot.</summary>
        public NetworkVariable<int> CurrentSpellID => m_currentSpellID;

        /// <summary>Gets whether the owner is charging a right-hand spell.</summary>
        public NetworkVariable<bool> IsChargingRightSpell => m_isChargingRightSpell;

        /// <summary>Gets whether the owner is charging a left-hand spell.</summary>
        public NetworkVariable<bool> IsChargingLeftSpell => m_isChargingLeftSpell;

        /// <summary>Gets whether the active spell reached its full-charge threshold.</summary>
        public NetworkVariable<bool> IsSpellFullyCharged => m_isSpellFullyCharged;

        /// <summary>Gets the owner-selected primary ammunition identifier.</summary>
        public NetworkVariable<int> MainProjectileID => m_mainProjectileID;

        /// <summary>Gets the owner-selected secondary ammunition identifier.</summary>
        public NetworkVariable<int> SecondaryProjectileID => m_secondaryProjectileID;

        /// <summary>Gets the ammunition identifier committed to the current shot.</summary>
        public NetworkVariable<int> CurrentProjectileID => m_currentProjectileID;

        /// <summary>Gets the ammunition slot committed to the current shot.</summary>
        public NetworkVariable<ProjectileSlot> CurrentProjectileSlot =>
            m_currentProjectileSlot;

        /// <summary>Gets whether an arrow model is currently attached to the character.</summary>
        public NetworkVariable<bool> HasArrowNotched => m_hasArrowNotched;

        /// <summary>Gets whether the ranged attack input is still held.</summary>
        public NetworkVariable<bool> IsHoldingArrow => m_isHoldingArrow;

        /// <summary>Gets whether this player is using first-person free aim.</summary>
        public NetworkVariable<bool> IsAiming => m_isAiming;

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
            m_isTwoHandingWeapon.OnValueChanged += OnTwoHandingStateChanged;
            m_isTwoHandingRightWeapon.OnValueChanged += OnTwoHandingStateChanged;
            m_isTwoHandingLeftWeapon.OnValueChanged += OnTwoHandingStateChanged;
            m_currentWeaponBeingTwoHanded.OnValueChanged += OnTwoHandWeaponIDChanged;
            m_currentHeadEquipmentID.OnValueChanged += OnHeadEquipmentIDChanged;
            m_currentBodyEquipmentID.OnValueChanged += OnBodyEquipmentIDChanged;
            m_currentHandEquipmentID.OnValueChanged += OnHandEquipmentIDChanged;
            m_currentLegEquipmentID.OnValueChanged += OnLegEquipmentIDChanged;
            m_isMale.OnValueChanged += OnBodyTypeChanged;
            m_currentSpellID.OnValueChanged += OnCurrentSpellIDChanged;
            m_isChargingRightSpell.OnValueChanged += OnChargingSpellStateChanged;
            m_isChargingLeftSpell.OnValueChanged += OnChargingSpellStateChanged;
            m_isSpellFullyCharged.OnValueChanged += OnSpellFullyChargedChanged;
            m_mainProjectileID.OnValueChanged += OnMainProjectileIDChanged;
            m_secondaryProjectileID.OnValueChanged +=
                OnSecondaryProjectileIDChanged;
            m_hasArrowNotched.OnValueChanged += OnRangedStateChanged;
            m_isHoldingArrow.OnValueChanged += OnRangedStateChanged;
            m_isAiming.OnValueChanged += OnRangedStateChanged;
            GetComponent<PlayerBodyManager>()?.ToggleBodyType(m_isMale.Value);
            m_playerInventoryManager?.InitializeRightWeaponFromID(
                m_currentRightHandWeaponID.Value);
            m_playerInventoryManager?.InitializeLeftWeaponFromID(
                m_currentLeftHandWeaponID.Value);
            m_playerInventoryManager?.InitializeArmorFromIDs(
                m_currentHeadEquipmentID.Value,
                m_currentBodyEquipmentID.Value,
                m_currentHandEquipmentID.Value,
                m_currentLegEquipmentID.Value);
            if (IsOwner &&
                m_currentSpellID.Value == k_NoSpellID &&
                m_playerInventoryManager?.CurrentSpell != null)
            {
                m_currentSpellID.Value =
                    m_playerInventoryManager.CurrentSpell.ItemID;
            }

            m_playerInventoryManager?.InitializeCurrentSpellFromID(
                m_currentSpellID.Value);
            m_playerInventoryManager?.InitializeMainProjectileFromID(
                m_mainProjectileID.Value);
            m_playerInventoryManager?.InitializeSecondaryProjectileFromID(
                m_secondaryProjectileID.Value);
            if (!IsOwner)
            {
                UpdateRemoteAnimatorController(m_currentWeaponIDBeingUsed.Value);
            }

            RefreshBlockingPresentation();
            RefreshTwoHandingPresentation();
            ApplyChargingSpellPresentation();
            ApplyRangedPresentation();

            ResetOwnedSprintState();
            ResetOwnedRangedState();
        }

        public override void OnNetworkDespawn()
        {
            m_currentRightHandWeaponID.OnValueChanged -= OnRightHandWeaponIDChanged;
            m_currentLeftHandWeaponID.OnValueChanged -= OnLeftHandWeaponIDChanged;
            m_currentWeaponIDBeingUsed.OnValueChanged -=
                OnCurrentWeaponIDBeingUsedChanged;
            m_isTwoHandingWeapon.OnValueChanged -= OnTwoHandingStateChanged;
            m_isTwoHandingRightWeapon.OnValueChanged -= OnTwoHandingStateChanged;
            m_isTwoHandingLeftWeapon.OnValueChanged -= OnTwoHandingStateChanged;
            m_currentWeaponBeingTwoHanded.OnValueChanged -= OnTwoHandWeaponIDChanged;
            m_currentHeadEquipmentID.OnValueChanged -= OnHeadEquipmentIDChanged;
            m_currentBodyEquipmentID.OnValueChanged -= OnBodyEquipmentIDChanged;
            m_currentHandEquipmentID.OnValueChanged -= OnHandEquipmentIDChanged;
            m_currentLegEquipmentID.OnValueChanged -= OnLegEquipmentIDChanged;
            m_isMale.OnValueChanged -= OnBodyTypeChanged;
            m_currentSpellID.OnValueChanged -= OnCurrentSpellIDChanged;
            m_isChargingRightSpell.OnValueChanged -= OnChargingSpellStateChanged;
            m_isChargingLeftSpell.OnValueChanged -= OnChargingSpellStateChanged;
            m_isSpellFullyCharged.OnValueChanged -= OnSpellFullyChargedChanged;
            m_mainProjectileID.OnValueChanged -= OnMainProjectileIDChanged;
            m_secondaryProjectileID.OnValueChanged -=
                OnSecondaryProjectileIDChanged;
            m_hasArrowNotched.OnValueChanged -= OnRangedStateChanged;
            m_isHoldingArrow.OnValueChanged -= OnRangedStateChanged;
            m_isAiming.OnValueChanged -= OnRangedStateChanged;
            if (IsOwner)
            {
                PlayerCamera.Instance?.SetAimMode(false, true);
            }
            RemoveTwoHandingPresentation(false);
            base.OnNetworkDespawn();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            ResetOwnedSprintState();
            ResetOwnedTwoHandingState();
            ResetOwnedSpellState();
            ResetOwnedRangedState();
        }

        /// <summary>Ensures the supplied equipped side owns the two-hand stance.</summary>
        public bool EnsureTwoHandWeapon(bool isRightHandWeapon)
        {
            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            bool isRequestedSideActive = m_isTwoHandingWeapon.Value &&
                (isRightHandWeapon
                    ? m_isTwoHandingRightWeapon.Value
                    : m_isTwoHandingLeftWeapon.Value);
            if (!isRequestedSideActive)
            {
                ToggleTwoHandWeapon(isRightHandWeapon);
            }

            return m_isTwoHandingWeapon.Value &&
                (isRightHandWeapon
                    ? m_isTwoHandingRightWeapon.Value
                    : m_isTwoHandingLeftWeapon.Value);
        }

        /// <summary>Writes the owner-authoritative first-person aiming condition.</summary>
        public void SetAimingState(bool isAiming)
        {
            if (IsSpawned && IsOwner && m_isAiming.Value != isAiming)
            {
                m_isAiming.Value = isAiming;
            }
        }

        /// <summary>Commits one ammunition slot and begins its held notch state.</summary>
        public void SetNotchedProjectileState(
            int projectileID,
            ProjectileSlot projectileSlot,
            bool hasArrowNotched,
            bool isHoldingArrow)
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            m_currentProjectileID.Value = hasArrowNotched
                ? projectileID
                : k_NoProjectileID;
            m_currentProjectileSlot.Value = projectileSlot;
            m_hasArrowNotched.Value = hasArrowNotched;
            m_isHoldingArrow.Value = hasArrowNotched && isHoldingArrow;
        }

        /// <summary>Releases held input while preserving the notched arrow until its event.</summary>
        public void SetHoldingArrowState(bool isHoldingArrow)
        {
            if (IsSpawned &&
                IsOwner &&
                m_hasArrowNotched.Value &&
                m_isHoldingArrow.Value != isHoldingArrow)
            {
                m_isHoldingArrow.Value = isHoldingArrow;
            }
        }

        /// <summary>Replicates a notch event without continuously synchronizing its model.</summary>
        [ServerRpc]
        public void NotifyServerOfNotchProjectileServerRpc(
            int projectileID,
            ProjectileSlot projectileSlot,
            ServerRpcParams serverRpcParams = default)
        {
            if (serverRpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            PresentNotchProjectileClientRpc(
                projectileID,
                projectileSlot,
                serverRpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void PresentNotchProjectileClientRpc(
            int projectileID,
            ProjectileSlot projectileSlot,
            ulong shooterClientID)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LocalClientId == shooterClientID)
            {
                return;
            }

            GetComponent<PlayerCombatManager>()
                ?.PerformNotchingProjectileFromRpc(
                    projectileID,
                    projectileSlot);
        }

        /// <summary>Replicates one projectile release event and its fire-frame orientation.</summary>
        [ServerRpc]
        public void NotifyServerOfReleaseProjectileServerRpc(
            int projectileID,
            Vector3 aimDirection,
            float characterYRotation,
            ServerRpcParams serverRpcParams = default)
        {
            if (serverRpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            PresentReleaseProjectileClientRpc(
                projectileID,
                aimDirection,
                characterYRotation,
                serverRpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void PresentReleaseProjectileClientRpc(
            int projectileID,
            Vector3 aimDirection,
            float characterYRotation,
            ulong shooterClientID)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LocalClientId == shooterClientID)
            {
                return;
            }

            GetComponent<PlayerCombatManager>()
                ?.PerformReleaseProjectileFromRpc(
                    projectileID,
                    aimDirection,
                    characterYRotation);
        }

        /// <summary>Writes one hand's sustained spell charge only when its value changes.</summary>
        public void SetChargingSpellState(bool isRightHand, bool isCharging)
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            NetworkVariable<bool> selectedState = isRightHand
                ? m_isChargingRightSpell
                : m_isChargingLeftSpell;
            NetworkVariable<bool> oppositeState = isRightHand
                ? m_isChargingLeftSpell
                : m_isChargingRightSpell;
            if (oppositeState.Value)
            {
                oppositeState.Value = false;
            }

            if (selectedState.Value != isCharging)
            {
                selectedState.Value = isCharging;
            }
        }

        /// <summary>Writes the one-shot full-charge condition when its value changes.</summary>
        public void SetSpellFullyChargedState(bool isFullyCharged)
        {
            if (IsSpawned &&
                IsOwner &&
                m_isSpellFullyCharged.Value != isFullyCharged)
            {
                m_isSpellFullyCharged.Value = isFullyCharged;
            }
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

        /// <summary>Changes the locally owned body type and rebuilds equipped models.</summary>
        public void SetBodyType(bool isMale)
        {
            if (IsSpawned && IsOwner)
            {
                m_isMale.Value = isMale;
            }
        }

        /// <summary>Replays synchronized body type and armor for late-joining clients.</summary>
        public void RefreshArmorPresentation()
        {
            GetComponent<PlayerBodyManager>()?.ToggleBodyType(m_isMale.Value);
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.InitializeArmorFromIDs(
                m_currentHeadEquipmentID.Value,
                m_currentBodyEquipmentID.Value,
                m_currentHandEquipmentID.Value,
                m_currentLegEquipmentID.Value);
            GetComponent<PlayerEquipmentManager>()?.RefreshArmorPresentation(m_isMale.Value);
        }

        /// <summary>Toggles the requested equipped side into or out of the two-hand stance.</summary>
        public void ToggleTwoHandWeapon(bool isRightHandWeapon)
        {
            if (!IsSpawned || !IsOwner || m_playerInventoryManager == null)
            {
                return;
            }

            WeaponItem weapon = isRightHandWeapon
                ? m_playerInventoryManager.CurrentRightHandWeapon
                : m_playerInventoryManager.CurrentLeftHandWeapon;
            if (weapon == null || weapon.IsUnarmed)
            {
                ClearTwoHandingState();
                return;
            }

            bool isSameSideActive = m_isTwoHandingWeapon.Value &&
                (isRightHandWeapon
                    ? m_isTwoHandingRightWeapon.Value
                    : m_isTwoHandingLeftWeapon.Value);
            if (isSameSideActive)
            {
                ClearTwoHandingState();
                return;
            }

            m_currentWeaponBeingTwoHanded.Value = weapon.ItemID;
            m_isTwoHandingRightWeapon.Value = isRightHandWeapon;
            m_isTwoHandingLeftWeapon.Value = !isRightHandWeapon;
            m_isTwoHandingWeapon.Value = true;
            SetCharacterActionHand(isRightHandWeapon);
        }

        private void ResetOwnedSprintState()
        {
            if (IsOwner && IsSpawned)
            {
                IsSprinting.Value = false;
            }
        }

        private void ResetOwnedTwoHandingState()
        {
            if (IsOwner && IsSpawned)
            {
                ClearTwoHandingState();
            }
        }

        private void ResetOwnedSpellState()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            m_isChargingRightSpell.Value = false;
            m_isChargingLeftSpell.Value = false;
            m_isSpellFullyCharged.Value = false;
        }

        private void ResetOwnedRangedState()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            m_currentProjectileID.Value = k_NoProjectileID;
            m_hasArrowNotched.Value = false;
            m_isHoldingArrow.Value = false;
            m_isAiming.Value = false;
        }

        private void OnMainProjectileIDChanged(
            int previousProjectileID,
            int currentProjectileID)
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.InitializeMainProjectileFromID(
                currentProjectileID);
        }

        private void OnSecondaryProjectileIDChanged(
            int previousProjectileID,
            int currentProjectileID)
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.InitializeSecondaryProjectileFromID(
                currentProjectileID);
        }

        private void OnRangedStateChanged(bool previousValue, bool currentValue)
        {
            ApplyRangedPresentation();
        }

        private void ApplyRangedPresentation()
        {
            PlayerManager player = GetComponent<PlayerManager>();
            player?.PlayerAnimatorManager?.SetRangedWeaponState(
                m_hasArrowNotched.Value,
                m_isHoldingArrow.Value,
                m_isAiming.Value);
            player?.EquipmentManager?.SetRangedWeaponState(
                m_hasArrowNotched.Value,
                m_isHoldingArrow.Value);
            if (!m_hasArrowNotched.Value)
            {
                player?.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            }

            player?.LocomotionManager?.SetCanRun(
                !m_hasArrowNotched.Value && !m_isAiming.Value);

            if (IsOwner)
            {
                PlayerCamera.Instance?.SetAimMode(m_isAiming.Value);
            }
        }

        private void OnCurrentSpellIDChanged(int previousSpellID, int currentSpellID)
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.InitializeCurrentSpellFromID(currentSpellID);
        }

        private void OnChargingSpellStateChanged(
            bool previousValue,
            bool currentValue)
        {
            ApplyChargingSpellPresentation();
        }

        private void ApplyChargingSpellPresentation()
        {
            PlayerManager player = GetComponent<PlayerManager>();
            player?.PlayerAnimatorManager?.SetSpellChargingState(
                m_isChargingRightSpell.Value,
                m_isChargingLeftSpell.Value);
            if (!m_isChargingRightSpell.Value && !m_isChargingLeftSpell.Value)
            {
                player?.CharacterEffectsManager?.DestroyAllCurrentActionEffects();
            }
        }

        private void OnSpellFullyChargedChanged(
            bool previousValue,
            bool currentValue)
        {
            PlayerManager player = GetComponent<PlayerManager>();
            player?.PlayerAnimatorManager?.SetSpellFullyChargedState(currentValue);
            if (!currentValue)
            {
                return;
            }

            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            CasterWeaponItem casterWeapon = (m_isUsingLeftHand.Value
                ? m_playerInventoryManager?.CurrentLeftHandWeapon
                : m_playerInventoryManager?.CurrentRightHandWeapon) as
                    CasterWeaponItem;
            m_playerInventoryManager?.CurrentSpell?.SuccessfullyChargeSpell(
                player,
                casterWeapon,
                m_isUsingLeftHand.Value == false);
        }

        private void OnRightHandWeaponIDChanged(int previousWeaponID, int currentWeaponID)
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.EquipRightWeaponFromID(currentWeaponID);
            RefreshChangedTwoHandWeapon(currentWeaponID, true);
        }

        private void OnLeftHandWeaponIDChanged(int previousWeaponID, int currentWeaponID)
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.EquipLeftWeaponFromID(currentWeaponID);
            RefreshChangedTwoHandWeapon(currentWeaponID, false);
        }

        private void OnHeadEquipmentIDChanged(int previousItemID, int currentItemID)
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.EquipHeadEquipmentFromID(currentItemID);
        }

        private void OnBodyEquipmentIDChanged(int previousItemID, int currentItemID)
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.EquipBodyEquipmentFromID(currentItemID);
        }

        private void OnHandEquipmentIDChanged(int previousItemID, int currentItemID)
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.EquipHandEquipmentFromID(currentItemID);
        }

        private void OnLegEquipmentIDChanged(int previousItemID, int currentItemID)
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.EquipLegEquipmentFromID(currentItemID);
        }

        private void OnBodyTypeChanged(bool previousIsMale, bool currentIsMale)
        {
            GetComponent<PlayerEquipmentManager>()?.RefreshArmorPresentation(currentIsMale);
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

        private void OnTwoHandingStateChanged(bool previousValue, bool currentValue)
        {
            RefreshTwoHandingPresentation();
        }

        private void OnTwoHandWeaponIDChanged(int previousWeaponID, int currentWeaponID)
        {
            RefreshTwoHandingPresentation();
        }

        /// <summary>
        /// Replays synchronized two-hand state after equipment initialization or late join.
        /// </summary>
        public void RefreshTwoHandingPresentation()
        {
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            PlayerManager player = GetComponent<PlayerManager>();
            bool hasExactlyOneSide = m_isTwoHandingRightWeapon.Value !=
                m_isTwoHandingLeftWeapon.Value;
            WeaponItem weapon = m_isTwoHandingRightWeapon.Value
                ? m_playerInventoryManager?.CurrentRightHandWeapon
                : m_isTwoHandingLeftWeapon.Value
                    ? m_playerInventoryManager?.CurrentLeftHandWeapon
                    : null;
            if (!m_isTwoHandingWeapon.Value ||
                !hasExactlyOneSide ||
                weapon == null ||
                weapon.IsUnarmed ||
                weapon.ItemID != m_currentWeaponBeingTwoHanded.Value)
            {
                RemoveTwoHandingPresentation();
                return;
            }

            m_playerInventoryManager.SetCurrentTwoHandWeapon(weapon);
            if (m_isTwoHandingRightWeapon.Value)
            {
                player?.EquipmentManager?.TwoHandRightWeapon();
            }
            else
            {
                player?.EquipmentManager?.TwoHandLeftWeapon();
            }

            player?.PlayerAnimatorManager?.UpdateAnimatorController(weapon);
            player?.PlayerAnimatorManager?.SetTwoHandingWeaponState(true);
            player?.CharacterEffectsManager?.ProcessStaticEffect(m_twoHandingEffect);
            if (player?.CharacterNetworkManager?.IsBlocking.Value == true)
            {
                player.PlayerStatsManager?.SetBlockingStats(weapon);
            }
        }

        private void RemoveTwoHandingPresentation(bool restoreEquipment = true)
        {
            PlayerManager player = GetComponent<PlayerManager>();
            m_playerInventoryManager ??= GetComponent<PlayerInventoryManager>();
            m_playerInventoryManager?.ClearCurrentTwoHandWeapon();
            player?.PlayerAnimatorManager?.SetTwoHandingWeaponState(false);
            if (m_twoHandingEffect != null)
            {
                player?.CharacterEffectsManager?.RemoveStaticEffect(
                    m_twoHandingEffect.StaticEffectID);
            }

            if (restoreEquipment)
            {
                player?.EquipmentManager?.UnTwoHandWeapon();
            }
        }

        private void ClearTwoHandingState()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            m_isTwoHandingWeapon.Value = false;
            m_isTwoHandingRightWeapon.Value = false;
            m_isTwoHandingLeftWeapon.Value = false;
            m_currentWeaponBeingTwoHanded.Value = k_NoWeaponID;
            SetCharacterActionHand(true);
        }

        private void RefreshChangedTwoHandWeapon(int currentWeaponID, bool isRightHand)
        {
            if (!m_isTwoHandingWeapon.Value ||
                (isRightHand && !m_isTwoHandingRightWeapon.Value) ||
                (!isRightHand && !m_isTwoHandingLeftWeapon.Value))
            {
                return;
            }

            if (IsOwner)
            {
                WeaponItem weapon = m_playerInventoryManager?.GetEquippedWeaponByID(
                    currentWeaponID);
                if (weapon == null || weapon.IsUnarmed)
                {
                    ClearTwoHandingState();
                    return;
                }

                m_currentWeaponBeingTwoHanded.Value = currentWeaponID;
            }

            RefreshTwoHandingPresentation();
        }
    }
}
