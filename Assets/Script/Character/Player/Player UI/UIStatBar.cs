using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    [RequireComponent(typeof(Slider))]
    public class UIStatBar : MonoBehaviour
    {
        [SerializeField] private Slider m_slider;

        private void Awake()
        {
            m_slider ??= GetComponent<Slider>();
        }

        /// <summary>
        /// Updates the displayed value without changing the configured resource range.
        /// </summary>
        public void SetStat(float value)
        {
            if (m_slider == null)
            {
                return;
            }

            m_slider.value = Mathf.Clamp(value, m_slider.minValue, m_slider.maxValue);
        }

        /// <summary>
        /// Sets the resource range and initializes the visual to its maximum value.
        /// </summary>
        public void SetMaxStat(float maximumValue)
        {
            if (m_slider == null)
            {
                return;
            }

            m_slider.minValue = 0f;
            m_slider.maxValue = Mathf.Max(0f, maximumValue);
            m_slider.value = m_slider.maxValue;
        }
    }
}
