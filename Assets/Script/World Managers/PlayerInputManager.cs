using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZZ
{
    [DefaultExecutionOrder(-10000)]
    public class PlayerInputManager : MonoBehaviour
    {
        public static PlayerInputManager instance;
        public static PlayerInputManager Instance => instance;

        public Vector2 MovementInput { get; private set; }
        public float VerticalInput { get; private set; }
        public float HorizontalInput { get; private set; }
        public float MoveAmount { get; private set; }
        public bool IsMovementInputEnabled { get; private set; }

        private PlayerControls playerControls;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (instance != this)
            {
                return;
            }

            playerControls ??= new PlayerControls();
            playerControls.PlayerMovement.Movement.performed += OnMovementChanged;
            playerControls.PlayerMovement.Movement.canceled += OnMovementChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            RefreshMovementInput(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            if (instance != this || playerControls == null)
            {
                return;
            }

            playerControls.PlayerMovement.Movement.performed -= OnMovementChanged;
            playerControls.PlayerMovement.Movement.canceled -= OnMovementChanged;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            DisableMovementInput();
        }

        private void Update()
        {
            if (IsMovementInputEnabled)
            {
                HandleMovementInput();
            }
        }

        private void HandleMovementInput()
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

        private void OnApplicationFocus(bool focus)
        {
            if (!focus)
            {
                DisableMovementInput();
                return;
            }

            if (instance != this || playerControls == null)
            {
                return;
            }

            RefreshMovementInput(SceneManager.GetActiveScene());
        }

        private void OnMovementChanged(InputAction.CallbackContext context)
        {
            MovementInput = context.ReadValue<Vector2>();
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

            DisableMovementInput();
        }

        public void EnablePlayerControls()
        {
            playerControls?.Enable();
        }

        public void DisablePlayerControls()
        {
            MovementInput = Vector2.zero;
            VerticalInput = 0f;
            HorizontalInput = 0f;
            MoveAmount = 0f;
            IsMovementInputEnabled = false;
            playerControls?.Disable();
        }

        private void DisableMovementInput()
        {
            DisablePlayerControls();
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            instance = null;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            playerControls?.Dispose();
        }
    }
}
