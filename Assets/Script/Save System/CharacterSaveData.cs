using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Contains the stable, serializable state required to restore one character.
    /// </summary>
    [Serializable]
    public class CharacterSaveData
    {
        private const int k_DefaultAttributeLevel = 10;
        private const float k_DefaultCurrentHealth = 150f;
        private const float k_DefaultCurrentStamina = 100f;
        private const int k_AttributeDataVersion = 1;
        private const int k_EquipmentDataVersion = 4;
        private const int k_CurrentDataVersion = 4;

        [SerializeField, Min(0)] private int m_dataVersion = k_CurrentDataVersion;
        [SerializeField] private string m_characterName = string.Empty;
        [SerializeField, Min(0f)] private float m_secondsPlayed;
        [SerializeField] private float m_xPosition;
        [SerializeField] private float m_yPosition;
        [SerializeField] private float m_zPosition;
        [SerializeField] private int m_sceneIndex;
        [SerializeField, Min(0)] private int m_vitality = k_DefaultAttributeLevel;
        [SerializeField, Min(0)] private int m_endurance = k_DefaultAttributeLevel;
        [SerializeField, Min(0f)] private float m_currentHealth = k_DefaultCurrentHealth;
        [SerializeField, Min(0f)] private float m_currentStamina = k_DefaultCurrentStamina;
        [SerializeField] private int m_headEquipmentID = -1;
        [SerializeField] private int m_bodyEquipmentID = -1;
        [SerializeField] private int m_handEquipmentID = -1;
        [SerializeField] private int m_legEquipmentID = -1;
        [SerializeField] private int m_rightHandWeaponSlot01ID = 1;
        [SerializeField] private int m_rightHandWeaponSlot02ID = 2;
        [SerializeField] private int m_rightHandWeaponSlot03ID;
        [SerializeField] private int m_leftHandWeaponSlot01ID = 3;
        [SerializeField] private int m_leftHandWeaponSlot02ID = 2;
        [SerializeField] private int m_leftHandWeaponSlot03ID;
        [SerializeField, Range(0, 2)] private int m_rightHandWeaponIndex;
        [SerializeField, Range(0, 2)] private int m_leftHandWeaponIndex;
        [SerializeField] private bool m_isMale = true;
        [SerializeField] private List<BossSaveData> m_bosses = new();
        [SerializeField] private List<SiteOfGraceSaveData> m_sitesOfGrace = new();

        public string CharacterName
        {
            get => m_characterName;
            set => m_characterName = value ?? string.Empty;
        }

        public float SecondsPlayed
        {
            get => m_secondsPlayed;
            set => m_secondsPlayed = Mathf.Max(0f, value);
        }

        public float XPosition
        {
            get => m_xPosition;
            set => m_xPosition = value;
        }

        public float YPosition
        {
            get => m_yPosition;
            set => m_yPosition = value;
        }

        public float ZPosition
        {
            get => m_zPosition;
            set => m_zPosition = value;
        }

        public int SceneIndex
        {
            get => m_sceneIndex;
            set => m_sceneIndex = value;
        }

        public int Vitality
        {
            get => m_vitality;
            set => m_vitality = Mathf.Max(0, value);
        }

        public int Endurance
        {
            get => m_endurance;
            set => m_endurance = Mathf.Max(0, value);
        }

        public float CurrentHealth
        {
            get => m_currentHealth;
            set => m_currentHealth = Mathf.Max(0f, value);
        }

        public float CurrentStamina
        {
            get => m_currentStamina;
            set => m_currentStamina = Mathf.Max(0f, value);
        }

        public int HeadEquipmentID
        {
            get => m_headEquipmentID;
            set => m_headEquipmentID = Mathf.Max(-1, value);
        }

        public int BodyEquipmentID
        {
            get => m_bodyEquipmentID;
            set => m_bodyEquipmentID = Mathf.Max(-1, value);
        }

        public int HandEquipmentID
        {
            get => m_handEquipmentID;
            set => m_handEquipmentID = Mathf.Max(-1, value);
        }

        public int LegEquipmentID
        {
            get => m_legEquipmentID;
            set => m_legEquipmentID = Mathf.Max(-1, value);
        }

        public int RightHandWeaponSlot01ID
        {
            get => m_rightHandWeaponSlot01ID;
            set => m_rightHandWeaponSlot01ID = Mathf.Max(0, value);
        }

        public int RightHandWeaponSlot02ID
        {
            get => m_rightHandWeaponSlot02ID;
            set => m_rightHandWeaponSlot02ID = Mathf.Max(0, value);
        }

        public int RightHandWeaponSlot03ID
        {
            get => m_rightHandWeaponSlot03ID;
            set => m_rightHandWeaponSlot03ID = Mathf.Max(0, value);
        }

        public int LeftHandWeaponSlot01ID
        {
            get => m_leftHandWeaponSlot01ID;
            set => m_leftHandWeaponSlot01ID = Mathf.Max(0, value);
        }

        public int LeftHandWeaponSlot02ID
        {
            get => m_leftHandWeaponSlot02ID;
            set => m_leftHandWeaponSlot02ID = Mathf.Max(0, value);
        }

        public int LeftHandWeaponSlot03ID
        {
            get => m_leftHandWeaponSlot03ID;
            set => m_leftHandWeaponSlot03ID = Mathf.Max(0, value);
        }

        public int RightHandWeaponIndex
        {
            get => m_rightHandWeaponIndex;
            set => m_rightHandWeaponIndex = Mathf.Clamp(value, 0, 2);
        }

        public int LeftHandWeaponIndex
        {
            get => m_leftHandWeaponIndex;
            set => m_leftHandWeaponIndex = Mathf.Clamp(value, 0, 2);
        }

        public bool IsMale
        {
            get => m_isMale;
            set => m_isMale = value;
        }

        /// <summary>Gets the saved lifecycle state for a boss, defaulting to dormant.</summary>
        public BossProgressState GetBossProgress(int bossID)
        {
            BossSaveData bossData = FindBossData(bossID);
            return bossData?.Progress ?? BossProgressState.Dormant;
        }

        /// <summary>
        /// Adds a boss entry or advances its state without allowing progress regression.
        /// </summary>
        public bool SetBossProgress(int bossID, BossProgressState progress)
        {
            if (bossID <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bossID),
                    bossID,
                    "Boss identifiers must be greater than zero.");
            }

            m_bosses ??= new List<BossSaveData>();
            BossSaveData bossData = FindBossData(bossID);
            if (bossData != null)
            {
                return bossData.AdvanceTo(progress);
            }

            m_bosses.Add(new BossSaveData(bossID, progress));
            return true;
        }

        /// <summary>Gets whether the identified boss should remain absent after loading.</summary>
        public bool IsBossDefeated(int bossID)
        {
            return GetBossProgress(bossID) == BossProgressState.Defeated;
        }

        /// <summary>Gets whether the identified Site of Grace has been restored.</summary>
        public bool IsSiteOfGraceActivated(int siteOfGraceID)
        {
            return FindSiteOfGraceData(siteOfGraceID)?.IsActivated ?? false;
        }

        /// <summary>Adds or updates one Site of Grace activation entry.</summary>
        public bool SetSiteOfGraceActivated(int siteOfGraceID, bool isActivated)
        {
            if (siteOfGraceID <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(siteOfGraceID),
                    siteOfGraceID,
                    "Site of Grace identifiers must be greater than zero.");
            }

            m_sitesOfGrace ??= new List<SiteOfGraceSaveData>();
            SiteOfGraceSaveData siteData = FindSiteOfGraceData(siteOfGraceID);
            if (siteData != null)
            {
                if (siteData.IsActivated == isActivated)
                {
                    return false;
                }

                siteData.IsActivated = isActivated;
                return true;
            }

            m_sitesOfGrace.Add(new SiteOfGraceSaveData(
                siteOfGraceID,
                isActivated));
            return true;
        }

        internal void MigrateToLatestVersion()
        {
            if (m_dataVersion < k_AttributeDataVersion)
            {
                m_vitality = k_DefaultAttributeLevel;
                m_endurance = k_DefaultAttributeLevel;
                m_currentHealth = k_DefaultCurrentHealth;
                m_currentStamina = k_DefaultCurrentStamina;
            }

            if (m_dataVersion < k_EquipmentDataVersion)
            {
                m_headEquipmentID = -1;
                m_bodyEquipmentID = -1;
                m_handEquipmentID = -1;
                m_legEquipmentID = -1;
                m_rightHandWeaponSlot01ID = 1;
                m_rightHandWeaponSlot02ID = 2;
                m_rightHandWeaponSlot03ID = 0;
                m_leftHandWeaponSlot01ID = 3;
                m_leftHandWeaponSlot02ID = 2;
                m_leftHandWeaponSlot03ID = 0;
                m_rightHandWeaponIndex = 0;
                m_leftHandWeaponIndex = 0;
                m_isMale = true;
            }

            m_bosses ??= new List<BossSaveData>();
            m_sitesOfGrace ??= new List<SiteOfGraceSaveData>();
            m_dataVersion = k_CurrentDataVersion;
        }

        private BossSaveData FindBossData(int bossID)
        {
            if (bossID <= 0 || m_bosses == null)
            {
                return null;
            }

            return m_bosses.FirstOrDefault(boss => boss?.BossID == bossID);
        }

        private SiteOfGraceSaveData FindSiteOfGraceData(int siteOfGraceID)
        {
            if (siteOfGraceID <= 0 || m_sitesOfGrace == null)
            {
                return null;
            }

            return m_sitesOfGrace.FirstOrDefault(
                site => site?.SiteOfGraceID == siteOfGraceID);
        }
    }
}
