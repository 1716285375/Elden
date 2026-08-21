using System;
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
        private const int k_CurrentDataVersion = 1;

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

        internal void MigrateToLatestVersion()
        {
            if (m_dataVersion >= k_CurrentDataVersion)
            {
                return;
            }

            m_vitality = k_DefaultAttributeLevel;
            m_endurance = k_DefaultAttributeLevel;
            m_currentHealth = k_DefaultCurrentHealth;
            m_currentStamina = k_DefaultCurrentStamina;
            m_dataVersion = k_CurrentDataVersion;
        }
    }
}
