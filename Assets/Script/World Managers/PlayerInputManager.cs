using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZZ
{
    [DefaultExecutionOrder(-10000)]
    public class PlayerInputManager : MonoBehaviour
    {
        private static PlayerInputManager s_instance;
        public static PlayerInputManager Instance => s_instance;

        public Vector2 MovementInput { get; private set; }
        public float VerticalInput { get; private set; }
        public float HorizontalInput { get; private set; }
        public float MoveAmount { get; private set; }
        public Vector2 CameraInput { get; private set; }
        public float CameraVerticalInput { get; private set; }
        public float CameraHorizontalInput { get; private set; }
        public bool IsMovementInputEnabled { get; private set; }

        private PlayerControls m_playerControls;
        private PlayerManager m_player;
        private bool m_hasDodgeInput;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (s_instance != this)
            {
                return;
            }

            m_playerControls ??= new PlayerControls();
            m_playerControls.PlayerMovement.Movement.performed += OnMovementChanged;
            m_playerControls.PlayerMovement.Movement.canceled += OnMovementChanged;
            m_playerControls.PlayerMovement.Dodge.performed += OnDodgePerformed;
            m_playerControls.PlayerCamera.Movement.performed += OnCameraMovementChanged;
            m_playerControls.PlayerCamera.Movement.canceled += OnCameraMovementChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            RefreshMovementInput(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            if (s_instance != this || m_playerControls == null)
            {
                return;
            }

            m_playerControls.PlayerMovement.Movement.performed -= OnMovementChanged;
            m_playerControls.PlayerMovement.Movement.canceled -= OnMovementChanged;
            m_playerControls.PlayerMovement.Dodge.performed -= OnDodgePerformed;
            m_playerControls.PlayerCamera.Movement.performed -= OnCameraMovementChanged;
            m_playerControls.PlayerCamera.Movement.canceled -= OnCameraMovementChanged;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            DisablePlayerControls();
        }

        private void Update()
        {
            if (IsMovementInputEnabled)
            {
                HandleAllInputs();
            }
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus)
            {
                DisablePlayerControls();
                return;
            }

            if (s_instance != this || m_playerControls == null)
            {
                return;
            }

            RefreshMovementInput(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            if (s_instance != this)
            {
                return;
            }

            s_instance = null;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            m_playerControls?.Dispose();
        }

        /// <summary>
        /// Associates input intentions with the locally owned player.
        /// </summary>
        public void BindPlayer(PlayerManager localPlayer)
        {
            if (localPlayer == null || !localPlayer.IsOwner)
            {
                return;
            }

            m_player = localPlayer;
        }

        /// <summary>
        /// Clears the locally owned player without disturbing a newer ownership binding.
        /// </summary>
        public void ClearPlayer(PlayerManager localPlayer)
        {
            if (m_player == localPlayer)
            {
                m_player = null;
            }
        }

        public void EnablePlayerControls()
        {
            m_playerControls?.Enable();
        }

        public void DisablePlayerControls()
        {
            MovementInput = Vector2.zero;
            VerticalInput = 0f;
            HorizontalInput = 0f;
            MoveAmount = 0f;
            CameraInput = Vector2.zero;
            CameraVerticalInput = 0f;
            CameraHorizontalInput = 0f;
            m_hasDodgeInput = false;
            IsMovementInputEnabled = false;
            m_playerControls?.Disable();
        }

        private void HandleAllInputs()
        {
            HandleCameraMovementInput();
            HandlePlayerMovementInput();
            HandleDodgeInput();
        }

        private void HandleCameraMovementInput()
        {
            CameraVerticalInput = CameraInput.y;
            CameraHorizontalInput = CameraInput.x;
        }

        private void HandlePlayerMovementInput()
        {
            VerticalInput = MovementInput.y;
            HorizontalInput = MovementInput.x;

            MoveAmount = Mathf.Clamp01(Mathf.Abs(VerticalInput) + Mathf.Abs(HorizontalInput));
            if (MoveAmount > 0f && MoveAmount <= 0.5f)
            {
                MoveAmount = 0.5f;
            }
            else if (MoveAmount > 0.5f)
            {
                MoveAmount = 1f;
            }
        }

        private void HandleDodgeInput()
        {
            if (!m_hasDodgeInput)
            {
                return;
            }

            m_hasDodgeInput = false;
            m_player?.LocomotionManager?.AttemptToPerformDodge();
        }

        private void OnMovementChanged(InputAction.CallbackContext context)
        {
            MovementInput = context.ReadValue<Vector2>();
        }

        private void OnCameraMovementChanged(InputAction.CallbackContext context)
        {
            CameraInput = context.ReadValue<Vector2>();
        }

        private void OnDodgePerformed(InputAction.CallbackContext context)
        {
            m_hasDodgeInput = true;
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene activeScene)
        {
            RefreshMovementInput(activeScene);
        }

        private void RefreshMovementInput(Scene activeScene)
        {
            int worldSceneIndex = WorldSaveGameManager.Instance != null
                ? WorldSaveGameManager.Instance.GetWorldSceneIndex()
                : -1;

            if (Application.isFocused && worldSceneIndex >= 0 && activeScene.buildIndex == worldSceneIndex)
            {
                EnablePlayerControls();
                IsMovementInputEnabled = true;
                return;
            }

            DisablePlayerControls();
        }
    }
}
