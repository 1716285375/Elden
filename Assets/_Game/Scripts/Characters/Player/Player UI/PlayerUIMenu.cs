using UnityEngine;
using UnityEngine.Serialization;

namespace ZZ
{
    /// <summary>Provides one lifecycle boundary for persistent modal player menus.</summary>
    public abstract class PlayerUIMenu : MonoBehaviour
    {
        [FormerlySerializedAs("m_characterMenu")]
        [FormerlySerializedAs("m_equipmentMenu")]
        [FormerlySerializedAs("m_siteOfGraceMenu")]
        [FormerlySerializedAs("m_teleportLocationMenu")]
        [SerializeField] private GameObject m_menuWindow;

        /// <summary>Gets whether this modal menu currently owns local UI input.</summary>
        public bool IsMenuOpen => m_menuWindow?.activeSelf == true;

        protected GameObject MenuWindow => m_menuWindow;

        /// <summary>Assigns a menu root created by a reusable runtime UI factory.</summary>
        protected void SetMenuWindow(GameObject menuWindow)
        {
            m_menuWindow = menuWindow;
        }

        protected virtual void OnDisable()
        {
            CloseMenu();
        }

        /// <summary>Closes competing windows and transfers local input to this menu.</summary>
        public virtual void OpenMenu()
        {
            PlayerUIManager playerUIManager = PlayerUIManager.Instance;
            if (playerUIManager?.CanOpenMenuWindows != true)
            {
                return;
            }

            playerUIManager.CloseAllMenuWindows();
            m_menuWindow?.SetActive(true);
            playerUIManager.NotifyMenuWindowOpened();
        }

        /// <summary>Closes this menu and releases input when no modal remains open.</summary>
        public virtual void CloseMenu()
        {
            m_menuWindow?.SetActive(false);
            PlayerUIManager.Instance?.RefreshMenuWindowState();
        }
    }
}
