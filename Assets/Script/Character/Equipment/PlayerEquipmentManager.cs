using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Discovers player hand slots and presents the weapons selected by inventory state.
    /// </summary>
    [RequireComponent(typeof(PlayerManager))]
    public class PlayerEquipmentManager : CharacterEquipmentManager
    {
        private WeaponModelInstantiationSlot m_rightHandSlot;
        private WeaponModelInstantiationSlot m_leftHandWeaponSlot;
        private WeaponModelInstantiationSlot m_leftHandShieldSlot;
        private WeaponModelInstantiationSlot m_backSlot;
        private WeaponModelInstantiationSlot m_hipSlot;
        private PlayerManager m_player;
        private CharacterSoundFXManager m_characterSoundFXManager;

        /// <summary>Gets the weapon manager of the currently loaded right-hand weapon model.</summary>
        public WeaponManager CurrentRightHandWeaponManager { get; private set; }

        /// <summary>Gets the weapon manager of the currently loaded left-hand weapon model.</summary>
        public WeaponManager CurrentLeftHandWeaponManager { get; private set; }

        /// <summary>Gets the weapon manager instantiated in the animation-compatible right hand.</summary>
        public WeaponManager CurrentTwoHandWeaponManager { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponent<PlayerManager>();
            m_characterSoundFXManager =
                GetComponentInChildren<CharacterSoundFXManager>(true);
            DiscoverWeaponSlots();
        }

        /// <summary>
        /// Loads both currently selected inventory weapons into their independent hand slots.
        /// </summary>
        public void LoadWeaponsOnBothHands()
        {
            PlayerInventoryManager inventoryManager = GetComponent<PlayerInventoryManager>();
            LoadRightWeapon(inventoryManager?.CurrentRightHandWeapon);
            LoadLeftWeapon(inventoryManager?.CurrentLeftHandWeapon);
        }

        /// <summary>
        /// Loads the selected weapon into the right-hand model slot.
        /// </summary>
        public void LoadRightWeapon(WeaponItem weapon)
        {
            if (IsTwoHanding())
            {
                RefreshTwoHandingPresentation();
                return;
            }

            LoadRightWeaponInHand(weapon);
        }

        private void LoadRightWeaponInHand(WeaponItem weapon)
        {
            if (m_rightHandSlot == null)
            {
                Debug.LogError("The player prefab is missing a right-hand weapon slot.", this);
                return;
            }

            m_rightHandSlot.LoadWeaponModel(weapon, Character);
            CurrentRightHandWeaponManager = m_rightHandSlot.CurrentWeaponManager;
            if (m_player?.CharacterNetworkManager?.IsBlocking.Value != true)
            {
                m_player?.PlayerAnimatorManager?.UpdateAnimatorController(weapon);
            }
        }

        /// <summary>
        /// Loads the selected weapon into the left-hand model slot.
        /// </summary>
        public void LoadLeftWeapon(WeaponItem weapon)
        {
            if (IsTwoHanding())
            {
                RefreshTwoHandingPresentation();
                return;
            }

            LoadLeftWeaponInHand(weapon);
        }

        private void LoadLeftWeaponInHand(WeaponItem weapon)
        {
            WeaponModelInstantiationSlot targetSlot = weapon?.WeaponModelType ==
                    WeaponModelType.Shield
                ? m_leftHandShieldSlot
                : m_leftHandWeaponSlot;
            if (targetSlot == null)
            {
                Debug.LogError(
                    $"The player prefab is missing the {weapon?.WeaponModelType} left-hand slot.",
                    this);
                return;
            }

            m_leftHandWeaponSlot?.UnloadWeaponModel();
            m_leftHandShieldSlot?.UnloadWeaponModel();
            targetSlot.LoadWeaponModel(weapon, Character);
            CurrentLeftHandWeaponManager = targetSlot.CurrentWeaponManager;
            if (m_player?.PlayerNetworkManager?.IsUsingLeftHand.Value == true ||
                m_player?.CharacterNetworkManager?.IsBlocking.Value == true)
            {
                m_player.PlayerAnimatorManager?.UpdateAnimatorController(weapon);
                m_player.PlayerStatsManager?.SetBlockingStats(weapon);
            }
        }

        /// <summary>Presents the right-hand weapon in hand and stores the left-hand model.</summary>
        public void TwoHandRightWeapon()
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            if (inventory == null)
            {
                return;
            }

            UnloadAllWeaponSlots();
            LoadRightWeaponModel(inventory.CurrentRightHandWeapon);
            PlaceWeaponModelInUnequippedSlot(inventory.CurrentLeftHandWeapon);
            CurrentTwoHandWeaponManager = m_rightHandSlot?.CurrentWeaponManager;
            CurrentTwoHandWeaponManager?.SetWeaponDamage();
        }

        /// <summary>Presents the left weapon in the right hand and stores the right-hand model.</summary>
        public void TwoHandLeftWeapon()
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            if (inventory == null)
            {
                return;
            }

            UnloadAllWeaponSlots();
            LoadRightWeaponModel(inventory.CurrentLeftHandWeapon);
            PlaceWeaponModelInUnequippedSlot(inventory.CurrentRightHandWeapon);
            CurrentTwoHandWeaponManager = m_rightHandSlot?.CurrentWeaponManager;
            CurrentTwoHandWeaponManager?.SetWeaponDamage();
        }

        /// <summary>Restores both normal hand slots after leaving the two-hand stance.</summary>
        public void UnTwoHandWeapon()
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            UnloadAllWeaponSlots();
            CurrentTwoHandWeaponManager = null;
            if (inventory == null)
            {
                return;
            }

            LoadRightWeaponInHand(inventory.CurrentRightHandWeapon);
            LoadLeftWeaponInHand(inventory.CurrentLeftHandWeapon);
            CurrentRightHandWeaponManager?.SetWeaponDamage();
            CurrentLeftHandWeaponManager?.SetWeaponDamage();
            if (m_player?.CharacterNetworkManager?.IsBlocking.Value != true)
            {
                m_player?.PlayerAnimatorManager?.UpdateAnimatorController(
                    inventory.CurrentRightHandWeapon);
            }
        }

        /// <summary>Loads an unequipped weapon into the back or hip slot selected by class.</summary>
        public void PlaceWeaponModelInUnequippedSlot(WeaponItem weapon)
        {
            if (weapon == null || weapon.IsUnarmed)
            {
                return;
            }

            WeaponModelInstantiationSlot targetSlot = weapon.WeaponClass == WeaponClass.Dagger
                ? m_hipSlot
                : m_backSlot;
            if (targetSlot == null)
            {
                Debug.LogError(
                    $"The player prefab is missing a storage slot for {weapon.WeaponClass}.",
                    this);
                return;
            }

            GetUnequippedPlacement(
                weapon.WeaponClass,
                out Vector3 localPosition,
                out Vector3 localEulerRotation);
            targetSlot.LoadWeaponModelAtPlacement(
                weapon,
                Character,
                localPosition,
                localEulerRotation,
                weapon.WeaponPivotScale);
        }

        /// <summary>Replays the synchronized side selection after equipment arrives.</summary>
        public void RefreshTwoHandingPresentation()
        {
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (networkManager?.IsTwoHandingRightWeapon.Value == true)
            {
                TwoHandRightWeapon();
            }
            else if (networkManager?.IsTwoHandingLeftWeapon.Value == true)
            {
                TwoHandLeftWeapon();
            }
        }

        /// <summary>
        /// Enables the current action-hand weapon's damage window on the locally owned player.
        /// </summary>
        public void OpenDamageCollider()
        {
            WeaponManager weaponManager = GetCurrentWeaponManager();
            m_characterSoundFXManager?.PlayWeaponWhoosh(weaponManager?.Weapon);
            if (m_player == null || !m_player.IsOwner)
            {
                return;
            }

            if (weaponManager == null)
            {
                return;
            }

            weaponManager.SetAttackType(m_player.PlayerCombatManager.CurrentAttackType);
            weaponManager.OpenDamageCollider();
        }

        /// <summary>
        /// Ends the current action-hand weapon's damage window.
        /// </summary>
        public void CloseDamageCollider()
        {
            GetCurrentWeaponManager()?.CloseDamageCollider();
        }

        private WeaponManager GetCurrentWeaponManager()
        {
            if (IsTwoHanding())
            {
                return CurrentTwoHandWeaponManager;
            }

            bool isUsingRightHand = m_player?.PlayerNetworkManager == null ||
                m_player.PlayerNetworkManager.IsUsingRightHand.Value;
            return isUsingRightHand
                ? CurrentRightHandWeaponManager
                : CurrentLeftHandWeaponManager;
        }

        private void DiscoverWeaponSlots()
        {
            foreach (WeaponModelInstantiationSlot slot in
                     GetComponentsInChildren<WeaponModelInstantiationSlot>(true))
            {
                if (slot.WeaponModelSlot == WeaponModelSlot.RightHandSlot)
                {
                    m_rightHandSlot = slot;
                }
                else if (slot.WeaponModelSlot == WeaponModelSlot.LeftHandSlot)
                {
                    m_leftHandWeaponSlot = slot;
                }
                else if (slot.WeaponModelSlot == WeaponModelSlot.LeftHandShieldSlot)
                {
                    m_leftHandShieldSlot = slot;
                }
                else if (slot.WeaponModelSlot == WeaponModelSlot.BackSlot)
                {
                    m_backSlot = slot;
                }
                else if (slot.WeaponModelSlot == WeaponModelSlot.HipSlot)
                {
                    m_hipSlot = slot;
                }
            }
        }

        private void LoadRightWeaponModel(WeaponItem weapon)
        {
            if (m_rightHandSlot == null || weapon == null)
            {
                return;
            }

            m_rightHandSlot.LoadWeaponModel(weapon, Character);
            CurrentRightHandWeaponManager = m_rightHandSlot.CurrentWeaponManager;
        }

        private void UnloadAllWeaponSlots()
        {
            m_rightHandSlot?.UnloadWeaponModel();
            m_leftHandWeaponSlot?.UnloadWeaponModel();
            m_leftHandShieldSlot?.UnloadWeaponModel();
            m_backSlot?.UnloadWeaponModel();
            m_hipSlot?.UnloadWeaponModel();
            CurrentRightHandWeaponManager = null;
            CurrentLeftHandWeaponManager = null;
        }

        private bool IsTwoHanding()
        {
            return m_player?.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true;
        }

        private static void GetUnequippedPlacement(
            WeaponClass weaponClass,
            out Vector3 localPosition,
            out Vector3 localEulerRotation)
        {
            if (weaponClass == WeaponClass.Dagger)
            {
                localPosition = new Vector3(0.12f, -0.08f, 0.04f);
                localEulerRotation = new Vector3(10f, 0f, 185f);
                return;
            }

            localPosition = weaponClass == WeaponClass.Shield
                ? new Vector3(0f, 0.02f, -0.14f)
                : new Vector3(0.12f, 0.03f, -0.08f);
            localEulerRotation = weaponClass == WeaponClass.Shield
                ? new Vector3(0f, 180f, 0f)
                : new Vector3(0f, 45f, 90f);
        }
    }
}
