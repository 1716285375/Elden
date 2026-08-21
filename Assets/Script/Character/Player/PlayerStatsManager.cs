namespace ZZ
{
    public class PlayerStatsManager : CharacterStatsManager
    {
        private PlayerManager m_player;
        private PlayerUIHUDManager m_boundHUD;

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponent<PlayerManager>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            BindLocalHUD();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            BindLocalHUD();
        }

        public override void OnLostOwnership()
        {
            UnbindLocalHUD();
            base.OnLostOwnership();
        }

        public override void OnNetworkDespawn()
        {
            UnbindLocalHUD();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Connects this locally owned player's shared Stamina state to the persistent HUD.
        /// </summary>
        public void BindLocalHUD()
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerUIHUDManager playerHUD = PlayerUIManager.Instance?.PlayerUIHUDManager;
            if (playerHUD == null || playerHUD == m_boundHUD)
            {
                return;
            }

            UnbindLocalHUD();
            m_boundHUD = playerHUD;
            m_boundHUD.BindStamina(CharacterNetworkManager);
        }

        /// <summary>
        /// Releases the HUD binding if it still represents this player.
        /// </summary>
        public void UnbindLocalHUD()
        {
            if (m_boundHUD == null)
            {
                return;
            }

            m_boundHUD.UnbindStamina(CharacterNetworkManager);
            m_boundHUD = null;
        }

        protected override bool IsStaminaRegenerationBlocked()
        {
            return base.IsStaminaRegenerationBlocked() ||
                m_player == null ||
                m_player.PlayerNetworkManager == null ||
                m_player.PlayerNetworkManager.IsSprinting.Value;
        }
    }
}
