using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(AudioSource))]
    public class CharacterSoundFXManager : MonoBehaviour
    {
        [SerializeField] private AudioSource m_audioSource;

        protected virtual void Awake()
        {
            m_audioSource ??= GetComponent<AudioSource>();
        }

        /// <summary>
        /// Plays the shared rolling sound at the character's world position.
        /// </summary>
        public void PlayRollingSoundFX()
        {
            AudioClip rollingSoundFX = WorldSoundFXManager.Instance?.RollingSoundFX;
            if (m_audioSource == null || rollingSoundFX == null)
            {
                return;
            }

            m_audioSource.PlayOneShot(rollingSoundFX);
        }
    }
}
