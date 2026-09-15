using UnityEngine;

namespace ZZ
{
    /// <summary>Owns GuardController runtime state independently of the combat facade.</summary>
    public sealed class GuardController
    {
        private readonly PlayerManager m_player;

        public GuardController(PlayerManager player)
        {
            m_player = player;
        }

        private float m_counterExpiresAt = float.NegativeInfinity;
        public bool CanCounter => Time.time < m_counterExpiresAt && m_player != null && !m_player.IsDead &&
            m_player.CharacterNetworkManager.CurrentStamina.Value > 0f;

        public void RecordSuccessfulBlock()
        {
            m_counterExpiresAt = Time.time + 1f;
        }

        public void ClearCounter() => m_counterExpiresAt = float.NegativeInfinity;

        private bool m_canBlock = true;
        public bool CanBlock => m_canBlock;

        public bool SetBlocking(bool isBlocking, WeaponItem blockingWeapon = null)
        {
            CharacterNetworkManager networkManager =
                m_player?.CharacterNetworkManager;
            if (m_player == null ||
                !m_player.IsOwner ||
                networkManager == null ||
                !networkManager.IsSpawned)
            {
                return false;
            }

            if (!isBlocking)
            {
                networkManager.SetBlockingState(false);
                m_player?.CharacterCombatManager?.Intent.Set(CombatIntent.Guard, false);
                return true;
            }

            if (!m_canBlock ||
                m_player.IsDead ||
                m_player.IsPerformingAction ||
                networkManager.IsAttacking.Value ||
                networkManager.IsBlocking.Value ||
                blockingWeapon == null)
            {
                return false;
            }

            networkManager.SetSneakingState(false);
            m_player.PlayerStatsManager?.SetBlockingStats(blockingWeapon);
            bool isRightHandBlock =
                m_player.PlayerNetworkManager?.IsTwoHandingRightWeapon.Value == true;
            m_player.PlayerNetworkManager?.SetCharacterActionHand(isRightHandBlock);
            networkManager.SetBlockingState(true);
            m_player?.CharacterCombatManager?.Intent.Set(CombatIntent.Guard, true);
            return true;
        }

        public void SetCanBlock(bool canBlock)
        {
            m_canBlock = canBlock;
            if (!m_canBlock)
            {
                SetBlocking(false);
            }
        }
    }
}
