using UnityEngine;

namespace ZZ
{
    /// <summary>Owns the standalone item inventory, independently of equipment-slot selection.</summary>
    public sealed class PlayerUIInventoryManager : PlayerUIMenu
    {
        private InventoryMenuView m_view;

        public void OpenInventory()
        {
            if (m_view == null)
            {
                m_view = InventoryMenuView.Create(transform);
                SetMenuWindow(m_view.gameObject);
            }
            OpenMenu();
        }

        public void ReturnToCharacterMenu()
        {
            PlayerUIManager.Instance.PlayerUICharacterMenuManager.OpenCharacterMenu();
        }
    }
}
