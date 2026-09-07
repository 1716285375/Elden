using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZZ
{
    public class PlayerUIManager : MonoBehaviour
    {
        private static PlayerUIManager s_instance;

        private const string k_TitleSceneName = "SCN_MainMenu";

        [Header("NETWORK JOIN")]
        [FormerlySerializedAs("startGameAsClient")]
        [SerializeField] private bool m_shouldStartAsClient;
        [SerializeField] private PlayerUIHUDManager m_playerUIHUDManager;
        [SerializeField] private PlayerUIBossHealthBar m_playerUIBossHealthBar;
        [SerializeField] private PlayerUISaveGameManager m_playerUISaveGameManager;
        [SerializeField] private PlayerUIPopUpManager m_playerUIPopUpManager;
        [SerializeField] private PlayerUICharacterMenuManager
            m_playerUICharacterMenuManager;
        [SerializeField] private PlayerUIEquipmentManager m_playerUIEquipmentManager;
        [SerializeField] private PlayerUISiteOfGraceManager
            m_playerUISiteOfGraceManager;
        [SerializeField] private PlayerUITeleportLocationManager
            m_playerUITeleportLocationManager;
        [SerializeField] private PlayerUILevelUpManager m_playerUILevelUpManager;
        [SerializeField] private PlayerUIWeaponUpgradeManager
            m_playerUIWeaponUpgradeManager;
        [SerializeField] private PlayerUIShopManager m_playerUIShopManager;
        [SerializeField] private PlayerUIStorageManager m_playerUIStorageManager;
        [SerializeField] private PlayerUILoadingScreenManager
            m_playerUILoadingScreenManager;
        [SerializeField] private GameObject m_menuEventSystem;
        [SerializeField] private FrontendSelectableVisual m_gameplayButtonStyle;

        [Header("UI SOUNDS")]
        [SerializeField] private AudioSource m_uiAudioSource;
        [SerializeField] private AudioClip m_menuHoverSound;
        [SerializeField] private AudioClip m_menuConfirmSound;
        [SerializeField] private AudioClip m_unableToContinueSound;

        [Header("UI CURSOR")]
        [SerializeField] private Texture2D m_uiCursorTexture;
        [SerializeField] private Vector2 m_uiCursorHotspot = new(16f, 16f);
        [Range(16, 128)] [SerializeField] private int m_uiCursorPixelWidth = 32;

        private Texture2D m_scaledUiCursor;

        private bool m_isMenuWindowOpen;
        private bool m_isMenuInputBlocked;
        private bool m_isInternalEventSystemActive;
        private bool m_lastCursorHiddenState;
        private bool m_hasMenuCursorApplied;
        private GameObject m_lastSelectedGameObject;
        private GameObject m_lastHoveredUiObject;
        private float m_lastHoverSoundTime = float.NegativeInfinity;
        private float m_lastConfirmSoundTime = float.NegativeInfinity;
        private PointerEventData m_pointerEventData;
        private EventSystem m_pointerEventSystem;
        private readonly List<RaycastResult> m_uiRaycastResults = new();

        public static PlayerUIManager Instance => s_instance;
        public PlayerUIHUDManager PlayerUIHUDManager => m_playerUIHUDManager;

        /// <summary>Gets the locally owned player cached for persistent UI consumers.</summary>
        public PlayerManager LocalPlayer { get; private set; }

        /// <summary>Gets the persistent Boss encounter HUD.</summary>
        public PlayerUIBossHealthBar PlayerUIBossHealthBar => m_playerUIBossHealthBar;

        /// <summary>
        /// Gets the persistent local Save Game menu controller.
        /// </summary>
        public PlayerUISaveGameManager PlayerUISaveGameManager => m_playerUISaveGameManager;

        /// <summary>
        /// Gets the persistent local transient-message controller.
        /// </summary>
        public PlayerUIPopUpManager PlayerUIPopUpManager => m_playerUIPopUpManager;

        /// <summary>Gets the local Character Menu controller.</summary>
        public PlayerUICharacterMenuManager PlayerUICharacterMenuManager =>
            m_playerUICharacterMenuManager;

        /// <summary>Gets the local Equipment Menu controller.</summary>
        public PlayerUIEquipmentManager PlayerUIEquipmentManager =>
            m_playerUIEquipmentManager;

        /// <summary>Gets the local Site of Grace rest-menu controller.</summary>
        public PlayerUISiteOfGraceManager PlayerUISiteOfGraceManager =>
            m_playerUISiteOfGraceManager;

        /// <summary>Gets the local unlocked fast-travel location controller.</summary>
        public PlayerUITeleportLocationManager PlayerUITeleportLocationManager =>
            m_playerUITeleportLocationManager;

        /// <summary>Gets the Site of Grace Level Up menu controller.</summary>
        public PlayerUILevelUpManager PlayerUILevelUpManager =>
            m_playerUILevelUpManager;

        /// <summary>Gets the shared Character Menu, Anvil, and Blacksmith service.</summary>
        public PlayerUIWeaponUpgradeManager PlayerUIWeaponUpgradeManager =>
            m_playerUIWeaponUpgradeManager;

        /// <summary>Gets the shared NPC context and merchant UI.</summary>
        public PlayerUIShopManager PlayerUIShopManager => m_playerUIShopManager;

        /// <summary>Gets the Site of Grace item-storage controller.</summary>
        public PlayerUIStorageManager PlayerUIStorageManager =>
            m_playerUIStorageManager;

        /// <summary>Gets the persistent world-transition loading-screen controller.</summary>
        public PlayerUILoadingScreenManager PlayerUILoadingScreenManager =>
            m_playerUILoadingScreenManager;

        /// <summary>Gets whether any modal player menu currently owns UI input.</summary>
        public bool IsMenuWindowOpen => m_isMenuWindowOpen;

        /// <summary>Styles generated shop and storage commands using the authored gameplay menu template.</summary>
        public void ApplyGameplayButtonStyle(Button button, TMPro.TMP_Text label)
        {
            if (button == null || m_gameplayButtonStyle == null)
            {
                return;
            }
            FrontendSelectableVisual visual = button.GetComponent<FrontendSelectableVisual>();
            if (visual == null)
            {
                visual = button.gameObject.AddComponent<FrontendSelectableVisual>();
            }
            visual.ApplyAppearanceFrom(m_gameplayButtonStyle, label);
        }

        /// <summary>Gets whether the active Scene and player state allow menus.</summary>
        public bool CanOpenMenuWindows =>
            !m_isMenuInputBlocked && SceneManager.GetActiveScene().buildIndex > 0;

        private void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
                m_playerUIHUDManager ??= GetComponentInChildren<PlayerUIHUDManager>(true);
                m_playerUIBossHealthBar ??=
                    GetComponentInChildren<PlayerUIBossHealthBar>(true);
                m_playerUISaveGameManager ??=
                    GetComponentInChildren<PlayerUISaveGameManager>(true);
                m_playerUIPopUpManager ??=
                    GetComponentInChildren<PlayerUIPopUpManager>(true);
                m_playerUICharacterMenuManager ??=
                    GetComponentInChildren<PlayerUICharacterMenuManager>(true);
                m_playerUIEquipmentManager ??=
                    GetComponentInChildren<PlayerUIEquipmentManager>(true);
                m_playerUISiteOfGraceManager ??=
                    GetComponentInChildren<PlayerUISiteOfGraceManager>(true);
                m_playerUITeleportLocationManager ??=
                    GetComponentInChildren<PlayerUITeleportLocationManager>(true);
                m_playerUILevelUpManager ??=
                    GetComponentInChildren<PlayerUILevelUpManager>(true);
                m_playerUIWeaponUpgradeManager ??=
                    GetComponentInChildren<PlayerUIWeaponUpgradeManager>(true);
                m_playerUIShopManager ??=
                    GetComponentInChildren<PlayerUIShopManager>(true);
                m_playerUIShopManager ??=
                    gameObject.AddComponent<PlayerUIShopManager>();
                m_playerUIStorageManager ??=
                    GetComponentInChildren<PlayerUIStorageManager>(true);
                m_playerUIStorageManager ??=
                    gameObject.AddComponent<PlayerUIStorageManager>();
                m_playerUILoadingScreenManager ??=
                    GetComponentInChildren<PlayerUILoadingScreenManager>(true);
                m_uiAudioSource ??= GetComponent<AudioSource>();
                if (m_uiAudioSource != null)
                {
                    m_uiAudioSource.spatialBlend = 0f;
                    m_uiAudioSource.playOnAwake = false;
                }

                BuildScaledUiCursor();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            UpdateCursorVisibility();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (m_shouldStartAsClient)
            {
                m_shouldStartAsClient = false;
                NetworkManager.Singleton.Shutdown();

                NetworkManager.Singleton.StartClient();
            }

            UpdateMenuSounds();
            UpdateCursorVisibility();
        }

        /// <summary>
        /// Enforces the authored cursor policy every frame so no other system can
        /// leave the pointer visible during gameplay or hide it inside a menu.
        /// </summary>
        private void UpdateCursorVisibility()
        {
            bool shouldHideCursor = ShouldHideCursor();
            bool statesMatch =
                Cursor.visible == !shouldHideCursor &&
                Cursor.lockState == (shouldHideCursor
                    ? CursorLockMode.Locked
                    : CursorLockMode.None);
            if (statesMatch &&
                shouldHideCursor == m_lastCursorHiddenState &&
                (shouldHideCursor || m_hasMenuCursorApplied))
            {
                return;
            }

            m_lastCursorHiddenState = shouldHideCursor;
            ApplyCursorState(shouldHideCursor);
        }

        /// <summary>
        /// Gameplay owns the pointer only outside the title screen while no modal
        /// menu is open; the front-end Scene always shows the authored cursor.
        /// </summary>
        private bool ShouldHideCursor()
        {
            if (m_isMenuWindowOpen ||
                SceneManager.GetActiveScene().name == k_TitleSceneName)
            {
                return false;
            }

            return PlayerInputManager.Instance == null ||
                PlayerInputManager.Instance.IsMovementInputEnabled;
        }

        /// <summary>
        /// Central UI feedback: hover plays when the pointer or navigation selection
        /// moves to a new element, and a confirm plays when a menu element is clicked.
        /// A short dedupe window keeps pointer and selection paths from double-firing.
        /// Runs only while the pointer is visible (menus and front-end).
        /// </summary>
        private void UpdateMenuSounds()
        {
            EventSystem eventSystem = EventSystem.current;
            if (!Cursor.visible || eventSystem == null)
            {
                m_lastHoveredUiObject = null;
                m_lastSelectedGameObject = null;
                return;
            }

            Mouse mouse = Mouse.current;
            bool isPointerOverUI = mouse != null && eventSystem.IsPointerOverGameObject();
            if (isPointerOverUI)
            {
                if (m_pointerEventData == null || m_pointerEventSystem != eventSystem)
                {
                    m_pointerEventData = new PointerEventData(eventSystem);
                    m_pointerEventSystem = eventSystem;
                }

                m_pointerEventData.position = mouse.position.ReadValue();
                m_uiRaycastResults.Clear();
                eventSystem.RaycastAll(m_pointerEventData, m_uiRaycastResults);
                GameObject hoveredObject = ResolveHoverTarget(m_uiRaycastResults);
                if (hoveredObject != null && hoveredObject != m_lastHoveredUiObject)
                {
                    TryPlayHoverSound();
                }

                m_lastHoveredUiObject = hoveredObject;
            }
            else
            {
                m_lastHoveredUiObject = null;
            }

            GameObject selectedObject = eventSystem.currentSelectedGameObject;
            if (selectedObject != null && selectedObject != m_lastSelectedGameObject)
            {
                TryPlayHoverSound();
            }

            m_lastSelectedGameObject = selectedObject;

            if (isPointerOverUI &&
                (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                TryPlayConfirmSound();
            }
        }

        private void TryPlayHoverSound()
        {
            if (Time.unscaledTime - m_lastHoverSoundTime < 0.05f)
            {
                return;
            }

            m_lastHoverSoundTime = Time.unscaledTime;
            PlayMenuHoverSound();
        }

        private void TryPlayConfirmSound()
        {
            if (Time.unscaledTime - m_lastConfirmSoundTime < 0.05f)
            {
                return;
            }

            m_lastConfirmSoundTime = Time.unscaledTime;
            PlayMenuConfirmSound();
        }

        /// <summary>
        /// Walks from the raycast hit up to the nearest Selectable so hovering a
        /// child Graphic inside a button still resolves to that button.
        /// </summary>
        private static GameObject ResolveHoverTarget(List<RaycastResult> raycastResults)
        {
            if (raycastResults == null || raycastResults.Count == 0)
            {
                return null;
            }

            Transform hoveredTransform = raycastResults[0].gameObject.transform;
            while (hoveredTransform != null &&
                hoveredTransform.GetComponent<Selectable>() == null)
            {
                hoveredTransform = hoveredTransform.parent;
            }

            return hoveredTransform != null
                ? hoveredTransform.gameObject
                : raycastResults[0].gameObject;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }

            if (m_scaledUiCursor != null)
            {
                Destroy(m_scaledUiCursor);
                m_scaledUiCursor = null;
            }
        }

        /// <summary>Closes every modal menu through one shared ownership boundary.</summary>
        public void CloseAllMenuWindows()
        {
            m_playerUICharacterMenuManager?.CloseMenu();
            m_playerUIEquipmentManager?.CloseMenu();
            m_playerUISaveGameManager?.CloseSaveGameMenu();
            m_playerUISiteOfGraceManager?.CloseSiteOfGraceMenu();
            m_playerUITeleportLocationManager?.CloseTeleportLocationMenu();
            m_playerUILevelUpManager?.CloseMenu();
            m_playerUIWeaponUpgradeManager?.CloseMenu();
            m_playerUIShopManager?.CloseMenu();
            m_playerUIStorageManager?.CloseMenu();
            ReleaseMenuInput();
        }

        /// <summary>Blocks menus for death or another higher-priority local state.</summary>
        public void SetMenuInputBlocked(bool isBlocked)
        {
            m_isMenuInputBlocked = isBlocked;
            if (isBlocked)
            {
                CloseAllMenuWindows();
            }
        }

        /// <summary>Caches the locally owned player for HUD and menu data access.</summary>
        public void BindLocalPlayer(PlayerManager player)
        {
            if (player != null && (!player.IsSpawned || player.IsOwner))
            {
                LocalPlayer = player;
            }
        }

        /// <summary>Clears the cached player only when the same owner is leaving.</summary>
        public void UnbindLocalPlayer(PlayerManager player)
        {
            if (LocalPlayer == player)
            {
                LocalPlayer = null;
            }
        }

        /// <summary>Transfers gameplay, cursor, and navigation input to a modal menu.</summary>
        public void NotifyMenuWindowOpened()
        {
            if (m_isMenuWindowOpen)
            {
                return;
            }

            m_isMenuWindowOpen = true;
            PlayerInputManager.Instance?.BlockGameplayInput();
            ActivateMenuEventSystem();
            UpdateCursorVisibility();
        }

        /// <summary>Releases modal input only after every menu window has closed.</summary>
        public void RefreshMenuWindowState()
        {
            bool hasOpenMenu =
                m_playerUICharacterMenuManager?.IsMenuOpen == true ||
                m_playerUIEquipmentManager?.IsMenuOpen == true ||
                m_playerUISaveGameManager?.IsSaveGameMenuOpen == true ||
                m_playerUISiteOfGraceManager?.IsSiteOfGraceMenuOpen == true ||
                m_playerUITeleportLocationManager
                    ?.IsTeleportLocationMenuOpen == true ||
                m_playerUILevelUpManager?.IsMenuOpen == true;
            hasOpenMenu |=
                m_playerUIWeaponUpgradeManager?.IsMenuOpen == true;
            hasOpenMenu |= m_playerUIShopManager?.IsMenuOpen == true;
            hasOpenMenu |= m_playerUIStorageManager?.IsMenuOpen == true;
            if (hasOpenMenu)
            {
                NotifyMenuWindowOpened();
                return;
            }

            ReleaseMenuInput();
        }

        /// <summary>Plays the shared non-spatial navigation sound.</summary>
        public void PlayMenuHoverSound()
        {
            PlayUISound(m_menuHoverSound);
        }

        /// <summary>Plays the shared non-spatial confirmation sound.</summary>
        public void PlayMenuConfirmSound()
        {
            PlayUISound(m_menuConfirmSound);
        }

        /// <summary>Plays the shared non-spatial invalid-action sound.</summary>
        public void PlayUnableToContinueSound()
        {
            PlayUISound(m_unableToContinueSound);
        }

        private void ActivateMenuEventSystem()
        {
            if (EventSystem.current != null || m_menuEventSystem == null)
            {
                return;
            }

            m_menuEventSystem.SetActive(true);
            m_isInternalEventSystemActive = true;
        }

        private void ReleaseMenuInput()
        {
            if (!m_isMenuWindowOpen)
            {
                return;
            }

            m_isMenuWindowOpen = false;
            EventSystem.current?.SetSelectedGameObject(null);
            if (m_isInternalEventSystemActive)
            {
                m_menuEventSystem?.SetActive(false);
                m_isInternalEventSystemActive = false;
            }

            PlayerInputManager.Instance?.UnblockGameplayInput();
            UpdateCursorVisibility();
        }

        /// <summary>
        /// Hides and locks the pointer while gameplay owns the screen, and swaps in
        /// the authored menu cursor whenever menus or the front-end Scene are active.
        /// </summary>
        private void ApplyCursorState(bool shouldHideCursor)
        {
            if (shouldHideCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                m_hasMenuCursorApplied = false;
                return;
            }

            m_hasMenuCursorApplied = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Cursor.SetCursor(m_scaledUiCursor, m_uiCursorHotspot, CursorMode.Auto);
        }

        /// <summary>
        /// Builds a small readable copy of the authored cursor art so the operating
        /// system cursor is not rendered at the source texture resolution.
        /// </summary>
        private void BuildScaledUiCursor()
        {
            if (m_uiCursorTexture == null)
            {
                return;
            }

            int targetWidth = Mathf.Min(m_uiCursorTexture.width, Mathf.Max(1, m_uiCursorPixelWidth));
            int targetHeight = Mathf.Max(1,
                Mathf.RoundToInt(targetWidth *
                    (float)m_uiCursorTexture.height / m_uiCursorTexture.width));
            RenderTexture previousRenderTexture = RenderTexture.active;
            RenderTexture cursorRenderTexture = RenderTexture.GetTemporary(
                targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            try
            {
                // Imported UI textures may omit CPU pixels; read back a small cursor copy.
                Graphics.Blit(m_uiCursorTexture, cursorRenderTexture);
                RenderTexture.active = cursorRenderTexture;
                m_scaledUiCursor = new Texture2D(
                    targetWidth, targetHeight, TextureFormat.RGBA32, false)
                {
                    name = "Menu Cursor",
                    filterMode = FilterMode.Bilinear
                };
                m_scaledUiCursor.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                // Cursor.SetCursor requires the CPU copy even after the texture is uploaded.
                m_scaledUiCursor.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;
                RenderTexture.ReleaseTemporary(cursorRenderTexture);
            }
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene activeScene)
        {
            UpdateCursorVisibility();
        }

        private void PlayUISound(AudioClip audioClip)
        {
            if (m_uiAudioSource != null && audioClip != null)
            {
                m_uiAudioSource.PlayOneShot(audioClip);
            }
        }
    }
}
