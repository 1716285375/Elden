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

            bool canFastTravel =
                playerUIManager.PlayerUITeleportLocationManager
                    ?.CanFastTravel == true;
            if (m_travelButton != null)
            {
                m_travelButton.interactable = canFastTravel;
            }

            Button initialButton = m_levelUpButton != null
                ? m_levelUpButton
                : canFastTravel
                    ? m_travelButton
                    : m_returnButton;
            initialButton?.Select();
            initialButton?.OnSelect(null);
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
    }
}
