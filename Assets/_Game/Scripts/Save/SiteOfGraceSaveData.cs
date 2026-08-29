using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Stores one stable Site of Grace activation entry for JSON save files.</summary>
    [Serializable]
    public sealed class SiteOfGraceSaveData
    {
        [SerializeField, Min(1)] private int m_siteOfGraceID;
        [SerializeField] private bool m_isActivated;

        /// <summary>Creates one serializable Site of Grace entry.</summary>
        public SiteOfGraceSaveData(int siteOfGraceID, bool isActivated)
        {
            m_siteOfGraceID = siteOfGraceID;
            m_isActivated = isActivated;
        }

        /// <summary>Gets the stable world-authored identifier.</summary>
        public int SiteOfGraceID => m_siteOfGraceID;

        /// <summary>Gets or sets whether this Site of Grace has been restored.</summary>
        public bool IsActivated
        {
            get => m_isActivated;
            set => m_isActivated = value;
        }
    }
}
