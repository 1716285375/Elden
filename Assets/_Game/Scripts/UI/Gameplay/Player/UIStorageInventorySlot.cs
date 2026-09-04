using TMPro;
using UnityEngine.EventSystems;

namespace ZZ
{
    /// <summary>Presents one transferable Inventory or Storage item.</summary>
    public sealed class UIStorageInventorySlot : UIInventorySlot
    {
        private PlayerUIStorageManager m_storageManager;
        private TMP_Text m_amountText;

        public bool IsSelectingFromPlayerInventory { get; private set; }

        /// <summary>Connects this reusable slot to one side of the Storage screen.</summary>
        public void Initialize(
            PlayerUIStorageManager storageManager,
            TMP_Text amountText)
        {
            m_storageManager = storageManager;
            m_amountText = amountText;
            SlotButton.onClick.RemoveAllListeners();
            SlotButton.onClick.AddListener(SwapItemLocation);
        }

        /// <summary>Sets item data and records which container currently owns it.</summary>
        public void AddStorageItem(
            Item item,
            bool isSelectingFromPlayerInventory)
        {
            IsSelectingFromPlayerInventory = isSelectingFromPlayerInventory;
            AddItem(item);
            if (m_amountText != null)
            {
                m_amountText.text = item?.IsStackable == true
                    ? item.CurrentItemAmount.ToString()
                    : string.Empty;
            }
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            m_storageManager?.SelectItem(
                CurrentItem,
                IsSelectingFromPlayerInventory);
        }

        public void SwapItemLocation()
        {
            m_storageManager?.SwapItemLocation(
                CurrentItem,
                IsSelectingFromPlayerInventory);
        }
    }
}
