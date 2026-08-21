using UnityEngine;

namespace ZZ
{
    public class PlayerUIHUDManager : MonoBehaviour
    {
        [SerializeField] private UIStatBar m_staminaBar;

        private CharacterNetworkManager m_boundNetworkManager;

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
        }

        /// <summary>
        /// Subscribes the HUD to one locally owned character and initializes its current values.
        /// </summary>
        public void BindStamina(CharacterNetworkManager networkManager)
        {
            if (networkManager == null)
            {
                return;
            }

            if (m_boundNetworkManager != networkManager)
            {
                UnbindCurrentStamina();
                m_boundNetworkManager = networkManager;
                m_boundNetworkManager.CurrentStamina.OnValueChanged += OnCurrentStaminaChanged;
                m_boundNetworkManager.MaxStamina.OnValueChanged += OnMaxStaminaChanged;
            }

            RefreshStaminaBar();
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Removes the binding only when the supplied character still owns this HUD connection.
        /// </summary>
        public void UnbindStamina(CharacterNetworkManager networkManager)
        {
            if (m_boundNetworkManager != networkManager)
            {
                return;
            }

            UnbindCurrentStamina();
            gameObject.SetActive(false);
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

        private void RefreshStaminaBar()
        {
            SetMaxStaminaValue(m_boundNetworkManager.MaxStamina.Value);
            SetNewStaminaValue(m_boundNetworkManager.CurrentStamina.Value);
        }

        private void UnbindCurrentStamina()
        {
            if (m_boundNetworkManager == null)
            {
                return;
            }

            m_boundNetworkManager.CurrentStamina.OnValueChanged -= OnCurrentStaminaChanged;
            m_boundNetworkManager.MaxStamina.OnValueChanged -= OnMaxStaminaChanged;
            m_boundNetworkManager = null;
        }
    }
}
