using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Selects and plays shared one-shot sound effects without coupling gameplay to clips.
    /// </summary>
    public class WorldSoundFXManager : MonoBehaviour
    {
        private const string k_CharacterLayerName = "Damageable Character";
        private const int k_MaximumSoundColliders = 64;

        private static WorldSoundFXManager s_instance;

        private readonly Collider[] m_soundColliders =
            new Collider[k_MaximumSoundColliders];
        private readonly HashSet<AICharacterManager> m_alertedCharacters = new();

        [SerializeField] private AudioClip m_rollingSoundFX;
        [SerializeField] private AudioClip m_pickupItemSoundEffect;
        [SerializeField] private AudioClip m_stanceBreakSoundEffect;
        [SerializeField] private AudioClip m_criticalStrikeSoundEffect;

        [Header("Quick Slot Sounds")]
        [SerializeField] private AudioClip m_flaskRestorationSoundEffect;
        [SerializeField] private AudioClip m_emptyFlaskSoundEffect;

        public static WorldSoundFXManager Instance => s_instance;
        public AudioClip RollingSoundFX => m_rollingSoundFX;
        public AudioClip PickupItemSoundEffect => m_pickupItemSoundEffect;
        public AudioClip StanceBreakSoundEffect => m_stanceBreakSoundEffect;
        public AudioClip CriticalStrikeSoundEffect =>
            m_criticalStrikeSoundEffect;
        public AudioClip FlaskRestorationSoundEffect =>
            m_flaskRestorationSoundEffect;
        public AudioClip EmptyFlaskSoundEffect => m_emptyFlaskSoundEffect;

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

            audioSource.PlayOneShot(soundEffect,
                Mathf.Clamp01(volumeScale) * SoundEffectVolume.GetVolumeScale(soundEffects, soundEffect));
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

        /// <summary>Alerts each living server AI within range exactly once.</summary>
        public int AlertNearbyCharactersToSound(
            Vector3 soundPosition,
            float soundRange)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.IsListening ||
                !networkManager.IsServer ||
                soundRange <= 0f)
            {
                return 0;
            }

            int characterLayerMask = LayerMask.GetMask(k_CharacterLayerName);
            if (characterLayerMask == 0)
            {
                return 0;
            }

            int colliderCount = Physics.OverlapSphereNonAlloc(
                soundPosition,
                soundRange,
                m_soundColliders,
                characterLayerMask,
                QueryTriggerInteraction.Collide);
            m_alertedCharacters.Clear();
            int alertedCount = 0;
            for (int colliderIndex = 0;
                colliderIndex < colliderCount;
                colliderIndex++)
            {
                Collider characterCollider = m_soundColliders[colliderIndex];
                AICharacterManager aiCharacter = characterCollider != null
                    ? characterCollider.GetComponentInParent<AICharacterManager>()
                    : null;
                if (aiCharacter == null ||
                    aiCharacter.IsDead ||
                    !m_alertedCharacters.Add(aiCharacter))
                {
                    continue;
                }

                if (aiCharacter.GetComponent<AICharacterCombatManager>()
                    ?.AlertCharacterToSound(soundPosition) == true)
                {
                    alertedCount++;
                }
            }

            return alertedCount;
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
