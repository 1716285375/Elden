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
        private WeaponModelInstantiationSlot m_leftHandSlot;
        private PlayerManager m_player;
        private CharacterSoundFXManager m_characterSoundFXManager;

        /// <summary>Gets the weapon manager of the currently loaded right-hand weapon model.</summary>
        public WeaponManager CurrentRightHandWeaponManager { get; private set; }

        /// <summary>Gets the weapon manager of the currently loaded left-hand weapon model.</summary>
        public WeaponManager CurrentLeftHandWeaponManager { get; private set; }

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
            if (m_rightHandSlot == null)
            {
                Debug.LogError("The player prefab is missing a right-hand weapon slot.", this);
                return;
            }

            m_rightHandSlot.LoadWeaponModel(weapon, Character);
            CurrentRightHandWeaponManager = m_rightHandSlot.CurrentWeaponManager;
        }

        /// <summary>
        /// Loads the selected weapon into the left-hand model slot.
        /// </summary>
        public void LoadLeftWeapon(WeaponItem weapon)
        {
            if (m_leftHandSlot == null)
            {
                Debug.LogError("The player prefab is missing a left-hand weapon slot.", this);
                return;
            }

            m_leftHandSlot.LoadWeaponModel(weapon, Character);
            CurrentLeftHandWeaponManager = m_leftHandSlot.CurrentWeaponManager;
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
                    m_leftHandSlot = slot;
                }
            }
        }
    }
}
