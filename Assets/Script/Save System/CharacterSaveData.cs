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
        [SerializeField] private string m_characterName = string.Empty;
        [SerializeField, Min(0f)] private float m_secondsPlayed;
        [SerializeField] private float m_xPosition;
        [SerializeField] private float m_yPosition;
        [SerializeField] private float m_zPosition;
        [SerializeField] private int m_sceneIndex;

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
    }
}
