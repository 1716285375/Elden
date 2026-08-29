using TMPro;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Presents one character's Health changes, accumulated damage or healing, and delayed hiding.
    /// </summary>
    public class CharacterHPBar : UIStatBar
    {
        [SerializeField, Min(0f)] private float m_defaultTimeBeforeBarHides = 3f;
        [SerializeField] private TMP_Text m_healthChangeText;

        private float m_currentHealthChange;
        private float m_oldHealthValue;
        private float m_hideTime;

        public float CurrentHealthChange => m_currentHealthChange;
        public float OldHealthValue => m_oldHealthValue;

        private void Update()
        {
            if (Time.time >= m_hideTime)
            {
                Hide();
            }
        }

        /// <summary>Initializes the synchronized range without presenting a change popup.</summary>
        public void Initialize(float maximumHealth, float currentHealth)
        {
            SetMaxStat(maximumHealth);
            SetStat(currentHealth);
            m_oldHealthValue = currentHealth;
            m_currentHealthChange = 0f;
            Hide();
        }

        /// <summary>Updates the bar and presents accumulated damage or healing.</summary>
        public void OnHPChanged(float oldHealthValue, float newHealthValue)
        {
            if (!gameObject.activeSelf)
            {
                m_currentHealthChange = 0f;
            }

            float healthChange = newHealthValue - oldHealthValue;
            if (!Mathf.Approximately(healthChange, 0f))
            {
                if (!Mathf.Approximately(m_currentHealthChange, 0f) &&
                    Mathf.Sign(m_currentHealthChange) != Mathf.Sign(healthChange))
                {
                    m_currentHealthChange = 0f;
                }

                m_currentHealthChange += healthChange;
            }

            m_oldHealthValue = newHealthValue;
            SetStat(newHealthValue);
            UpdateChangeText();
            m_hideTime = Time.time + m_defaultTimeBeforeBarHides;
            gameObject.SetActive(true);
        }

        private void UpdateChangeText()
        {
            if (m_healthChangeText == null)
            {
                return;
            }

            int displayValue = Mathf.RoundToInt(Mathf.Abs(m_currentHealthChange));
            m_healthChangeText.text = m_currentHealthChange > 0f
                ? $"+{displayValue}"
                : $"-{displayValue}";
            m_healthChangeText.color = m_currentHealthChange > 0f
                ? new Color(0.3f, 0.85f, 0.35f, 1f)
                : Color.white;
        }

        private void Hide()
        {
            m_hideTime = float.PositiveInfinity;
            gameObject.SetActive(false);
        }
    }
}
