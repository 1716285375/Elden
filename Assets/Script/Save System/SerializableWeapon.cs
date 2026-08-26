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

        public SerializableWeapon()
        {
        }

        public SerializableWeapon(int itemID, int ashOfWarID)
        {
            ItemID = itemID;
            AshOfWarID = ashOfWarID;
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
    }
}
