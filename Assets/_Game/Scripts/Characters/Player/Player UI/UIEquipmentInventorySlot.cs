using UnityEngine;

namespace ZZ
{
    /// <summary>Presents one compatible inventory item and delegates its equipment request.</summary>
    public class UIEquipmentInventorySlot : UIInventorySlot
    {
        private PlayerUIEquipmentManager m_equipmentManager;

        protected override void Awake()
        {
            base.Awake();
            m_equipmentManager ??=
                GetComponentInParent<PlayerUIEquipmentManager>(true);
        }

        /// <summary>Overrides automatic parent discovery for dynamically created slots.</summary>
        public void SetEquipmentManager(PlayerUIEquipmentManager equipmentManager)
        {
            m_equipmentManager = equipmentManager;
        }

        /// <summary>Equips the represented item into the currently selected equipment slot.</summary>
        public void EquipItem()
        {
            m_equipmentManager?.EquipItem(CurrentItem);
        }
    }
}
