using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Selects and plays shared one-shot sound effects without coupling gameplay to clips.
    /// </summary>
    public class WorldSoundFXManager : MonoBehaviour
    {
        private static WorldSoundFXManager s_instance;

        [SerializeField] private AudioClip m_rollingSoundFX;
        [SerializeField] private AudioClip m_pickupItemSoundEffect;
        [SerializeField] private AudioClip m_stanceBreakSoundEffect;
        [SerializeField] private AudioClip m_criticalStrikeSoundEffect;

        public static WorldSoundFXManager Instance => s_instance;
        public AudioClip RollingSoundFX => m_rollingSoundFX;
        public AudioClip PickupItemSoundEffect => m_pickupItemSoundEffect;
        public AudioClip StanceBreakSoundEffect => m_stanceBreakSoundEffect;
        public AudioClip CriticalStrikeSoundEffect =>
            m_criticalStrikeSoundEffect;

        /// <summary>
        /// Selects a non-null clip at random and plays it through the supplied spatial source.
        /// </summary>
        public bool PlaySoundEffect(
            AudioClip[] soundEffects,
            AudioSource audioSource,
            float volumeScale = 1f)
        {
            if (audioSource == null ||
                !TrySelectRandomSoundEffect(soundEffects, out AudioClip soundEffect))
            {
                return false;
            }

            audioSource.PlayOneShot(soundEffect, Mathf.Clamp01(volumeScale));
            return true;
        }

        /// <summary>Selects one non-null clip without allocating a filtered collection.</summary>
        public static bool TrySelectRandomSoundEffect(
            AudioClip[] soundEffects,
            out AudioClip soundEffect)
        {
            soundEffect = null;
            if (soundEffects == null || soundEffects.Length == 0)
            {
                return false;
            }

            int validSoundCount = 0;
            for (int soundIndex = 0; soundIndex < soundEffects.Length; soundIndex++)
            {
                if (soundEffects[soundIndex] != null)
                {
                    validSoundCount++;
                }
            }

            if (validSoundCount == 0)
            {
                return false;
            }

            int selectedValidIndex = Random.Range(0, validSoundCount);
            for (int soundIndex = 0; soundIndex < soundEffects.Length; soundIndex++)
            {
                AudioClip candidate = soundEffects[soundIndex];
                if (candidate == null)
                {
                    continue;
                }

                if (selectedValidIndex == 0)
                {
                    soundEffect = candidate;
                    return true;
                }

                selectedValidIndex--;
            }

            return false;
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}
