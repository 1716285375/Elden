using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>
    /// Renders an integer as a row of digit sprite Images (HUD/Numbers art).
    /// Child images are generated on demand from the current value, so the row
    /// always fits its content. Alignment and digit size are configurable.
    /// </summary>
    public class HUDNumberDisplay : MonoBehaviour
    {
        private const string k_DigitObjectName = "Digit";

        [Header("Appearance")]
        [SerializeField] private Sprite[] m_digitSprites; // index 0..9
        [SerializeField, Min(1f)] private float m_digitHeight = 24f;
        [SerializeField, Min(0f)] private float m_letterSpacing = 1f;
        [SerializeField] private bool m_rightAligned = true;

        /// <summary>Shows the given value, clearing the row when it is negative.</summary>
        public void SetNumber(int number)
        {
            ClearDigits();

            if (number < 0 || number > 999999999)
            {
                return;
            }

            string digits = number.ToString();
            float runningOffset = 0f;
            for (int index = digits.Length - 1; index >= 0; index--)
            {
                int digitValue = digits[index] - '0';
                Sprite digitSprite = GetDigitSprite(digitValue);
                if (digitSprite == null)
                {
                    continue;
                }

                float digitWidth =
                    m_digitHeight * (digitSprite.rect.width / digitSprite.rect.height);
                AddDigitImage(digitSprite, digitWidth, runningOffset);
                runningOffset += digitWidth + m_letterSpacing;
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

        private void AddDigitImage(Sprite digitSprite, float digitWidth, float offset)
        {
            var digitObject = new GameObject(
                k_DigitObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            digitObject.transform.SetParent(transform, false);

            RectTransform rectTransform = (RectTransform)digitObject.transform;
            rectTransform.pivot = new Vector2(1f, 0.5f);
            rectTransform.anchorMin = new Vector2(m_rightAligned ? 1f : 0f, 0.5f);
            rectTransform.anchorMax = new Vector2(m_rightAligned ? 1f : 0f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(
                m_rightAligned ? -offset : offset,
                0f);
            rectTransform.sizeDelta = new Vector2(digitWidth, m_digitHeight);

            Image digitImage = digitObject.GetComponent<Image>();
            digitImage.sprite = digitSprite;
            digitImage.raycastTarget = false;
        }

        private void ClearDigits()
        {
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                Transform child = transform.GetChild(index);
                if (child.name == k_DigitObjectName)
                {
                    Destroy(child.gameObject);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
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
