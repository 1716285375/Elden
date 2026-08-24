using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

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
        [SerializeField] private PlayerUICharacterMenuManager
            m_playerUICharacterMenuManager;
        [SerializeField] private PlayerUIEquipmentManager m_playerUIEquipmentManager;
        [SerializeField] private GameObject m_menuEventSystem;

        private bool m_isMenuWindowOpen;
        private bool m_isMenuInputBlocked;
        private bool m_isInternalEventSystemActive;
        private bool m_wasCursorVisible;
        private CursorLockMode m_previousCursorLockMode;

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

        /// <summary>Gets the local Character Menu controller.</summary>
        public PlayerUICharacterMenuManager PlayerUICharacterMenuManager =>
            m_playerUICharacterMenuManager;

        /// <summary>Gets the local Equipment Menu controller.</summary>
        public PlayerUIEquipmentManager PlayerUIEquipmentManager =>
            m_playerUIEquipmentManager;

        /// <summary>Gets whether any modal player menu currently owns UI input.</summary>
        public bool IsMenuWindowOpen => m_isMenuWindowOpen;

        /// <summary>Gets whether the active Scene and player state allow menus.</summary>
        public bool CanOpenMenuWindows =>
            !m_isMenuInputBlocked && SceneManager.GetActiveScene().buildIndex > 0;

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
                m_playerUICharacterMenuManager ??=
                    GetComponentInChildren<PlayerUICharacterMenuManager>(true);
                m_playerUIEquipmentManager ??=
                    GetComponentInChildren<PlayerUIEquipmentManager>(true);
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

        /// <summary>Closes every modal menu through one shared ownership boundary.</summary>
        public void CloseAllMenuWindows()
        {
            m_playerUICharacterMenuManager?.CloseCharacterMenu();
            m_playerUIEquipmentManager?.CloseEquipmentManagerMenu();
            m_playerUISaveGameManager?.CloseSaveGameMenu();
            ReleaseMenuInput();
        }

        /// <summary>Blocks menus for death or another higher-priority local state.</summary>
        public void SetMenuInputBlocked(bool isBlocked)
        {
            m_isMenuInputBlocked = isBlocked;
            if (isBlocked)
            {
                CloseAllMenuWindows();
            }
        }

        /// <summary>Transfers gameplay, cursor, and navigation input to a modal menu.</summary>
        public void NotifyMenuWindowOpened()
        {
            if (m_isMenuWindowOpen)
            {
                return;
            }

            m_isMenuWindowOpen = true;
            m_previousCursorLockMode = Cursor.lockState;
            m_wasCursorVisible = Cursor.visible;
            PlayerInputManager.Instance?.BlockGameplayInput();
            ActivateMenuEventSystem();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Releases modal input only after every menu window has closed.</summary>
        public void RefreshMenuWindowState()
        {
            bool hasOpenMenu =
                m_playerUICharacterMenuManager?.IsCharacterMenuOpen == true ||
                m_playerUIEquipmentManager?.IsEquipmentMenuOpen == true ||
                m_playerUISaveGameManager?.IsSaveGameMenuOpen == true;
            if (hasOpenMenu)
            {
                NotifyMenuWindowOpened();
                return;
            }

            ReleaseMenuInput();
        }

        private void ActivateMenuEventSystem()
        {
            if (EventSystem.current != null || m_menuEventSystem == null)
            {
                return;
            }

            m_menuEventSystem.SetActive(true);
            m_isInternalEventSystemActive = true;
        }

        private void ReleaseMenuInput()
        {
            if (!m_isMenuWindowOpen)
            {
                return;
            }

            m_isMenuWindowOpen = false;
            EventSystem.current?.SetSelectedGameObject(null);
            if (m_isInternalEventSystemActive)
            {
                m_menuEventSystem?.SetActive(false);
                m_isInternalEventSystemActive = false;
            }

            PlayerInputManager.Instance?.UnblockGameplayInput();
            Cursor.lockState = m_previousCursorLockMode;
            Cursor.visible = m_wasCursorVisible;
        }
    }
}
