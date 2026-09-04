using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Defines weight and lifecycle hooks shared by every equippable catalog item.
    /// </summary>
    public abstract class EquipmentItem : Item
    {
        [Header("Equipment")]
        [SerializeField, Min(0f)] private float m_itemWeight;

        /// <summary>Gets the weight contributed while this item is equipped.</summary>
        public float ItemWeight => Mathf.Max(0f, m_itemWeight);

        /// <summary>Applies item-specific state after the runtime copy is equipped.</summary>
        public virtual void OnItemEquipped(CharacterManager character)
        {
        }

        /// <summary>Removes item-specific state before the runtime copy is discarded.</summary>
        public virtual void OnItemUnequipped(CharacterManager character)
        {
        }
    }
}
