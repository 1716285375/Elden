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
        public bool IsMenuOpen => m_menuWindow != null && m_menuWindow.activeInHierarchy;

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
            if (!isActiveAndEnabled || m_menuWindow == null ||
                playerUIManager?.CanOpenMenuWindows != true)
            {
                return;
            }

            Transform menuParent = m_menuWindow.transform.parent;
            if (menuParent != null && !menuParent.gameObject.activeInHierarchy)
            {
                return;
            }

            playerUIManager.CloseAllMenuWindows();
            // Default buttons select themselves in OnEnable and need the EventSystem first.
            playerUIManager.NotifyMenuWindowOpened();
            m_menuWindow.SetActive(true);
            if (!IsMenuOpen)
            {
                playerUIManager.RefreshMenuWindowState();
            }
        }

        /// <summary>Closes this menu and releases input when no modal remains open.</summary>
        public virtual void CloseMenu()
        {
            m_menuWindow?.SetActive(false);
            PlayerUIManager.Instance?.RefreshMenuWindowState();
        }
    }
}
