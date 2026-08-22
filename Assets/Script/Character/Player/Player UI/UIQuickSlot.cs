using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>
    /// Presents one reusable quick-slot item, quantity, and selection highlight.
    /// </summary>
    public class UIQuickSlot : MonoBehaviour
    {
        [SerializeField] private Image m_iconImage;
        [SerializeField] private TMP_Text m_quantityText;
        [SerializeField] private Image m_highlightImage;

        /// <summary>Updates the item icon and hides the icon for an empty slot.</summary>
        public void SetItem(Item item)
        {
            if (m_iconImage == null)
            {
                return;
            }

            Sprite icon = item != null ? item.ItemIcon : null;
            m_iconImage.sprite = icon;
            m_iconImage.enabled = icon != null;
        }

        /// <summary>Shows a stack count greater than one and hides it otherwise.</summary>
        public void SetQuantity(int quantity)
        {
            if (m_quantityText == null)
            {
                return;
            }

            bool shouldShowQuantity = quantity > 1;
            m_quantityText.gameObject.SetActive(shouldShowQuantity);
            m_quantityText.text = shouldShowQuantity ? quantity.ToString() : string.Empty;
        }

        /// <summary>Controls the reserved selection highlight.</summary>
        public void SetHighlighted(bool isHighlighted)
        {
            if (m_highlightImage != null)
            {
                m_highlightImage.gameObject.SetActive(isHighlighted);
            }
        }
    }
}
