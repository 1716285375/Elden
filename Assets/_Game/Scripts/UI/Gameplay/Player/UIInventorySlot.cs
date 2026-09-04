using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Presents one inventory item with shared focus and icon behavior.</summary>
    [RequireComponent(typeof(Button))]
    public class UIInventorySlot : MonoBehaviour,
        ISelectHandler,
        IDeselectHandler
    {
        [SerializeField] private Image m_itemIcon;
        [SerializeField] private Image m_highlightedIcon;
        [SerializeField] private TMP_Text m_itemNameText;

        public Item CurrentItem { get; private set; }

        protected Button SlotButton { get; private set; }

        protected virtual void Awake()
        {
            SlotButton = GetComponent<Button>();
            DeselectSlot();
        }

        /// <summary>Configures this reusable slot with one valid inventory item.</summary>
        public virtual bool AddItem(Item item)
        {
            if (item == null)
            {
                return false;
            }

            CurrentItem = item;
            if (m_itemIcon != null)
            {
                m_itemIcon.sprite = item.ItemIcon;
                m_itemIcon.enabled = item.ItemIcon != null;
            }

            if (m_itemNameText != null)
            {
                m_itemNameText.text = item.ItemName;
            }

            return true;
        }

        /// <summary>Assigns visuals for a slot built by a runtime UI factory.</summary>
        public void SetVisualReferences(
            Image itemIcon,
            Image highlightedIcon,
            TMP_Text itemNameText)
        {
            m_itemIcon = itemIcon;
            m_highlightedIcon = highlightedIcon;
            m_itemNameText = itemNameText;
        }

        public void SelectSlot()
        {
            m_highlightedIcon?.gameObject.SetActive(true);
        }

        public void DeselectSlot()
        {
            m_highlightedIcon?.gameObject.SetActive(false);
        }

        public virtual void OnSelect(BaseEventData eventData)
        {
            SelectSlot();
        }

        public virtual void OnDeselect(BaseEventData eventData)
        {
            DeselectSlot();
        }

        /// <summary>EventTrigger adapter retained for authored inventory prefabs.</summary>
        public void SelectSlot(BaseEventData eventData)
        {
            SelectSlot();
        }

        /// <summary>EventTrigger adapter retained for authored inventory prefabs.</summary>
        public void DeselectSlot(BaseEventData eventData)
        {
            DeselectSlot();
        }
    }
}
