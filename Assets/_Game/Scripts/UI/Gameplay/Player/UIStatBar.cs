using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    [RequireComponent(typeof(Slider))]
    public class UIStatBar : MonoBehaviour
    {
        [SerializeField] private Slider m_slider;
        [SerializeField] private RectTransform m_rectTransform;
        [SerializeField] private Image m_fillImage;
        [SerializeField] private bool m_shouldScaleBarLengthWithStats;
        [SerializeField, Min(0f)] private float m_widthScaleMultiplier = 2f;
        [SerializeField] private bool m_shouldTweenValue = true;
        [SerializeField, Min(0f)] private float m_valueTweenDuration = 0.25f;
        [SerializeField] private Ease m_valueTweenEase = Ease.OutQuad;

        private Color m_defaultFillColor;
        private bool m_hasDefaultFillColor;
        private Tween m_valueTween;

        /// <summary>
        /// Whether <see cref="SetStat"/> eases towards the new value or assigns it outright.
        /// Subclasses that track a value exactly override this instead of relying on the field.
        /// </summary>
        protected virtual bool ShouldTweenValue => m_shouldTweenValue;

        protected virtual void Awake()
        {
            m_slider ??= GetComponent<Slider>();
            m_rectTransform ??= GetComponent<RectTransform>();
            ResolveFillImage();
        }

        private void OnDestroy()
        {
            m_valueTween?.Kill();
            m_valueTween = null;
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

            float clampedValue = Mathf.Clamp(
                value,
                m_slider.minValue,
                m_slider.maxValue);

            m_valueTween?.Kill();
            m_valueTween = null;

            if (!ShouldTweenValue || m_valueTweenDuration <= 0f)
            {
                m_slider.value = clampedValue;
                return;
            }

            // Killing without completing means the next hit picks up from the value currently on
            // screen, so rapid damage chains smoothly instead of snapping back to a stale target.
            m_valueTween = m_slider
                .DOValue(clampedValue, m_valueTweenDuration)
                .SetEase(m_valueTweenEase)
                .SetUpdate(true);
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

            // A value tween still in flight would overwrite the snapped value below, which leaves
            // level-ups and boss binds animating towards a stale target.
            m_valueTween?.Kill();
            m_valueTween = null;

            m_slider.minValue = 0f;
            m_slider.maxValue = Mathf.Max(0f, maximumValue);
            m_slider.value = m_slider.maxValue;
            ScaleBarLength(m_slider.maxValue);
        }

        /// <summary>Toggles the shared Poison color without losing the authored fill color.</summary>
        public void SetPoisonedColor(bool isPoisoned)
        {
            ResolveFillImage();
            if (m_fillImage == null)
            {
                return;
            }

            Color poisonColor = WorldUtilityManager.Instance != null
                ? WorldUtilityManager.Instance.PoisonColor
                : new Color(0.34f, 0.62f, 0.2f, 1f);
            m_fillImage.color = isPoisoned
                ? poisonColor
                : m_defaultFillColor;
        }

        private void ResolveFillImage()
        {
            m_slider ??= GetComponent<Slider>();
            m_fillImage ??= m_slider?.fillRect?.GetComponent<Image>();
            if (m_fillImage != null && !m_hasDefaultFillColor)
            {
                m_defaultFillColor = m_fillImage.color;
                m_hasDefaultFillColor = true;
            }
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
