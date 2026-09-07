using UnityEngine;

namespace ZZ
{
    /// <summary>Plays player-owned sounds and exposes movement noise to server AI.</summary>
    public class PlayerSoundFXManager : CharacterSoundFXManager
    {
        private const float k_FootstepSoundRange = 2f;
        private const float k_WalkingFootstepInterval = 0.54f;
        private const float k_RunningFootstepInterval = 0.4f;
        private const float k_SprintingFootstepInterval = 0.3f;
        private const float k_MinimumFootstepMovement = 0.15f;

        private PlayerManager m_player;
        private float m_nextFootstepTime;
        private float m_remoteSpatialBlend = 1f;

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponentInParent<PlayerManager>();
            if (CharacterAudioSource != null)
            {
                m_remoteSpatialBlend = CharacterAudioSource.spatialBlend;
            }
        }

        private void Update()
        {
            UpdateAudioPerspective();
            PlayFootstepSoundEffect();
        }

        private void UpdateAudioPerspective()
        {
            if (m_player == null || !m_player.IsSpawned || CharacterAudioSource == null)
            {
                return;
            }

            // Camera collision and orbit must not change the loudness of the local player's own actions.
            CharacterAudioSource.spatialBlend = m_player.IsOwner ? 0f : m_remoteSpatialBlend;
        }

        /// <inheritdoc />
        public override void PlayFootstepSoundEffect()
        {
            if (!CanPlayFootstep())
            {
                return;
            }

            PlayerNetworkManager networkManager = m_player.PlayerNetworkManager;
            m_nextFootstepTime = Time.time + GetFootstepInterval(
                networkManager.MoveAmount.Value, networkManager.IsSprinting.Value);

            if (m_player?.IsClient == true)
            {
                base.PlayFootstepSoundEffect();
            }

            if (m_player?.IsServer == true)
            {
                WorldSoundFXManager.Instance?.AlertNearbyCharactersToSound(
                    transform.position,
                    k_FootstepSoundRange);
            }
        }

        /// <inheritdoc />
        public override void PlayBlockingSoundEffect()
        {
            WeaponItem blockingWeapon =
                m_player?.InventoryManager?.CurrentLeftHandWeapon;
            PlayRandomSoundEffect(blockingWeapon?.BlockingSoundEffects);
        }

        private bool CanPlayFootstep()
        {
            if (m_player == null ||
                !m_player.IsSpawned ||
                (!m_player.IsClient && !m_player.IsServer) ||
                m_player.IsDead ||
                !m_player.IsGrounded ||
                !m_player.CanMove ||
                m_player.IsPerformingAction ||
                m_player.ShouldApplyRootMotion ||
                m_player.PlayerNetworkManager == null ||
                m_player.PlayerNetworkManager.IsSneaking.Value ||
                m_player.PlayerNetworkManager.IsChargingAttack.Value ||
                m_player.PlayerNetworkManager.IsRolling.Value ||
                m_player.PlayerNetworkManager.IsJumping.Value ||
                m_player.PlayerNetworkManager.IsClimbingLadder.Value ||
                m_player.PlayerNetworkManager.MoveAmount.Value <
                    k_MinimumFootstepMovement)
            {
                m_nextFootstepTime = Time.time;
                return false;
            }

            return Time.time >= m_nextFootstepTime;
        }

        private static float GetFootstepInterval(
            float moveAmount,
            bool isSprinting)
        {
            if (isSprinting)
            {
                return k_SprintingFootstepInterval;
            }

            return moveAmount >= 0.75f
                ? k_RunningFootstepInterval
                : k_WalkingFootstepInterval;
        }
    }
}
