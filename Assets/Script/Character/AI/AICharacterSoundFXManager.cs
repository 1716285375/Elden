using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Provides fixed, equipment-independent blocking sounds for an AI character.</summary>
    public class AICharacterSoundFXManager : CharacterSoundFXManager
    {
        [Header("Blocking Sounds")]
        [SerializeField] private AudioClip[] m_blockingSoundEffects =
            Array.Empty<AudioClip>();

        /// <summary>Plays one random AI blocking impact through the character's audio source.</summary>
        public override void PlayBlockingSoundEffect()
        {
            WorldSoundFXManager.Instance?.PlaySoundEffect(
                m_blockingSoundEffects,
                CharacterAudioSource);
        }
    }
}
