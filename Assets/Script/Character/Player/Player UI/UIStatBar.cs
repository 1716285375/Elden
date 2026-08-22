using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    [RequireComponent(typeof(Slider))]
    public class UIStatBar : MonoBehaviour
    {
        [SerializeField] private Slider m_slider;
        [SerializeField] private RectTransform m_rectTransform;
        [SerializeField] private bool m_shouldScaleBarLengthWithStats;
        [SerializeField, Min(0f)] private float m_widthScaleMultiplier = 2f;

        protected virtual void Awake()
        {
            m_slider ??= GetComponent<Slider>();
            m_rectTransform ??= GetComponent<RectTransform>();
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
            ScaleBarLength(m_slider.maxValue);
        }

        private void ScaleBarLength(float maximumValue)
        {
            if (!m_shouldScaleBarLengthWithStats || m_rectTransform == null)
            {
                return;
            }

            Vector2 sizeDelta = m_rectTransform.sizeDelta;
            sizeDelta.x = Mathf.Max(0f, maximumValue) * m_widthScaleMultiplier;
            m_rectTransform.sizeDelta = sizeDelta;
        }
    }
}
