using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Presents one compatible inventory item and delegates its equipment request.</summary>
    [RequireComponent(typeof(Button))]
    public class UIEquipmentInventorySlot : MonoBehaviour
    {
        [SerializeField] private Image m_itemIcon;
        [SerializeField] private Image m_highlightedIcon;
        [SerializeField] private TMP_Text m_itemNameText;

        private Item m_currentItem;
        private PlayerUIEquipmentManager m_equipmentManager;

        public Item CurrentItem => m_currentItem;

        private void Awake()
        {
            m_equipmentManager ??=
                GetComponentInParent<PlayerUIEquipmentManager>(true);
            DeselectSlot();
        }

        /// <summary>Configures this reusable slot with one valid inventory item.</summary>
        public bool AddItem(Item item)
        {
            if (item == null)
            {
                return false;
            }

            m_currentItem = item;
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

        /// <summary>Overrides automatic parent discovery for dynamically created slots.</summary>
        public void SetEquipmentManager(PlayerUIEquipmentManager equipmentManager)
        {
            m_equipmentManager = equipmentManager;
        }

        /// <summary>Equips the represented item into the currently selected equipment slot.</summary>
        public void EquipItem()
        {
            m_equipmentManager?.EquipItem(m_currentItem);
        }

        /// <summary>Shows keyboard or controller selection focus.</summary>
        public void SelectSlot()
        {
            m_highlightedIcon?.gameObject.SetActive(true);
        }

        /// <summary>Hides keyboard or controller selection focus.</summary>
        public void DeselectSlot()
        {
            m_highlightedIcon?.gameObject.SetActive(false);
        }

        /// <summary>EventTrigger adapter for selection events.</summary>
        public void SelectSlot(BaseEventData eventData)
        {
            SelectSlot();
        }

        /// <summary>EventTrigger adapter for deselection events.</summary>
        public void DeselectSlot(BaseEventData eventData)
        {
            DeselectSlot();
        }
    }
}
