using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Binds a character's replicated Health to its world-space UI and camera-facing presentation.
    /// </summary>
    public class CharacterUIManager : MonoBehaviour
    {
        [SerializeField] private CharacterManager m_character;
        [SerializeField] private Canvas m_characterUICanvas;
        [SerializeField] private CharacterHPBar m_characterHPBar;

        private CharacterNetworkManager m_networkManager;
        private bool m_isHealthBound;

        private void Awake()
        {
            m_character ??= GetComponentInParent<CharacterManager>();
            m_characterUICanvas ??= GetComponent<Canvas>();
            m_characterHPBar ??= GetComponentInChildren<CharacterHPBar>(true);
            m_networkManager = m_character?.CharacterNetworkManager;
        }

        private void OnEnable()
        {
            if (m_character is AICharacterManager)
            {
                BindNetworkHealth();
            }
        }

        private void OnDisable()
        {
            UnbindNetworkHealth();
        }

        private void LateUpdate()
        {
            Camera viewingCamera = Camera.main;
            if (viewingCamera == null || m_characterUICanvas == null)
            {
                return;
            }

            Transform canvasTransform = m_characterUICanvas.transform;
            canvasTransform.LookAt(
                canvasTransform.position + viewingCamera.transform.forward,
                viewingCamera.transform.up);
        }

        /// <summary>Subscribes this presentation to synchronized Health values.</summary>
        public void BindNetworkHealth()
        {
            m_networkManager ??= m_character?.CharacterNetworkManager;
            if (m_character != null && !m_character.HasFloatingHPBar)
            {
                RefreshVisibility();
                return;
            }

            if (m_isHealthBound || m_networkManager == null)
            {
                RefreshVisibility();
                return;
            }

            m_networkManager.CurrentHealth.OnValueChanged += OnHPChanged;
            m_networkManager.MaxHealth.OnValueChanged += OnMaximumHealthChanged;
            m_isHealthBound = true;
            RefreshVisibility();
            m_characterHPBar?.Initialize(
                m_networkManager.MaxHealth.Value,
                m_networkManager.CurrentHealth.Value);
        }

        /// <summary>Releases synchronized Health subscriptions for despawn or disable.</summary>
        public void UnbindNetworkHealth()
        {
            if (!m_isHealthBound || m_networkManager == null)
            {
                return;
            }

            m_networkManager.CurrentHealth.OnValueChanged -= OnHPChanged;
            m_networkManager.MaxHealth.OnValueChanged -= OnMaximumHealthChanged;
            m_isHealthBound = false;
            m_characterHPBar?.gameObject.SetActive(false);
        }

        /// <summary>Re-evaluates local-owner and Boss visibility without changing Health.</summary>
        public void RefreshVisibility()
        {
            bool shouldHaveFloatingBar = m_character != null &&
                m_character.HasFloatingHPBar &&
                (m_character is not PlayerManager player || !player.IsOwner);
            if (m_characterUICanvas != null)
            {
                m_characterUICanvas.enabled = shouldHaveFloatingBar;
            }

            if (!shouldHaveFloatingBar)
            {
                m_characterHPBar?.gameObject.SetActive(false);
            }
        }

        /// <summary>Updates the world-space bar from a replicated Health change.</summary>
        public void OnHPChanged(float oldHealthValue, float newHealthValue)
        {
            RefreshVisibility();
            if (m_characterUICanvas != null && m_characterUICanvas.enabled)
            {
                m_characterHPBar?.OnHPChanged(oldHealthValue, newHealthValue);
            }
        }

        /// <summary>Clears accumulated Health changes and hides the floating bar.</summary>
        public void ResetCharacterHPBar()
        {
            m_networkManager ??= m_character?.CharacterNetworkManager;
            if (m_networkManager == null)
            {
                return;
            }

            m_characterHPBar?.Initialize(
                m_networkManager.MaxHealth.Value,
                m_networkManager.CurrentHealth.Value);
        }

        private void OnMaximumHealthChanged(
            float previousMaximumHealth,
            float maximumHealth)
        {
            m_characterHPBar?.Initialize(
                maximumHealth,
                m_networkManager.CurrentHealth.Value);
        }
    }
}
