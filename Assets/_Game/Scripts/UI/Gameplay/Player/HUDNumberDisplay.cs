using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>
    /// Renders an integer as a row of digit sprite Images (HUD/Numbers art).
    /// Reuses digit Images across value changes and hides unused digits immediately.
    /// </summary>
    public class HUDNumberDisplay : MonoBehaviour
    {
        private const string k_DigitObjectName = "Digit";

        [Header("Appearance")]
        [SerializeField] private Sprite[] m_digitSprites; // index 0..9
        [SerializeField, Min(1f)] private float m_digitHeight = 24f;
        [SerializeField, Min(0f)] private float m_letterSpacing = 1f;
        [SerializeField] private bool m_rightAligned = true;

        private readonly List<Image> m_digitImages = new();
        private int m_lastNumber = int.MinValue;

        /// <summary>Shows the given value, clearing the row when it is negative.</summary>
        public void SetNumber(int number)
        {
            if (m_digitImages.Count == 0)
            {
                foreach (Transform child in transform)
                {
                    if (child.name == k_DigitObjectName && child.TryGetComponent(out Image image))
                    {
                        m_digitImages.Add(image);
                    }
                }
            }

            if (number == m_lastNumber)
            {
                return;
            }

            m_lastNumber = number;
            string digits = number >= 0 && number <= 999999999 ? number.ToString() : string.Empty;
            float runningOffset = 0f;
            int imageIndex = 0;
            for (int index = 0; index < digits.Length; index++)
            {
                int digitValue = digits[m_rightAligned ? digits.Length - 1 - index : index] - '0';
                Sprite digitSprite = GetDigitSprite(digitValue);
                if (digitSprite == null)
                {
                    continue;
                }

                float digitWidth =
                    m_digitHeight * (digitSprite.rect.width / digitSprite.rect.height);
                Image digitImage = GetOrCreateDigitImage(imageIndex);
                RectTransform rectTransform = digitImage.rectTransform;
                float horizontalAnchor = m_rightAligned ? 1f : 0f;
                rectTransform.pivot = new Vector2(horizontalAnchor, 0.5f);
                rectTransform.anchorMin = new Vector2(horizontalAnchor, 0.5f);
                rectTransform.anchorMax = rectTransform.anchorMin;
                rectTransform.anchoredPosition = new Vector2(m_rightAligned ? -runningOffset : runningOffset, 0f);
                rectTransform.sizeDelta = new Vector2(digitWidth, m_digitHeight);
                digitImage.sprite = digitSprite;
                digitImage.gameObject.SetActive(true);
                imageIndex++;
                runningOffset += digitWidth + m_letterSpacing;
            }

            for (int index = imageIndex; index < m_digitImages.Count; index++)
            {
                m_digitImages[index].gameObject.SetActive(false);
            }
        }

        private Sprite GetDigitSprite(int digitValue)
        {
            if (m_digitSprites == null || digitValue < 0 || digitValue >= m_digitSprites.Length)
            {
                return null;
            }

            return m_digitSprites[digitValue];
        }

        private Image GetOrCreateDigitImage(int index)
        {
            if (index < m_digitImages.Count)
            {
                return m_digitImages[index];
            }

            var digitObject = new GameObject(
                k_DigitObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            digitObject.transform.SetParent(transform, false);

            Image digitImage = digitObject.GetComponent<Image>();
            digitImage.raycastTarget = false;
            m_digitImages.Add(digitImage);
            return digitImage;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            m_lastNumber = int.MinValue;
            if (m_digitSprites == null || m_digitSprites.Length != 10)
            {
                m_digitSprites = new Sprite[10];
            }

            for (int index = 0; index < m_digitSprites.Length; index++)
            {
                if (m_digitSprites[index] != null)
                {
                    continue;
                }

                m_digitSprites[index] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"Assets/_Game/Art/UI/HUD/Numbers/hud_{index}.png");
            }
        }
#endif
    }
}
