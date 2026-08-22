using TMPro;
using UnityEngine;

namespace ZZ
{
    /// <summary>Displays the active network Boss name and replicated Health.</summary>
    public class PlayerUIBossHealthBar : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_bossNameText;
        [SerializeField] private UIStatBar m_healthBar;

        private BossCharacterManager m_boundBoss;
        private CharacterNetworkManager m_boundNetworkManager;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Binds the shared HUD to the supplied network Boss.</summary>
        public void BindBoss(BossCharacterManager boss)
        {
            if (boss == null)
            {
                return;
            }

            if (m_boundBoss != boss)
            {
                UnbindCurrentBoss();
                m_boundBoss = boss;
                m_boundNetworkManager = boss.CharacterNetworkManager;
                if (m_boundNetworkManager != null)
                {
                    m_boundNetworkManager.CurrentHealth.OnValueChanged +=
                        OnCurrentHealthChanged;
                    m_boundNetworkManager.MaxHealth.OnValueChanged +=
                        OnMaximumHealthChanged;
                }
            }

            if (m_bossNameText != null)
            {
                m_bossNameText.text = boss.BossName;
            }

            RefreshHealth();
            gameObject.SetActive(true);
        }

        /// <summary>Releases this HUD only when it is still bound to the supplied Boss.</summary>
        public void UnbindBoss(BossCharacterManager boss)
        {
            if (boss != null && m_boundBoss != boss)
            {
                return;
            }

            UnbindCurrentBoss();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnbindCurrentBoss();
        }

        private void OnCurrentHealthChanged(float previousHealth, float currentHealth)
        {
            m_healthBar?.SetStat(currentHealth);
        }

        private void OnMaximumHealthChanged(float previousHealth, float maximumHealth)
        {
            RefreshHealth();
        }

        private void RefreshHealth()
        {
            if (m_boundNetworkManager == null)
            {
                return;
            }

            m_healthBar?.SetMaxStat(m_boundNetworkManager.MaxHealth.Value);
            m_healthBar?.SetStat(m_boundNetworkManager.CurrentHealth.Value);
        }

        private void UnbindCurrentBoss()
        {
            if (m_boundNetworkManager != null)
            {
                m_boundNetworkManager.CurrentHealth.OnValueChanged -=
                    OnCurrentHealthChanged;
                m_boundNetworkManager.MaxHealth.OnValueChanged -=
                    OnMaximumHealthChanged;
            }

            m_boundNetworkManager = null;
            m_boundBoss = null;
        }
    }
}
