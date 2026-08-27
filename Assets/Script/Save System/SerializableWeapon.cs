using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Stores the minimum state required to rebuild one distinct weapon instance.
    /// </summary>
    [Serializable]
    public sealed class SerializableWeapon
    {
        [SerializeField] private int m_itemID = -1;
        [SerializeField] private int m_ashOfWarID = -1;
        [SerializeField, Range(0, 10)] private int m_upgradeLevel;

        public SerializableWeapon()
        {
        }

        public SerializableWeapon(
            int itemID,
            int ashOfWarID,
            int upgradeLevel = 0)
        {
            ItemID = itemID;
            AshOfWarID = ashOfWarID;
            UpgradeLevel = upgradeLevel;
        }

        /// <summary>Gets or sets the stable weapon-template identifier.</summary>
        public int ItemID
        {
            get => m_itemID;
            set => m_itemID = Mathf.Max(-1, value);
        }

        /// <summary>Gets or sets the equipped Ash of War identifier, or -1 for none.</summary>
        public int AshOfWarID
        {
            get => m_ashOfWarID;
            set => m_ashOfWarID = Mathf.Max(-1, value);
        }

        /// <summary>Gets or sets the saved per-instance reinforcement level.</summary>
        public int UpgradeLevel
        {
            get => m_upgradeLevel;
            set => m_upgradeLevel = Mathf.Clamp(value, 0, 10);
        }
    }
}
