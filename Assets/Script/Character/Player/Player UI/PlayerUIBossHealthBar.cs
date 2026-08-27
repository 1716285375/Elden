using System.Collections;
using TMPro;
using UnityEngine;

namespace ZZ
{
    /// <summary>Displays the active network Boss name and replicated Health.</summary>
    public class PlayerUIBossHealthBar : MonoBehaviour
    {
        private const float k_DefaultRemovalDelay = 1f;

        [SerializeField] private TMP_Text m_bossNameText;
        [SerializeField] private UIStatBar m_healthBar;

        private BossCharacterManager m_boundBoss;
        private CharacterNetworkManager m_boundNetworkManager;
        private Coroutine m_removeHealthBarCoroutine;

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

            StopRemovalCoroutine();
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

        /// <summary>Removes the supplied Boss bar after the standard encounter-end delay.</summary>
        public void RemoveHPBar(BossCharacterManager boss)
        {
            if (boss != null && m_boundBoss != boss)
            {
                return;
            }

            StopRemovalCoroutine();
            if (isActiveAndEnabled)
            {
                m_removeHealthBarCoroutine = StartCoroutine(
                    RemoveHPBarAfterDelay());
            }
            else
            {
                UnbindBoss(boss);
            }
        }

        /// <summary>Releases this HUD only when it is still bound to the supplied Boss.</summary>
        public void UnbindBoss(BossCharacterManager boss)
        {
            if (boss != null && m_boundBoss != boss)
            {
                return;
            }

            StopRemovalCoroutine();
            UnbindCurrentBoss();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            StopRemovalCoroutine();
            UnbindCurrentBoss();
        }

        private IEnumerator RemoveHPBarAfterDelay()
        {
            yield return new WaitForSecondsRealtime(k_DefaultRemovalDelay);

            m_removeHealthBarCoroutine = null;
            UnbindCurrentBoss();
            gameObject.SetActive(false);
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

        private void StopRemovalCoroutine()
        {
            if (m_removeHealthBarCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_removeHealthBarCoroutine);
            m_removeHealthBarCoroutine = null;
        }
    }
}
