using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Owns unlocked Site of Grace navigation and local fast travel.</summary>
    public class PlayerUITeleportLocationManager : MonoBehaviour
    {
        [Header("TELEPORT LOCATION MENU")]
        [SerializeField] private GameObject m_teleportLocationMenu;
        [SerializeField] private Button[] m_teleportLocationButtons =
            System.Array.Empty<Button>();
        [SerializeField] private int[] m_siteOfGraceIDs =
            System.Array.Empty<int>();
        [SerializeField] private Button m_returnButton;

        /// <summary>Gets whether the unlocked-location menu currently owns input.</summary>
        public bool IsTeleportLocationMenuOpen =>
            m_teleportLocationMenu?.activeSelf == true;

        /// <summary>Gets whether the current session contains exactly one local player.</summary>
        public bool CanFastTravel => CanFastTravelInCurrentSession();

        private void OnDisable()
        {
            CloseTeleportLocationMenu();
        }

        /// <summary>Opens the travel menu when the active session is single-player.</summary>
        public void OpenTeleportLocationMenu()
        {
            PlayerUIManager playerUIManager = PlayerUIManager.Instance;
            if (playerUIManager?.CanOpenMenuWindows != true || !CanFastTravel)
            {
                return;
            }

            playerUIManager.CloseAllMenuWindows();
            m_teleportLocationMenu?.SetActive(true);
            playerUIManager.NotifyMenuWindowOpened();
            RefreshUnlockedLocations();
        }

        /// <summary>Closes only the unlocked-location menu.</summary>
        public void CloseTeleportLocationMenu()
        {
            m_teleportLocationMenu?.SetActive(false);
            PlayerUIManager.Instance?.RefreshMenuWindowState();
        }

        /// <summary>Teleports the local player to one unlocked, registered location.</summary>
        public void TeleportToSiteOfGrace(int siteOfGraceID)
        {
            if (!CanFastTravel)
            {
                return;
            }

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
                return;
            }

            PlayerUIManager.Instance?.CloseAllMenuWindows();
            loadingScreenManager?.DeactivateLoadingScreen();
        }

        /// <summary>Shows unlocked destinations and selects the first valid button.</summary>
        public void RefreshUnlockedLocations()
        {
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
