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

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponentInParent<PlayerManager>();
        }

        private void Update()
        {
            if (!CanPlayFootstep())
            {
                return;
            }

            PlayerNetworkManager networkManager =
                m_player.PlayerNetworkManager;
            m_nextFootstepTime = Time.time + GetFootstepInterval(
                networkManager.MoveAmount.Value,
                networkManager.IsSprinting.Value);
            PlayFootstepSoundEffect();
        }

        /// <inheritdoc />
        public override void PlayFootstepSoundEffect()
        {
            if (m_player?.PlayerNetworkManager?.IsSneaking.Value == true)
            {
                return;
            }

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
                m_player.PlayerNetworkManager == null ||
                m_player.PlayerNetworkManager.IsSneaking.Value ||
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
