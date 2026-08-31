using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZZ
{
    /// <summary>Routes a reusable inventory slot to the active Buy or Sell action.</summary>
    public sealed class UIShopInventorySlot : UIInventorySlot
    {
        [SerializeField] private TMP_Text m_itemValueText;
        [SerializeField] private TMP_Text m_itemAmountText;

        private PlayerUIShopManager m_shopManager;

        /// <summary>Connects runtime-created shop labels and click behavior.</summary>
        public void Initialize(
            PlayerUIShopManager shopManager,
            TMP_Text itemValueText,
            TMP_Text itemAmountText)
        {
            m_shopManager = shopManager;
            m_itemValueText = itemValueText;
            m_itemAmountText = itemAmountText;
            SlotButton.onClick.RemoveListener(BuyOrSellItem);
            SlotButton.onClick.AddListener(BuyOrSellItem);
        }

        /// <inheritdoc />
        public override bool AddItem(Item item)
        {
            if (!base.AddItem(item))
            {
                return false;
            }

            int value = m_shopManager?.GetDisplayedValue(item) ?? 0;
            if (m_itemValueText != null)
            {
                m_itemValueText.text = value.ToString();
            }

            if (m_itemAmountText != null)
            {
                m_itemAmountText.text = item.IsInfinite
                    ? "∞"
                    : Mathf.Max(
                        1,
                        m_shopManager?.GetDisplayedAmount(item) ?? 1)
                        .ToString();
            }

            return true;
        }

        public void BuyOrSellItem()
        {
            m_shopManager?.BuyOrSellItem(CurrentItem);
        }

        /// <inheritdoc />
        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            m_shopManager?.SelectItem(CurrentItem);
        }
    }
}
