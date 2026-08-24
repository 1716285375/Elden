using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Owns the local Site of Grace rest menu and Travel command.</summary>
    public class PlayerUISiteOfGraceManager : MonoBehaviour
    {
        [Header("SITE OF GRACE MENU")]
        [SerializeField] private GameObject m_siteOfGraceMenu;
        [SerializeField] private Button m_travelButton;
        [SerializeField] private Button m_returnButton;

        /// <summary>Gets whether the Site of Grace menu currently owns local input.</summary>
        public bool IsSiteOfGraceMenuOpen =>
            m_siteOfGraceMenu?.activeSelf == true;

        private void OnDisable()
        {
            CloseSiteOfGraceMenu();
        }

        /// <summary>Opens the rest menu after the locally owned player finishes resting.</summary>
        public void OpenSiteOfGraceMenu()
        {
            PlayerUIManager playerUIManager = PlayerUIManager.Instance;
            if (playerUIManager?.CanOpenMenuWindows != true)
            {
                return;
            }

            playerUIManager.PlayerUIPopUpManager?.CloseAllPopUpWindows();
            playerUIManager.CloseAllMenuWindows();
            m_siteOfGraceMenu?.SetActive(true);
            playerUIManager.NotifyMenuWindowOpened();

            bool canFastTravel =
                playerUIManager.PlayerUITeleportLocationManager
                    ?.CanFastTravel == true;
            if (m_travelButton != null)
            {
                m_travelButton.interactable = canFastTravel;
            }

            Button initialButton = canFastTravel
                ? m_travelButton
                : m_returnButton;
            initialButton?.Select();
            initialButton?.OnSelect(null);
        }

        /// <summary>Closes only the Site of Grace menu.</summary>
        public void CloseSiteOfGraceMenu()
        {
            m_siteOfGraceMenu?.SetActive(false);
            PlayerUIManager.Instance?.RefreshMenuWindowState();
        }

        /// <summary>Moves from the rest menu to the unlocked-location menu.</summary>
        public void OpenTeleportLocationMenu()
        {
            PlayerUIManager.Instance?.PlayerUITeleportLocationManager
                ?.OpenTeleportLocationMenu();
        }
    }
}
