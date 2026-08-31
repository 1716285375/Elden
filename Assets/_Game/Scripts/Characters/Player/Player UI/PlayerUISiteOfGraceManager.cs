using TMPro;
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

        private Button m_storageButton;

        /// <summary>Gets whether the Site of Grace menu currently owns local input.</summary>
        public bool IsSiteOfGraceMenuOpen =>
            IsMenuOpen;

        private void Awake()
        {
            EnsureStorageButton();
        }

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

            if (m_storageButton != null)
            {
                m_storageButton.interactable =
                    playerUIManager.LocalPlayer?.InventoryManager != null;
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

        /// <summary>Moves from the rest menu into the player's persistent Storage.</summary>
        public void OpenStorageMenu()
        {
            if (IsMenuOpen)
            {
                PlayerUIManager.Instance?.PlayerUIStorageManager
                    ?.OpenStorageMenu();
            }
        }

        private void EnsureStorageButton()
        {
            if (m_storageButton != null || m_levelUpButton == null)
            {
                return;
            }

            m_storageButton = Instantiate(
                m_levelUpButton,
                m_levelUpButton.transform.parent);
            m_storageButton.name = "Storage Button";
            m_storageButton.onClick = new Button.ButtonClickedEvent();
            m_storageButton.onClick.AddListener(OpenStorageMenu);
            m_storageButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Automatic
            };
            TMP_Text tmpLabel =
                m_storageButton.GetComponentInChildren<TMP_Text>(true);
            if (tmpLabel != null)
            {
                tmpLabel.text = "Storage";
            }
            else
            {
                Text legacyLabel =
                    m_storageButton.GetComponentInChildren<Text>(true);
                if (legacyLabel != null)
                {
                    legacyLabel.text = "Storage";
                }
            }

            int targetIndex = m_travelButton != null
                ? m_travelButton.transform.GetSiblingIndex()
                : m_levelUpButton.transform.GetSiblingIndex() + 1;
            m_storageButton.transform.SetSiblingIndex(targetIndex);
        }
    }
}
