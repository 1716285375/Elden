using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Owns unlocked Site of Grace navigation and local fast travel.</summary>
    public class PlayerUITeleportLocationManager : PlayerUIMenu
    {
        [Header("TELEPORT LOCATION MENU")]
        [SerializeField] private Button[] m_teleportLocationButtons =
            System.Array.Empty<Button>();
        [SerializeField] private int[] m_siteOfGraceIDs =
            System.Array.Empty<int>();
        [SerializeField] private Button m_returnButton;

        private TravelMenuView m_view;
        private int m_pendingDestination = -1;

        public bool IsConfirmationOpen => m_pendingDestination >= 0;

        /// <summary>Gets whether the unlocked-location menu currently owns input.</summary>
        public bool IsTeleportLocationMenuOpen =>
            IsMenuOpen;

        /// <summary>Gets whether the current session contains exactly one local player.</summary>
        public bool CanFastTravel => CanFastTravelInCurrentSession();

        /// <summary>Opens the travel menu when the active session is single-player.</summary>
        public void OpenTeleportLocationMenu()
        {
            if (!CanFastTravel)
            {
                return;
            }

            OpenMenu();
            if (!IsMenuOpen)
            {
                return;
            }

            EnsureToolkitMenu();
            RefreshUnlockedLocations();
        }

        /// <summary>Closes only the unlocked-location menu.</summary>
        public void CloseTeleportLocationMenu()
        {
            CloseMenu();
        }

        /// <summary>Teleports the local player to one unlocked, registered location.</summary>
        public void TeleportToSiteOfGrace(int siteOfGraceID)
        {
            if (!IsMenuOpen || !CanFastTravel || PlayerUIManager.Instance?.LocalPlayer == null ||
                PlayerUIManager.Instance.LocalPlayer.IsDead)
            {
                CancelTravel();
                m_view?.SetFeedback("当前无法传送，请检查角色状态与会话人数。");
                return;
            }
            CancelTravel();

            PlayerUILoadingScreenManager loadingScreenManager =
                PlayerUIManager.Instance?.PlayerUILoadingScreenManager;
            loadingScreenManager?.ActivateLoadingScreen();
            SiteOfGraceInteractable siteOfGrace =
                WorldObjectManager.Instance?.GetSiteOfGraceByID(siteOfGraceID);
            if (siteOfGrace == null ||
                !siteOfGrace.IsActivated ||
                !siteOfGrace.TeleportLocalPlayer())
            {
                loadingScreenManager?.DeactivateLoadingScreen(0f);
                RefreshUnlockedLocations();
                m_view?.SetFeedback("目的地已不可用，请重新选择赐福。");
                return;
            }

            PlayerUIManager.Instance?.CloseAllMenuWindows();
            loadingScreenManager?.DeactivateLoadingScreen();
        }

        /// <summary>Shows unlocked destinations and selects the first valid button.</summary>
        public void RefreshUnlockedLocations()
        {
            if (m_view != null)
            {
                m_view.RefreshLocations();
                return;
            }
            int buttonCount = Mathf.Min(
                m_teleportLocationButtons?.Length ?? 0,
                m_siteOfGraceIDs?.Length ?? 0);
            Button firstUnlockedButton = null;
            for (int index = 0; index < buttonCount; index++)
            {
                Button locationButton = m_teleportLocationButtons[index];
                SiteOfGraceInteractable siteOfGrace =
                    WorldObjectManager.Instance?.GetSiteOfGraceByID(
                        m_siteOfGraceIDs[index]);
                bool isUnlocked = IsLocationUnlocked(siteOfGrace);
                locationButton?.gameObject.SetActive(isUnlocked);
                if (firstUnlockedButton == null && isUnlocked)
                {
                    firstUnlockedButton = locationButton;
                }
            }

            for (int index = buttonCount;
                index < (m_teleportLocationButtons?.Length ?? 0);
                index++)
            {
                m_teleportLocationButtons[index]?.gameObject.SetActive(false);
            }

            Button initialButton = firstUnlockedButton ?? m_returnButton;
            initialButton?.Select();
            initialButton?.OnSelect(null);
        }

        /// <summary>Reviews an unlocked destination without moving the player.</summary>
        public void RequestTravel(int siteOfGraceID)
        {
            SiteOfGraceInteractable destination = WorldObjectManager.Instance?.GetSiteOfGraceByID(siteOfGraceID);
            if (!IsMenuOpen || IsConfirmationOpen || !CanFastTravel || !IsLocationUnlocked(destination) ||
                destination.TeleportTransform == null)
            {
                m_view?.SetFeedback("该目的地暂时无法传送。");
                return;
            }
            m_pendingDestination = siteOfGraceID;
            m_view?.ShowConfirmation(destination.DisplayName);
        }

        /// <summary>Revalidates and performs the reviewed destination exactly once.</summary>
        public void ConfirmTravel()
        {
            if (!IsConfirmationOpen)
            {
                return;
            }
            TeleportToSiteOfGrace(m_pendingDestination);
        }

        /// <summary>Cancels confirmation without changing position or closing the destination list.</summary>
        public void CancelTravel()
        {
            m_pendingDestination = -1;
            m_view?.HideConfirmation();
        }

        /// <summary>Returns to the grace commands without travelling.</summary>
        public void ReturnToSiteOfGrace()
        {
            CloseMenu();
            PlayerUIManager.Instance?.PlayerUISiteOfGraceManager?.OpenSiteOfGraceMenu();
        }

        /// <inheritdoc />
        public override void CloseMenu()
        {
            CancelTravel();
            base.CloseMenu();
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
            m_view = TravelMenuView.Create(MenuWindow.transform, this);
        }

        /// <summary>Returns whether one registered destination is unlocked.</summary>
        public static bool IsLocationUnlocked(
            SiteOfGraceInteractable siteOfGrace)
        {
            return siteOfGrace != null && siteOfGrace.IsActivated;
        }

        /// <summary>Returns whether fast travel is allowed for a player count.</summary>
        public static bool IsFastTravelAllowed(int playerCount)
        {
            return playerCount == 1;
        }

        private static bool CanFastTravelInCurrentSession()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            WorldGameSessionManager sessionManager =
                WorldGameSessionManager.Instance;
            return networkManager != null &&
                networkManager.IsListening &&
                networkManager.LocalClient?.PlayerObject != null &&
                sessionManager != null &&
                IsFastTravelAllowed(sessionManager.Players.Count);
        }
    }
}
