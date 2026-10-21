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
        private CharacterMenuView m_view;

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
                EnsureToolkitMenu();
                m_view.Focus("resume");
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

        private void EnsureToolkitMenu()
        {
            if (m_view != null || MenuWindow == null)
            {
                return;
            }
            // Serialized children remain until prefab migration; they neither render nor receive input.
            CanvasGroup legacy = MenuWindow.GetComponent<CanvasGroup>();
            if (legacy == null)
            {
                legacy = MenuWindow.AddComponent<CanvasGroup>();
            }
            legacy.alpha = 0f;
            legacy.interactable = false;
            legacy.blocksRaycasts = false;
            m_view = CharacterMenuView.Create(MenuWindow.transform, this);
        }

        /// <summary>Opens Toolkit settings while retaining this menu's gameplay input ownership.</summary>
        public void OpenSettings()
        {
            if (!IsMenuOpen)
            {
                return;
            }
            if (m_settingsPanel == null)
            {
                m_settingsPanel = GameSettingsPanel.Create(MenuWindow.transform);
            }
            m_settingsPanel.Open(() => m_view.Focus("settings"));
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
            if (GameSettingsPanel.ClosedThisFrame ||
                m_settingsPanel != null && m_settingsPanel.gameObject.activeInHierarchy)
            {
                return;
            }
            if (m_view != null && m_view.IsConfirmationOpen)
            {
                m_view.CancelExit();
                return;
            }
            if (m_view != null && m_view.IsStatusOpen)
            {
                m_view.CloseStatus();
                return;
            }
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
                OnCloseMenuPerformed(context);
                return;
            }

            OpenCharacterMenu();
        }

        private void OnCloseMenuPerformed(InputAction.CallbackContext context)
        {
            if (PlayerUIManager.Instance?.PlayerUILoadingScreenManager?.IsLoadingScreenActive == true)
            {
                return;
            }
            if (GameSettingsPanel.ClosedThisFrame ||
                m_settingsPanel != null && m_settingsPanel.gameObject.activeInHierarchy)
            {
                // Toolkit owns cancel while the overlay is open, including nested dropdown popups.
                return;
            }
            if (m_view != null && m_view.IsConfirmationOpen)
            {
                m_view.CancelExit();
                return;
            }
            if (m_view != null && m_view.IsStatusOpen)
            {
                m_view.CloseStatus();
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

            if (playerUIManager.PlayerUISiteOfGraceManager?.IsMemoryOpen == true)
            {
                playerUIManager.PlayerUISiteOfGraceManager.CloseMemory();
                return;
            }

            PlayerUITeleportLocationManager travel = playerUIManager.PlayerUITeleportLocationManager;
            if (playerUIManager.PlayerUIInventoryManager?.IsMenuOpen == true)
            {
                playerUIManager.PlayerUIInventoryManager.ReturnToCharacterMenu();
                return;
            }
            if (playerUIManager.PlayerUISaveGameManager?.IsSaveGameMenuOpen == true)
            {
                playerUIManager.PlayerUISaveGameManager.ReturnToCharacterMenu();
                return;
            }
            if (travel?.IsMenuOpen == true)
            {
                if (travel.IsConfirmationOpen)
                {
                    travel.CancelTravel();
                }
                else
                {
                    travel.ReturnToSiteOfGrace();
                }
                return;
            }

            PlayerUILevelUpManager levelUp = playerUIManager.PlayerUILevelUpManager;
            if (levelUp?.IsMenuOpen == true)
            {
                if (levelUp.IsConfirmationOpen)
                {
                    levelUp.CancelConfirmation();
                }
                else
                {
                    levelUp.ReturnToSiteOfGrace();
                }
                return;
            }

            PlayerUIStorageManager storage = playerUIManager.PlayerUIStorageManager;
            if (storage?.IsMenuOpen == true)
            {
                storage.ReturnToSiteOfGrace();
                return;
            }

            PlayerUIShopManager shop = playerUIManager.PlayerUIShopManager;
            if (shop?.IsMenuOpen == true)
            {
                shop.NavigateBack();
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
