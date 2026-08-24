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

            WorldSoundFXManager.Instance?.PlaySoundEffect(
                weapon.Whooshes,
                m_audioSource);
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

            WorldSoundFXManager.Instance?.PlaySoundEffect(
                isRelease
                    ? weapon.ReleaseSoundEffects
                    : weapon.DrawSoundEffects,
                m_audioSource);
        }

        /// <summary>Plays one locally spatialized damage grunt for this character.</summary>
        public void PlayDamageGrunt()
        {
            WorldSoundFXManager.Instance?.PlaySoundEffect(
                m_damageGrunts,
                m_audioSource);
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
    }
}
