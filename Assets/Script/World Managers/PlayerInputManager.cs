using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZZ
{
    [DefaultExecutionOrder(-10000)]
    public class PlayerInputManager : MonoBehaviour
    {
        private const string k_GameplaySceneName = "Scene_World_01";
        private const int k_MaxQueuedAttackInputs = 2;

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

        [Header("Attack Input Buffer")]
        [SerializeField, Min(0f)] private float m_inputBufferDuration = 0.3f;

        private PlayerControls m_playerControls;
        private PlayerManager m_player;
        private readonly Queue<AttackInput> m_attackInputQueue = new();
        private bool m_hasDodgeInput;
        private bool m_hasJumpInput;
        private bool m_hasSwitchRightWeaponInput;
        private bool m_hasSwitchLeftWeaponInput;
        private bool m_hasRBInput;
        private bool m_hasRTStartedInput;
        private bool m_hasRTReleasedInput;
        private bool m_isLBInputHeld;
        private bool m_hasLockOnInput;
        private bool m_hasInteractionInput;
        private bool m_isGameplayInputBlocked;
        private bool m_isSprintInputHeld;
        private bool m_isTwoHandInputHeld;
        private bool m_hasTwoHandRightWeaponInput;
        private bool m_hasTwoHandLeftWeaponInput;

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
            DisablePlayerControls();
            m_playerControls.PlayerMovement.Movement.performed += OnMovementChanged;
            m_playerControls.PlayerMovement.Movement.canceled += OnMovementChanged;
            m_playerControls.PlayerMovement.Dodge.performed += OnDodgePerformed;
            m_playerControls.PlayerMovement.Sprint.performed += OnSprintPerformed;
            m_playerControls.PlayerMovement.Sprint.canceled += OnSprintCanceled;
            m_playerControls.PlayerMovement.Jump.performed += OnJumpPerformed;
            m_playerControls.PlayerMovement.SwitchRightWeapon.performed +=
                OnSwitchRightWeaponPerformed;
            m_playerControls.PlayerMovement.SwitchLeftWeapon.performed +=
                OnSwitchLeftWeaponPerformed;
            m_playerControls.PlayerMovement.RB.performed += OnRBPerformed;
            m_playerControls.PlayerMovement.RT.started += OnRTStarted;
            m_playerControls.PlayerMovement.RT.canceled += OnRTCanceled;
            m_playerControls.PlayerMovement.LB.performed += OnLBPerformed;
            m_playerControls.PlayerMovement.LB.canceled += OnLBCanceled;
            m_playerControls.PlayerMovement.TwoHandWeapon.performed +=
                OnTwoHandWeaponPerformed;
            m_playerControls.PlayerMovement.TwoHandWeapon.canceled +=
                OnTwoHandWeaponCanceled;
            m_playerControls.PlayerMovement.TwoHandRightWeapon.performed +=
                OnTwoHandRightWeaponPerformed;
            m_playerControls.PlayerMovement.TwoHandLeftWeapon.performed +=
                OnTwoHandLeftWeaponPerformed;
            m_playerControls.PlayerMovement.Interact.performed += OnInteractPerformed;
            m_playerControls.PlayerCamera.Movement.performed += OnCameraMovementChanged;
            m_playerControls.PlayerCamera.Movement.canceled += OnCameraMovementChanged;
            m_playerControls.PlayerCamera.LockOn.performed += OnLockOnPerformed;
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
            m_playerControls.PlayerMovement.Sprint.performed -= OnSprintPerformed;
            m_playerControls.PlayerMovement.Sprint.canceled -= OnSprintCanceled;
            m_playerControls.PlayerMovement.Jump.performed -= OnJumpPerformed;
            m_playerControls.PlayerMovement.SwitchRightWeapon.performed -=
                OnSwitchRightWeaponPerformed;
            m_playerControls.PlayerMovement.SwitchLeftWeapon.performed -=
                OnSwitchLeftWeaponPerformed;
            m_playerControls.PlayerMovement.RB.performed -= OnRBPerformed;
            m_playerControls.PlayerMovement.RT.started -= OnRTStarted;
            m_playerControls.PlayerMovement.RT.canceled -= OnRTCanceled;
            m_playerControls.PlayerMovement.LB.performed -= OnLBPerformed;
            m_playerControls.PlayerMovement.LB.canceled -= OnLBCanceled;
            m_playerControls.PlayerMovement.TwoHandWeapon.performed -=
                OnTwoHandWeaponPerformed;
            m_playerControls.PlayerMovement.TwoHandWeapon.canceled -=
                OnTwoHandWeaponCanceled;
            m_playerControls.PlayerMovement.TwoHandRightWeapon.performed -=
                OnTwoHandRightWeaponPerformed;
            m_playerControls.PlayerMovement.TwoHandLeftWeapon.performed -=
                OnTwoHandLeftWeaponPerformed;
            m_playerControls.PlayerMovement.Interact.performed -= OnInteractPerformed;
            m_playerControls.PlayerCamera.Movement.performed -= OnCameraMovementChanged;
            m_playerControls.PlayerCamera.Movement.canceled -= OnCameraMovementChanged;
            m_playerControls.PlayerCamera.LockOn.performed -= OnLockOnPerformed;
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
            ClearAttackInputQueue();
        }

        /// <summary>
        /// Clears the locally owned player without disturbing a newer ownership binding.
        /// </summary>
        public void ClearPlayer(PlayerManager localPlayer)
        {
            if (m_player == localPlayer)
            {
                ClearAttackInputQueue();
                m_player = null;
            }
        }

        /// <summary>
        /// Enables local movement and camera input when gameplay is not blocked by a modal UI.
        /// </summary>
        public void EnablePlayerControls()
        {
            if (m_isGameplayInputBlocked)
            {
                return;
            }

            m_playerControls?.PlayerMovement.Enable();
            m_playerControls?.PlayerCamera.Enable();
            IsMovementInputEnabled = true;
        }

        /// <summary>
        /// Disables local movement and camera input and clears all held gameplay intentions.
        /// </summary>
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
            m_hasJumpInput = false;
            m_hasSwitchRightWeaponInput = false;
            m_hasSwitchLeftWeaponInput = false;
            m_hasRBInput = false;
            m_hasRTStartedInput = false;
            m_hasRTReleasedInput = false;
            m_isLBInputHeld = false;
            m_hasLockOnInput = false;
            m_hasInteractionInput = false;
            m_isSprintInputHeld = false;
            m_isTwoHandInputHeld = false;
            m_hasTwoHandRightWeaponInput = false;
            m_hasTwoHandLeftWeaponInput = false;
            m_player?.LocomotionManager?.HandleSprinting(false);
            m_player?.PlayerCombatManager?.CancelChargingAttack();
            m_player?.PlayerCombatManager?.SetBlocking(false);
            ClearAttackInputQueue();
            IsMovementInputEnabled = false;
            m_playerControls?.PlayerMovement.Disable();
            m_playerControls?.PlayerCamera.Disable();
        }

        /// <summary>
        /// Blocks gameplay controls while a modal player UI owns navigation input.
        /// </summary>
        public void BlockGameplayInput()
        {
            m_isGameplayInputBlocked = true;
            DisablePlayerControls();
        }

        /// <summary>
        /// Releases the modal UI block and restores controls when the active Scene permits gameplay.
        /// </summary>
        public void UnblockGameplayInput()
        {
            m_isGameplayInputBlocked = false;
            RefreshMovementInput(SceneManager.GetActiveScene());
        }

        /// <summary>
        /// Records one attack only while the current combat animation permits queuing.
        /// </summary>
        public bool TryQueueAttackInput(AttackInputType inputType)
        {
            if (m_player?.PlayerCombatManager?.CanQueueNextAttack != true)
            {
                return false;
            }

            RemoveExpiredAttackInputs(Time.time);
            if (m_attackInputQueue.Count >= k_MaxQueuedAttackInputs)
            {
                return false;
            }

            m_attackInputQueue.Enqueue(new AttackInput(inputType, Time.time));
            return true;
        }

        /// <summary>Returns the oldest unexpired buffered attack.</summary>
        public bool TryDequeueAttackInput(out AttackInput attackInput)
        {
            RemoveExpiredAttackInputs(Time.time);
            if (m_attackInputQueue.Count == 0)
            {
                attackInput = default;
                return false;
            }

            attackInput = m_attackInputQueue.Dequeue();
            return true;
        }

        /// <summary>Clears every attack intent that no longer belongs to the current action.</summary>
        public void ClearAttackInputQueue()
        {
            m_attackInputQueue.Clear();
        }

        private void HandleAllInputs()
        {
            if (PlayerUIManager.Instance?.IsMenuWindowOpen == true)
            {
                return;
            }

            HandleCameraMovementInput();
            HandleLockOnInput();
            HandlePlayerMovementInput();
            HandleDodgeInput();
            HandleJumpInput();
            HandleWeaponSwitchInput();
            HandleInteractionInput();
            HandleSprinting();
            HandleTwoHandInput();
            HandleBlockingInput();
            HandleAttackInput();
        }

        private void HandleCameraMovementInput()
        {
            CameraVerticalInput = CameraInput.y;
            CameraHorizontalInput = CameraInput.x;
            m_player?.LockOnManager?.HandleTargetSwitchInput(CameraHorizontalInput);
        }

        private void HandleLockOnInput()
        {
            if (!m_hasLockOnInput)
            {
                return;
            }

            m_hasLockOnInput = false;
            m_player?.LockOnManager?.HandleLockOn();
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
            m_player?.PlayerCombatManager?.SetBlocking(false);
            m_player?.LocomotionManager?.AttemptToPerformDodge();
        }

        private void HandleSprinting()
        {
            m_player?.LocomotionManager?.HandleSprinting(m_isSprintInputHeld);
        }

        private void HandleJumpInput()
        {
            if (!m_hasJumpInput)
            {
                return;
            }

            m_hasJumpInput = false;
            m_player?.PlayerCombatManager?.SetBlocking(false);
            m_player?.LocomotionManager?.AttemptToPerformJump();
        }

        private void HandleWeaponSwitchInput()
        {
            if (m_hasSwitchRightWeaponInput)
            {
                m_hasSwitchRightWeaponInput = false;
                m_player?.InventoryManager?.SwitchRightWeapon();
            }

            if (m_hasSwitchLeftWeaponInput)
            {
                m_hasSwitchLeftWeaponInput = false;
                m_player?.InventoryManager?.SwitchLeftWeapon();
            }
        }

        private void HandleAttackInput()
        {
            if (m_hasRBInput && !m_isTwoHandInputHeld)
            {
                m_hasRBInput = false;
                if (!TryQueueAttackInput(AttackInputType.Light))
                {
                    PerformRightHandAction();
                }
            }

            if (m_hasRTStartedInput)
            {
                m_hasRTStartedInput = false;
                if (!TryQueueAttackInput(AttackInputType.Heavy))
                {
                    m_player?.PlayerCombatManager?.BeginChargingHeavyAttack();
                }
            }

            if (m_hasRTReleasedInput)
            {
                m_hasRTReleasedInput = false;
                m_player?.PlayerCombatManager?.ReleaseChargingHeavyAttack();
            }
        }

        private void HandleBlockingInput()
        {
            if (!m_isLBInputHeld)
            {
                return;
            }

            WeaponItem weapon = ResolveBlockingWeapon();
            m_player?.PlayerCombatManager?.PerformWeaponBasedAction(
                weapon?.LeftHandAction,
                weapon);
        }

        private void HandleInteractionInput()
        {
            if (!m_hasInteractionInput)
            {
                return;
            }

            m_hasInteractionInput = false;
            m_player?.InteractionManager?.HandleInteractionInput();
        }

        private void HandleTwoHandInput()
        {
            if (!m_isTwoHandInputHeld || m_player?.PlayerNetworkManager == null)
            {
                return;
            }

            if (m_hasTwoHandRightWeaponInput)
            {
                m_hasTwoHandRightWeaponInput = false;
                m_hasRBInput = false;
                m_player.PlayerCombatManager?.SetBlocking(false);
                m_player.PlayerNetworkManager.ToggleTwoHandWeapon(true);
            }

            if (m_hasTwoHandLeftWeaponInput)
            {
                m_hasTwoHandLeftWeaponInput = false;
                m_isLBInputHeld = false;
                m_player.PlayerCombatManager?.SetBlocking(false);
                m_player.PlayerNetworkManager.ToggleTwoHandWeapon(false);
            }
        }

        private void PerformRightHandAction()
        {
            WeaponItem weapon = ResolveAttackWeapon();
            WeaponItemBasedAction action =
                m_player?.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true
                    ? weapon?.TwoHandRightAction
                    : weapon?.RightHandAction;
            if (action == null || weapon == null)
            {
                return;
            }

            m_player.PlayerCombatManager?.PerformWeaponBasedAction(action, weapon);
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

        private void OnSprintPerformed(InputAction.CallbackContext context)
        {
            m_isSprintInputHeld = true;
        }

        private void OnSprintCanceled(InputAction.CallbackContext context)
        {
            m_isSprintInputHeld = false;
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            m_hasJumpInput = true;
        }

        private void OnSwitchRightWeaponPerformed(InputAction.CallbackContext context)
        {
            m_hasSwitchRightWeaponInput = true;
        }

        private void OnSwitchLeftWeaponPerformed(InputAction.CallbackContext context)
        {
            m_hasSwitchLeftWeaponInput = true;
        }

        private void OnRBPerformed(InputAction.CallbackContext context)
        {
            m_hasRBInput = true;
        }

        private void OnRTStarted(InputAction.CallbackContext context)
        {
            m_hasRTStartedInput = true;
        }

        private WeaponItem ResolveAttackWeapon()
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            return m_player?.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true
                ? inventory?.CurrentTwoHandWeapon
                : inventory?.CurrentRightHandWeapon;
        }

        private WeaponItem ResolveBlockingWeapon()
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            return m_player?.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true
                ? inventory?.CurrentTwoHandWeapon
                : inventory?.CurrentLeftHandWeapon;
        }

        private void OnRTCanceled(InputAction.CallbackContext context)
        {
            m_hasRTReleasedInput = true;
        }

        private void OnLBPerformed(InputAction.CallbackContext context)
        {
            m_isLBInputHeld = true;
        }

        private void OnLBCanceled(InputAction.CallbackContext context)
        {
            m_isLBInputHeld = false;
            m_player?.PlayerCombatManager?.SetBlocking(false);
        }

        private void OnTwoHandWeaponPerformed(InputAction.CallbackContext context)
        {
            m_isTwoHandInputHeld = true;
        }

        private void OnTwoHandWeaponCanceled(InputAction.CallbackContext context)
        {
            m_isTwoHandInputHeld = false;
            m_hasTwoHandRightWeaponInput = false;
            m_hasTwoHandLeftWeaponInput = false;
        }

        private void OnTwoHandRightWeaponPerformed(InputAction.CallbackContext context)
        {
            m_hasTwoHandRightWeaponInput = m_isTwoHandInputHeld;
        }

        private void OnTwoHandLeftWeaponPerformed(InputAction.CallbackContext context)
        {
            m_hasTwoHandLeftWeaponInput = m_isTwoHandInputHeld;
        }

        private void OnLockOnPerformed(InputAction.CallbackContext context)
        {
            m_hasLockOnInput = true;
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            m_hasInteractionInput = true;
        }

        private void RemoveExpiredAttackInputs(float currentTime)
        {
            while (m_attackInputQueue.Count > 0 &&
                IsAttackInputExpired(
                    m_attackInputQueue.Peek(),
                    currentTime,
                    m_inputBufferDuration))
            {
                m_attackInputQueue.Dequeue();
            }
        }

        private static bool IsAttackInputExpired(
            AttackInput attackInput,
            float currentTime,
            float bufferDuration)
        {
            return currentTime >
                attackInput.Timestamp + Mathf.Max(0f, bufferDuration);
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene activeScene)
        {
            RefreshMovementInput(activeScene);
        }

        private void RefreshMovementInput(Scene activeScene)
        {
            if (Application.isFocused &&
                activeScene.name == k_GameplaySceneName &&
                !m_isGameplayInputBlocked)
            {
                EnablePlayerControls();
                return;
            }

            DisablePlayerControls();
        }
    }
}
