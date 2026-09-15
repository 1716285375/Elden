using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Owns Character Menu lifecycle and the persistent menu open/close input.</summary>
    public class PlayerUICharacterMenuManager : PlayerUIMenu
    {
        private const string k_OpenCharacterMenuActionName =
            "Open Character Menu";
        private const string k_CloseMenuActionName = "Close Menu";

        [SerializeField] private Button m_initialButton;

        private PlayerControls m_playerControls;
        private InputAction m_openCharacterMenuAction;
        private InputAction m_closeMenuAction;
        private Coroutine m_delayedCloseRoutine;
        private GameSettingsPanel m_settingsPanel;
        private Button m_settingsButton;

        public bool IsCharacterMenuOpen => IsMenuOpen;

        private void OnEnable()
        {
            m_playerControls ??= new PlayerControls();
            InputActionMap uiActions = m_playerControls.UI.Get();
            m_openCharacterMenuAction = uiActions.FindAction(
                k_OpenCharacterMenuActionName,
                true);
            m_closeMenuAction = uiActions.FindAction(k_CloseMenuActionName, true);
            m_openCharacterMenuAction.performed += OnOpenCharacterMenuPerformed;
            m_closeMenuAction.performed += OnCloseMenuPerformed;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            RefreshMenuInput(SceneManager.GetActiveScene());
        }

        protected override void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (m_openCharacterMenuAction != null)
            {
                m_openCharacterMenuAction.performed -=
                    OnOpenCharacterMenuPerformed;
            }

            if (m_closeMenuAction != null)
            {
                m_closeMenuAction.performed -= OnCloseMenuPerformed;
            }

            m_playerControls?.UI.Disable();
            CancelDelayedClose();
            base.OnDisable();
        }

        private void OnDestroy()
        {
            m_playerControls?.Dispose();
        }

        /// <summary>Opens the Character Menu after closing other transient UI.</summary>
        public void OpenCharacterMenu()
        {
            CancelDelayedClose();
            PlayerUIManager.Instance?.PlayerUIPopUpManager
                ?.CloseAllPopUpWindows();
            OpenMenu();
            if (IsMenuOpen)
            {
                EnsureSettingsButton();
                m_initialButton?.Select();
            }
        }

        /// <summary>Closes only the Character Menu window.</summary>
        public void CloseCharacterMenu()
        {
            CloseMenu();
        }

        /// <summary>Closes every menu and returns to the front-end Scene.</summary>
        public void ReturnToMainMenu()
        {
            CancelDelayedClose();
            PlayerUIManager.Instance?.CloseAllMenuWindows();
            WorldSaveGameManager.Instance?.ReturnToMainMenu();
        }

        /// <summary>Exits the application (stops Play Mode in the Editor).</summary>
        public void QuitGame()
        {
            GameExit.Quit();
        }

        /// <inheritdoc />
        public override void CloseMenu()
        {
            CancelDelayedClose();
            m_settingsPanel?.gameObject.SetActive(false);
            base.CloseMenu();
        }

        private void EnsureSettingsButton()
        {
            if (m_settingsButton != null || MenuWindow == null)
            {
                return;
            }
            m_settingsButton = GameSettingsPanel.CreateButton(MenuWindow.transform, "Settings", OpenSettings);
            RectTransform rect = m_settingsButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.77f, 0.87f);
            rect.anchorMax = new Vector2(0.94f, 0.94f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            m_settingsButton.GetComponent<LayoutElement>().ignoreLayout = true;
        }

        private void OpenSettings()
        {
            if (m_settingsPanel == null)
            {
                m_settingsPanel = GameSettingsPanel.Create(MenuWindow.transform);
            }
            m_settingsPanel.Open(() => m_settingsButton.Select());
        }

        /// <summary>
        /// Defers a UI-confirm close past one physics tick so it cannot become gameplay input.
        /// </summary>
        public void CloseAllMenuWindowsAfterFixedUpdate()
        {
            CancelDelayedClose();
            m_delayedCloseRoutine = StartCoroutine(
                CloseAllMenuWindowsAfterFixedUpdateRoutine());
        }

        private IEnumerator CloseAllMenuWindowsAfterFixedUpdateRoutine()
        {
            yield return new WaitForFixedUpdate();
            m_delayedCloseRoutine = null;
            PlayerUIManager.Instance?.CloseAllMenuWindows();
        }

        private void OnOpenCharacterMenuPerformed(
            InputAction.CallbackContext context)
        {
            if (TryCancelUpgradeConfirmation())
            {
                return;
            }
            PlayerUIManager playerUIManager = PlayerUIManager.Instance;
            if (playerUIManager == null)
            {
                return;
            }

            if (playerUIManager.IsMenuWindowOpen)
            {
                playerUIManager.CloseAllMenuWindows();
                return;
            }

            OpenCharacterMenu();
        }

        private void OnCloseMenuPerformed(InputAction.CallbackContext context)
        {
            if (m_settingsPanel != null && m_settingsPanel.gameObject.activeInHierarchy)
            {
                m_settingsPanel.Close();
                return;
            }
            if (TryCancelUpgradeConfirmation())
            {
                return;
            }
            PlayerUIManager playerUIManager = PlayerUIManager.Instance;
            if (playerUIManager?.IsMenuWindowOpen != true)
            {
                return;
            }

            PlayerUIEquipmentManager equipmentManager = playerUIManager.PlayerUIEquipmentManager;
            if (equipmentManager?.IsEquipmentMenuOpen == true &&
                equipmentManager.IsEquipmentInventoryOpen)
            {
                equipmentManager.CloseEquipmentInventoryWindow();
                return;
            }

            playerUIManager.CloseAllMenuWindows();
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene activeScene)
        {
            RefreshMenuInput(activeScene);
        }

        private static bool TryCancelUpgradeConfirmation()
        {
            PlayerUIWeaponUpgradeManager upgrade = PlayerUIManager.Instance?.PlayerUIWeaponUpgradeManager;
            if (upgrade?.IsConfirmationOpen != true)
            {
                return false;
            }
            upgrade.CancelUpgradeWeapon();
            return true;
        }

        private void RefreshMenuInput(Scene activeScene)
        {
            if (activeScene.buildIndex > 0)
            {
                m_playerControls?.UI.Enable();
                return;
            }

            PlayerUIManager.Instance?.CloseAllMenuWindows();
            m_playerControls?.UI.Disable();
        }

        private void CancelDelayedClose()
        {
            if (m_delayedCloseRoutine == null)
            {
                return;
            }

            StopCoroutine(m_delayedCloseRoutine);
            m_delayedCloseRoutine = null;
        }
    }
}
