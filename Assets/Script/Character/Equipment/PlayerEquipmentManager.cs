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

        protected override void Awake()
        {
            base.Awake();
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
