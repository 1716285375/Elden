using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace ZZ
{
    public class PlayerUIManager : MonoBehaviour
    {
        private static PlayerUIManager s_instance;

        [Header("NETWORK JOIN")]
        [FormerlySerializedAs("startGameAsClient")]
        [SerializeField] private bool m_shouldStartAsClient;
        [SerializeField] private PlayerUIHUDManager m_playerUIHUDManager;
        [SerializeField] private PlayerUIBossHealthBar m_playerUIBossHealthBar;
        [SerializeField] private PlayerUISaveGameManager m_playerUISaveGameManager;
        [SerializeField] private PlayerUIPopUpManager m_playerUIPopUpManager;

        public static PlayerUIManager Instance => s_instance;
        public PlayerUIHUDManager PlayerUIHUDManager => m_playerUIHUDManager;

        /// <summary>Gets the persistent Boss encounter HUD.</summary>
        public PlayerUIBossHealthBar PlayerUIBossHealthBar => m_playerUIBossHealthBar;

        /// <summary>
        /// Gets the persistent local Save Game menu controller.
        /// </summary>
        public PlayerUISaveGameManager PlayerUISaveGameManager => m_playerUISaveGameManager;

        /// <summary>
        /// Gets the persistent local transient-message controller.
        /// </summary>
        public PlayerUIPopUpManager PlayerUIPopUpManager => m_playerUIPopUpManager;

        private void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
                m_playerUIHUDManager ??= GetComponentInChildren<PlayerUIHUDManager>(true);
                m_playerUIBossHealthBar ??=
                    GetComponentInChildren<PlayerUIBossHealthBar>(true);
                m_playerUISaveGameManager ??=
                    GetComponentInChildren<PlayerUISaveGameManager>(true);
                m_playerUIPopUpManager ??=
                    GetComponentInChildren<PlayerUIPopUpManager>(true);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (m_shouldStartAsClient)
            {
                m_shouldStartAsClient = false;
                NetworkManager.Singleton.Shutdown();

                NetworkManager.Singleton.StartClient();
            }
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
