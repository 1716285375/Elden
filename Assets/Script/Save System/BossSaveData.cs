using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Serializable progress entry for one stable boss identifier.</summary>
    [Serializable]
    public class BossSaveData
    {
        [SerializeField, Min(1)] private int m_bossID;
        [SerializeField] private BossProgressState m_progress;

        /// <summary>Creates one boss progress entry for JSON persistence.</summary>
        public BossSaveData(int bossID, BossProgressState progress)
        {
            m_bossID = bossID;
            m_progress = progress;
        }

        /// <summary>Gets the stable authored boss identifier.</summary>
        public int BossID => m_bossID;

        /// <summary>Gets the furthest lifecycle state this boss has reached.</summary>
        public BossProgressState Progress => m_progress;

        internal bool AdvanceTo(BossProgressState progress)
        {
            if ((byte)progress <= (byte)m_progress)
            {
                return false;
            }

            m_progress = progress;
            return true;
        }
    }
}
