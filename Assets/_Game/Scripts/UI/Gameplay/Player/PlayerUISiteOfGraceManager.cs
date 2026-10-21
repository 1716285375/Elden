using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Owns the local Site of Grace rest menu and Travel command.</summary>
    public class PlayerUISiteOfGraceManager : PlayerUIMenu
    {
        [Header("SITE OF GRACE MENU")]
        [SerializeField] private Button m_levelUpButton;
        [SerializeField] private Button m_travelButton;
        [SerializeField] private Button m_returnButton;

        private SiteOfGraceView m_view;

        public bool IsMemoryOpen => m_view?.IsMemoryOpen == true;

        /// <summary>Closes the nested spell page without closing the grace menu.</summary>
        public void CloseMemory()
        {
            m_view?.CloseMemory();
        }

        /// <summary>Gets whether the Site of Grace menu currently owns local input.</summary>
        public bool IsSiteOfGraceMenuOpen =>
            IsMenuOpen;

        /// <summary>Opens the rest menu after the locally owned player finishes resting.</summary>
        public void OpenSiteOfGraceMenu()
        {
            PlayerUIManager playerUIManager = PlayerUIManager.Instance;
            playerUIManager?.PlayerUIPopUpManager?.CloseAllPopUpWindows();
            OpenMenu();
            if (!IsMenuOpen)
            {
                return;
            }

            EnsureToolkitMenu();
            m_view?.Refresh();
        }

        /// <summary>Closes only the Site of Grace menu.</summary>
        public void CloseSiteOfGraceMenu()
        {
            CloseMenu();
        }

        /// <summary>Moves from the rest menu to the unlocked-location menu.</summary>
        public void OpenTeleportLocationMenu()
        {
            PlayerUIManager.Instance?.PlayerUITeleportLocationManager
                ?.OpenTeleportLocationMenu();
        }

        /// <summary>Moves from the rest menu into the Level Up preview.</summary>
        public void OpenLevelUpMenu()
        {
            if (IsMenuOpen)
            {
                PlayerUIManager.Instance?.PlayerUILevelUpManager?.OpenMenu();
            }
        }

        /// <summary>Moves from the rest menu into the player's persistent Storage.</summary>
        public void OpenStorageMenu()
        {
            if (IsMenuOpen)
            {
                PlayerUIManager.Instance?.PlayerUIStorageManager
                    ?.OpenStorageMenu();
            }
        }

        private void EnsureToolkitMenu()
        {
            if (m_view != null || MenuWindow == null || !Application.isPlaying)
            {
                return;
            }
            CanvasGroup legacy = MenuWindow.GetComponent<CanvasGroup>();
            if (legacy == null)
            {
                legacy = MenuWindow.AddComponent<CanvasGroup>();
            }
            legacy.alpha = 0f;
            legacy.interactable = false;
            legacy.blocksRaycasts = false;
            m_view = SiteOfGraceView.Create(MenuWindow.transform, this);
        }
    }
}
