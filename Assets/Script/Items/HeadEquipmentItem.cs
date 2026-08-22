using UnityEngine;

namespace ZZ
{
    /// <summary>Defines an armor item equipped in the head slot.</summary>
    [CreateAssetMenu(fileName = "Head Equipment", menuName = "ZZ/Items/Armor/Head")]
    public class HeadEquipmentItem : ArmorItem
    {
        [SerializeField] private HeadEquipmentType m_headEquipmentType;

        /// <summary>Gets the body-feature visibility rule used by this head item.</summary>
        public HeadEquipmentType HeadEquipmentType => m_headEquipmentType;
    }
}
