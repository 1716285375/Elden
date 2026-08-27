using UnityEngine;

namespace ZZ
{
    /// <summary>Plays player-owned sounds and exposes movement noise to server AI.</summary>
    public class PlayerSoundFXManager : CharacterSoundFXManager
    {
        private const float k_FootstepSoundRange = 2f;
        private const float k_FootstepInterval = 0.55f;
        private const float k_MinimumFootstepMovement = 0.15f;

        private PlayerManager m_player;
        private float m_nextFootstepTime;

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponentInParent<PlayerManager>();
        }

        private void Update()
        {
            if (m_player == null ||
                !m_player.IsSpawned ||
                !m_player.IsServer ||
                m_player.IsDead ||
                m_player.PlayerNetworkManager == null ||
                m_player.PlayerNetworkManager.MoveAmount.Value <
                    k_MinimumFootstepMovement ||
                Time.time < m_nextFootstepTime)
            {
                return;
            }

            m_nextFootstepTime = Time.time + k_FootstepInterval;
            PlayFootstepSoundEffect();
        }

        /// <inheritdoc />
        public override void PlayFootstepSoundEffect()
        {
            base.PlayFootstepSoundEffect();
            WorldSoundFXManager.Instance?.AlertNearbyCharactersToSound(
                transform.position,
                k_FootstepSoundRange);
        }

        /// <inheritdoc />
        public override void PlayBlockingSoundEffect()
        {
            WeaponItem blockingWeapon =
                m_player?.InventoryManager?.CurrentLeftHandWeapon;
            WorldSoundFXManager.Instance?.PlaySoundEffect(
                blockingWeapon?.BlockingSoundEffects,
                CharacterAudioSource);
        }
    }
}
