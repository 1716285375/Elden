using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>
    /// Owns the local in-world Save Game modal presentation and save commands.
    /// </summary>
    public class PlayerUISaveGameManager : MonoBehaviour
    {
        [Header("MENU")]
        [SerializeField] private GameObject m_saveGameMenu;
        [SerializeField] private Button m_saveGameButton;
        [SerializeField] private Button m_returnToGameButton;
        [SerializeField] private TMP_Text m_characterNameText;
        [SerializeField] private TMP_Text m_saveDetailsText;
        [SerializeField] private TMP_Text m_feedbackText;

        private bool m_isInputBlockedByDeath;
        private bool m_isSaveGameMenuOpen;

        /// <summary>
        /// Gets whether the world Save Game menu currently owns local input.
        /// </summary>
        public bool IsSaveGameMenuOpen => m_isSaveGameMenuOpen;

        /// <summary>
        /// Prevents a dead local player from opening or writing the in-world Save Game menu.
        /// </summary>
        public void SetDeathInputBlocked(bool isBlocked)
        {
            m_isInputBlockedByDeath = isBlocked;
            if (isBlocked)
            {
                CloseSaveGameMenu();
            }
        }

        private void OnDisable()
        {
            CloseSaveGameMenu();
        }

        /// <summary>
        /// Opens the in-world Save Game menu and transfers local navigation input to it.
        /// </summary>
        public void OpenSaveGameMenu()
        {
            if (m_isInputBlockedByDeath ||
                m_isSaveGameMenuOpen ||
                SceneManager.GetActiveScene().buildIndex <= 0)
            {
                return;
            }

            PlayerUIManager.Instance?.CloseAllMenuWindows();
            m_isSaveGameMenuOpen = true;
            m_saveGameMenu?.SetActive(true);
            PlayerUIManager.Instance?.NotifyMenuWindowOpened();
            RefreshSaveGamePresentation();
        }

        /// <summary>
        /// Closes the in-world Save Game menu and returns input to the locally owned player.
        /// </summary>
        public void CloseSaveGameMenu()
        {
            if (!m_isSaveGameMenuOpen)
            {
                m_saveGameMenu?.SetActive(false);
                return;
            }

            m_isSaveGameMenuOpen = false;
            m_saveGameMenu?.SetActive(false);
            PlayerUIManager.Instance?.RefreshMenuWindowState();
        }

        /// <summary>
        /// Writes the active local character to disk and displays the result in the menu.
        /// </summary>
        public void SaveCurrentGame()
        {
            if (m_isInputBlockedByDeath)
            {
                return;
            }

            WorldSaveGameManager saveGameManager = WorldSaveGameManager.Instance;
            bool wasSaved = saveGameManager != null && saveGameManager.SaveGame();
            if (m_feedbackText != null)
            {
                m_feedbackText.text = wasSaved
                    ? "GAME SAVED"
                    : "SAVE UNAVAILABLE FOR THIS LOCAL PLAYER";
            }

            RefreshSaveGamePresentation(false);
        }

        private void RefreshSaveGamePresentation(bool shouldResetFeedback = true)
        {
            WorldSaveGameManager saveGameManager = WorldSaveGameManager.Instance;
            CharacterSaveData characterData = saveGameManager?.CurrentCharacterData;
            bool canSaveGame = saveGameManager != null && saveGameManager.CanSaveGame;
            if (m_characterNameText != null)
            {
                string characterName = characterData?.CharacterName;
                m_characterNameText.text = string.IsNullOrWhiteSpace(characterName)
                    ? "UNNAMED"
                    : characterName.ToUpperInvariant();
            }

            if (m_saveDetailsText != null)
            {
                m_saveDetailsText.text = BuildSaveDetails(saveGameManager, characterData);
            }

            if (m_saveGameButton != null)
            {
                m_saveGameButton.interactable = canSaveGame;
            }

            if (shouldResetFeedback && m_feedbackText != null)
            {
                m_feedbackText.text = canSaveGame
                    ? "SAVE THE CURRENT POSITION AND PLAY TIME"
                    : "NO WRITABLE LOCAL SAVE SLOT";
            }

            Button initialButton = canSaveGame ? m_saveGameButton : m_returnToGameButton;
            initialButton?.Select();
        }

        private static string BuildSaveDetails(
            WorldSaveGameManager saveGameManager,
            CharacterSaveData characterData)
        {
            if (saveGameManager == null ||
                characterData == null ||
                saveGameManager.CurrentCharacterSlot == CharacterSlot.NoSlot)
            {
                return "NO ACTIVE CHARACTER";
            }

            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(characterData.SecondsPlayed));
            int hours = totalSeconds / 3600;
            int minutes = totalSeconds % 3600 / 60;
            int seconds = totalSeconds % 60;
            return $"SLOT {(int)saveGameManager.CurrentCharacterSlot:00}   {hours:00}:{minutes:00}:{seconds:00}";
        }
    }
}
