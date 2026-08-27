namespace ZZ
{
    public class PlayerStatsManager : CharacterStatsManager
    {
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private int m_runes;

        private PlayerManager m_player;
        private PlayerUIHUDManager m_boundHUD;

        /// <summary>Gets this local player's private Rune balance.</summary>
        public int Runes => UnityEngine.Mathf.Max(0, m_runes);

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
        /// Connects this locally owned player's shared Health and Stamina state to the HUD.
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
            m_boundHUD.BindStats(CharacterNetworkManager);
            m_boundHUD.SetRuneCountImmediately(Runes);
        }

        /// <summary>Adds a positive Rune reward and starts the HUD pending presentation.</summary>
        public void AddRunes(int runesToAdd)
        {
            if (runesToAdd <= 0 || IsSpawned && !IsOwner)
            {
                return;
            }

            m_runes = CalculateRuneTotal(m_runes, runesToAdd);
            PlayerUIManager.Instance?.PlayerUIHUDManager?.SetRunesCount(
                runesToAdd);
        }

        /// <summary>Spends a non-negative Rune amount without earned-Rune feedback.</summary>
        public bool TrySpendRunes(int runesToSpend)
        {
            if (runesToSpend < 0 ||
                IsSpawned && !IsOwner ||
                runesToSpend > Runes)
            {
                return false;
            }

            m_runes -= runesToSpend;
            PlayerUIManager.Instance?.PlayerUIHUDManager
                ?.SetRuneCountImmediately(Runes);
            return true;
        }

        /// <summary>Loads a Rune balance without displaying it as a new reward.</summary>
        public void SetRunes(int runeCount)
        {
            if (IsSpawned && !IsOwner)
            {
                return;
            }

            m_runes = UnityEngine.Mathf.Max(0, runeCount);
            PlayerUIManager.Instance?.PlayerUIHUDManager
                ?.SetRuneCountImmediately(Runes);
        }

        /// <summary>Returns a non-negative, overflow-safe Rune balance.</summary>
        public static int CalculateRuneTotal(int currentRunes, int runesToAdd)
        {
            long total = (long)UnityEngine.Mathf.Max(0, currentRunes) +
                UnityEngine.Mathf.Max(0, runesToAdd);
            return total >= int.MaxValue ? int.MaxValue : (int)total;
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

            m_boundHUD.UnbindStats(CharacterNetworkManager);
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
