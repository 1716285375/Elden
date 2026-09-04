using UnityEngine;

namespace ZZ
{
    /// <summary>Plays spatial one-shot sounds associated with one character.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class CharacterSoundFXManager : MonoBehaviour
    {
        [SerializeField] private AudioSource m_audioSource;

        [Header("Damage Sounds")]
        [SerializeField] private AudioClip[] m_damageGrunts =
            System.Array.Empty<AudioClip>();

        [Header("Movement Sounds")]
        [SerializeField] private AudioClip[] m_footstepSounds =
            System.Array.Empty<AudioClip>();

        protected AudioSource CharacterAudioSource => m_audioSource;

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

        /// <summary>Plays the shared item-collection sound before its world object is removed.</summary>
        public void PlayPickupItemSound()
        {
            AudioClip pickupSound = WorldSoundFXManager.Instance?.PickupItemSoundEffect;
            if (m_audioSource != null && pickupSound != null)
            {
                m_audioSource.PlayOneShot(pickupSound);
            }
        }

        /// <summary>Plays a random whoosh belonging to the weapon opening its hit window.</summary>
        public void PlayWeaponWhoosh(WeaponItem weapon)
        {
            if (weapon == null)
            {
                return;
            }

            PlayRandomSoundEffect(weapon.Whooshes);
        }

        /// <summary>Plays a randomized bow draw or release clip through this character.</summary>
        public void PlayRangedWeaponSound(
            RangedWeaponItem weapon,
            bool isRelease)
        {
            if (weapon == null)
            {
                return;
            }

            PlayRandomSoundEffect(
                isRelease
                    ? weapon.ReleaseSoundEffects
                    : weapon.DrawSoundEffects);
        }

        /// <summary>Plays shared full or empty flask feedback through this character.</summary>
        public void PlayFlaskSound(bool isEmpty)
        {
            AudioClip flaskSound = isEmpty
                ? WorldSoundFXManager.Instance?.EmptyFlaskSoundEffect
                : WorldSoundFXManager.Instance?.FlaskRestorationSoundEffect;
            if (m_audioSource != null && flaskSound != null)
            {
                m_audioSource.PlayOneShot(flaskSound);
            }
        }

        /// <summary>Plays one locally spatialized damage grunt for this character.</summary>
        public void PlayDamageGrunt()
        {
            PlayRandomSoundEffect(m_damageGrunts);
        }

        /// <summary>Plays one authored footstep without producing an AI stimulus.</summary>
        public virtual void PlayFootstepSoundEffect()
        {
            PlayRandomSoundEffect(m_footstepSounds);
        }

        /// <summary>Plays a resolved block-impact clip through the character's spatial source.</summary>
        public void PlayBlockSound(AudioClip blockSound)
        {
            if (m_audioSource != null && blockSound != null)
            {
                m_audioSource.PlayOneShot(blockSound);
            }
        }

        /// <summary>Plays blocking feedback selected by the character implementation.</summary>
        public virtual void PlayBlockingSoundEffect()
        {
        }

        /// <summary>Plays the shared Stance Break impact on every presenting peer.</summary>
        public virtual void PlayStanceBrokenSoundEffect()
        {
            AudioClip stanceBreakSound =
                WorldSoundFXManager.Instance?.StanceBreakSoundEffect;
            if (m_audioSource != null && stanceBreakSound != null)
            {
                m_audioSource.PlayOneShot(stanceBreakSound);
            }
        }

        /// <summary>Plays the shared Critical Strike impact at the victim.</summary>
        public virtual void PlayCriticalStrikeSoundEffect()
        {
            AudioClip criticalStrikeSound =
                WorldSoundFXManager.Instance?.CriticalStrikeSoundEffect;
            if (m_audioSource != null && criticalStrikeSound != null)
            {
                m_audioSource.PlayOneShot(criticalStrikeSound);
            }
        }

        /// <summary>
        /// Plays a random character-owned clip even when the persistent world
        /// sound manager is absent during direct Scene testing.
        /// </summary>
        protected bool PlayRandomSoundEffect(
            AudioClip[] soundEffects,
            float volumeScale = 1f)
        {
            if (m_audioSource == null)
            {
                return false;
            }

            WorldSoundFXManager soundManager = WorldSoundFXManager.Instance;
            if (soundManager != null)
            {
                return soundManager.PlaySoundEffect(
                    soundEffects,
                    m_audioSource,
                    volumeScale);
            }

            if (!WorldSoundFXManager.TrySelectRandomSoundEffect(
                    soundEffects,
                    out AudioClip soundEffect))
            {
                return false;
            }

            m_audioSource.PlayOneShot(
                soundEffect,
                Mathf.Clamp01(volumeScale));
            return true;
        }
    }
}
