using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    public class PlayerUIHUDManager : MonoBehaviour
    {
        [SerializeField] private UIStatBar m_healthBar;
        [SerializeField] private UIStatBar m_staminaBar;
        [SerializeField] private UIQuickSlot m_leftWeaponQuickSlot;
        [SerializeField] private UIQuickSlot m_rightWeaponQuickSlot;
        [SerializeField] private UIQuickSlot m_spellQuickSlot;
        [SerializeField] private UIQuickSlot m_itemQuickSlot;

        private CharacterNetworkManager m_boundNetworkManager;
        private PlayerInventoryManager m_boundInventoryManager;

        /// <summary>
        /// Updates the local Health presentation from shared character state.
        /// </summary>
        public void SetNewHealthValue(float currentHealth)
        {
            m_healthBar?.SetStat(currentHealth);
        }

        /// <summary>
        /// Updates the local Health range from shared character state.
        /// </summary>
        public void SetMaxHealthValue(float maximumHealth)
        {
            m_healthBar?.SetMaxStat(maximumHealth);
            RefreshHUD();
        }

        /// <summary>
        /// Updates the local Stamina presentation from shared character state.
        /// </summary>
        public void SetNewStaminaValue(float currentStamina)
        {
            m_staminaBar?.SetStat(currentStamina);
        }

        /// <summary>
        /// Updates the local Stamina range from shared character state.
        /// </summary>
        public void SetMaxStaminaValue(float maximumStamina)
        {
            m_staminaBar?.SetMaxStat(maximumStamina);
            RefreshHUD();
        }

        /// <summary>
        /// Subscribes the HUD to one locally owned character and initializes its resources.
        /// </summary>
        public void BindStats(CharacterNetworkManager networkManager)
        {
            if (networkManager == null)
            {
                return;
            }

            if (m_boundNetworkManager != networkManager)
            {
                UnbindCurrentStats();
                m_boundNetworkManager = networkManager;
                m_boundNetworkManager.CurrentHealth.OnValueChanged += OnCurrentHealthChanged;
                m_boundNetworkManager.MaxHealth.OnValueChanged += OnMaxHealthChanged;
                m_boundNetworkManager.CurrentStamina.OnValueChanged += OnCurrentStaminaChanged;
                m_boundNetworkManager.MaxStamina.OnValueChanged += OnMaxStaminaChanged;
            }

            gameObject.SetActive(true);
            RefreshStatBars();
        }

        /// <summary>
        /// Removes the binding only when the supplied character still owns this HUD connection.
        /// </summary>
        public void UnbindStats(CharacterNetworkManager networkManager)
        {
            if (m_boundNetworkManager != networkManager)
            {
                return;
            }

            UnbindCurrentStats();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Binds the reusable quick slots to the locally owned player's equipped weapons.
        /// </summary>
        public void BindQuickSlots(PlayerInventoryManager inventoryManager)
        {
            if (inventoryManager == null)
            {
                return;
            }

            if (m_boundInventoryManager == inventoryManager)
            {
                return;
            }

            UnbindCurrentQuickSlots();
            m_boundInventoryManager = inventoryManager;
            m_boundInventoryManager.RightHandWeaponChanged +=
                OnRightHandWeaponChanged;
            m_boundInventoryManager.LeftHandWeaponChanged +=
                OnLeftHandWeaponChanged;
            RefreshQuickSlots();
        }

        /// <summary>
        /// Releases the quick-slot binding only when it still represents the supplied inventory.
        /// </summary>
        public void UnbindQuickSlots(PlayerInventoryManager inventoryManager)
        {
            if (m_boundInventoryManager != inventoryManager)
            {
                return;
            }

            UnbindCurrentQuickSlots();
            m_leftWeaponQuickSlot?.SetItem(null);
            m_rightWeaponQuickSlot?.SetItem(null);
        }

        /// <summary>
        /// Forces the status-bar layout to react to stat-driven width changes.
        /// </summary>
        public void RefreshHUD()
        {
            RefreshStatBarLayout(m_healthBar);
            RefreshStatBarLayout(m_staminaBar);

            if (transform is RectTransform rectTransform)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }
        }

        private void OnCurrentHealthChanged(float previousHealth, float currentHealth)
        {
            SetNewHealthValue(currentHealth);
        }

        private void OnMaxHealthChanged(float previousHealth, float maximumHealth)
        {
            SetMaxHealthValue(maximumHealth);
            if (m_boundNetworkManager != null)
            {
                SetNewHealthValue(m_boundNetworkManager.CurrentHealth.Value);
            }
        }

        private void OnCurrentStaminaChanged(float previousStamina, float currentStamina)
        {
            SetNewStaminaValue(currentStamina);
        }

        private void OnMaxStaminaChanged(float previousStamina, float maximumStamina)
        {
            SetMaxStaminaValue(maximumStamina);
            if (m_boundNetworkManager != null)
            {
                SetNewStaminaValue(m_boundNetworkManager.CurrentStamina.Value);
            }
        }

        private void OnRightHandWeaponChanged(WeaponItem weapon)
        {
            m_rightWeaponQuickSlot?.SetItem(weapon);
        }

        private void OnLeftHandWeaponChanged(WeaponItem weapon)
        {
            m_leftWeaponQuickSlot?.SetItem(weapon);
        }

        private void RefreshStatBars()
        {
            SetMaxHealthValue(m_boundNetworkManager.MaxHealth.Value);
            SetNewHealthValue(m_boundNetworkManager.CurrentHealth.Value);
            SetMaxStaminaValue(m_boundNetworkManager.MaxStamina.Value);
            SetNewStaminaValue(m_boundNetworkManager.CurrentStamina.Value);
        }

        private void RefreshQuickSlots()
        {
            m_leftWeaponQuickSlot?.SetItem(
                m_boundInventoryManager.CurrentLeftHandWeapon);
            m_rightWeaponQuickSlot?.SetItem(
                m_boundInventoryManager.CurrentRightHandWeapon);
            m_spellQuickSlot?.SetItem(null);
            m_itemQuickSlot?.SetItem(null);
        }

        private void UnbindCurrentStats()
        {
            if (m_boundNetworkManager == null)
            {
                return;
            }

            m_boundNetworkManager.CurrentHealth.OnValueChanged -= OnCurrentHealthChanged;
            m_boundNetworkManager.MaxHealth.OnValueChanged -= OnMaxHealthChanged;
            m_boundNetworkManager.CurrentStamina.OnValueChanged -= OnCurrentStaminaChanged;
            m_boundNetworkManager.MaxStamina.OnValueChanged -= OnMaxStaminaChanged;
            m_boundNetworkManager = null;
        }

        private void UnbindCurrentQuickSlots()
        {
            if (m_boundInventoryManager == null)
            {
                return;
            }

            m_boundInventoryManager.RightHandWeaponChanged -=
                OnRightHandWeaponChanged;
            m_boundInventoryManager.LeftHandWeaponChanged -=
                OnLeftHandWeaponChanged;
            m_boundInventoryManager = null;
        }

        private static void RefreshStatBarLayout(UIStatBar statBar)
        {
            if (statBar == null)
            {
                return;
            }

            GameObject statBarObject = statBar.gameObject;
            bool wasActive = statBarObject.activeSelf;
            statBarObject.SetActive(false);
            statBarObject.SetActive(wasActive);
        }
    }
}
