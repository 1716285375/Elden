using UnityEngine;
using UnityEngine.Serialization;

namespace ZZ
{
    public class PlayerLocomotionManager : CharacterLocomotionManager
    {
        private const float k_SprintMovementThreshold = 0.5f;

        [Header("Movement Speeds")]
        [FormerlySerializedAs("walkingSpeed")]
        [SerializeField, Min(0f)] private float m_walkingSpeed = 2f;
        [FormerlySerializedAs("runningSpeed")]
        [SerializeField, Min(0f)] private float m_runningSpeed = 5f;
        [SerializeField, Min(0f)] private float m_sprintingSpeed = 8f;
        [FormerlySerializedAs("rotationSpeed")]
        [SerializeField, Min(0f)] private float m_rotationSpeed = 15f;

        [Header("Air Movement")]
        [SerializeField, Min(0f)] private float m_jumpHeight = 2f;
        [SerializeField, Min(0f)] private float m_jumpForwardSpeed = 5f;
        [SerializeField, Min(0f)] private float m_freeFallingSpeed = 2f;
        [SerializeField, Range(0f, 1f)] private float m_sprintJumpMomentum = 1f;
        [SerializeField, Range(0f, 1f)] private float m_runJumpMomentum = 0.5f;
        [SerializeField, Range(0f, 1f)] private float m_walkJumpMomentum = 0.25f;

        [Header("Stamina Costs")]
        [SerializeField, Min(0f)] private float m_sprintingStaminaCost = 10f;
        [SerializeField, Min(0f)] private float m_dodgeStaminaCost = 25f;
        [SerializeField, Min(0f)] private float m_jumpStaminaCost = 25f;

        private PlayerManager m_player;
        private PlayerInputManager m_playerInputManager;
        private PlayerCamera m_playerCamera;
        private Vector3 m_jumpDirection;

        public bool IsSprinting =>
            m_player != null &&
            m_player.PlayerNetworkManager != null &&
            m_player.PlayerNetworkManager.IsSprinting.Value;

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponent<PlayerManager>();
        }

        private void Update()
        {
            HandleAllMovement();
        }

        public void HandleAllMovement()
        {
            if (m_player == null || !m_player.IsSpawned)
            {
                return;
            }

            if (!m_player.IsInGameplayScene)
            {
                ResetAirborneMovement();
                m_jumpDirection = Vector3.zero;
                if (m_player.IsOwner)
                {
                    SetSprinting(false);
                    PublishMovementState(0f, 0f, 0f);
                    m_player.PlayerAnimatorManager?.UpdateAnimatorMovementParameters(0f, 0f, false);
                }
                return;
            }

            HandleGroundCheck();

            if (m_player.IsOwner)
            {
                HandleOwnerMovement();
                return;
            }

            HandleRemoteMovementAnimation();
        }

        public void WarpTo(Vector3 position, Quaternion rotation)
        {
            ResetAirborneMovement();
            m_jumpDirection = Vector3.zero;
            bool controllerWasEnabled = m_characterController != null && m_characterController.enabled;
            if (controllerWasEnabled)
            {
                m_characterController.enabled = false;
            }

            transform.SetPositionAndRotation(position, rotation);

            if (controllerWasEnabled)
            {
                m_characterController.enabled = true;
            }
        }

        /// <summary>
        /// Validates a jump intent, consumes stamina, and starts the authored take-off animation.
        /// </summary>
        public void AttemptToPerformJump()
        {
            if (m_player == null || !m_player.IsOwner)
            {
                return;
            }

            float currentStamina = m_player.CharacterNetworkManager != null
                ? m_player.CharacterNetworkManager.CurrentStamina.Value
                : 0f;
            if (!CanJump(
                m_player.IsPerformingAction,
                currentStamina,
                m_player.IsJumping,
                m_player.IsGrounded) ||
                m_player.PlayerAnimatorManager == null ||
                !m_player.PlayerAnimatorManager.CanPlayJumpStartAnimation())
            {
                return;
            }

            PlayerStatsManager statsManager = m_player.PlayerStatsManager;
            if (statsManager == null || !statsManager.TryConsumeStamina(m_jumpStaminaCost))
            {
                return;
            }

            m_playerInputManager ??= PlayerInputManager.Instance;
            m_playerCamera ??= PlayerCamera.Instance;
            CaptureJumpDirection();
            SetSprinting(false);
            m_player.BeginJump();
            m_player.PlayerAnimatorManager.PlayJumpStartAnimation();
        }

        /// <summary>
        /// Applies the calculated upward velocity at the Jump Start animation's take-off frame.
        /// </summary>
        public void ApplyJumpingVelocity()
        {
            if (m_player == null || !m_player.IsOwner || !m_player.IsJumping)
            {
                return;
            }

            m_verticalVelocity.y = CalculateJumpVelocity(m_jumpHeight, GravityForce);
        }

        /// <summary>
        /// Validates a dodge request and selects a roll or backstep from the current movement input.
        /// </summary>
        public void AttemptToPerformDodge()
        {
            if (m_player == null || !m_player.IsOwner || m_player.IsPerformingAction)
            {
                return;
            }

            m_playerInputManager ??= PlayerInputManager.Instance;
            m_playerCamera ??= PlayerCamera.Instance;
            if (m_playerInputManager == null)
            {
                return;
            }

            if (m_playerInputManager.MoveAmount > 0f)
            {
                if (m_playerCamera == null || m_playerCamera.CameraObject == null)
                {
                    return;
                }

                if (!TryConsumeDodgeStamina())
                {
                    return;
                }

                SetSprinting(false);
                PerformRoll();
                return;
            }

            if (!TryConsumeDodgeStamina())
            {
                return;
            }

            SetSprinting(false);
            PerformBackstep();
        }

        /// <summary>
        /// Resolves held sprint input into the player's validated sprint gameplay state.
        /// </summary>
        public void HandleSprinting(bool isSprintInputHeld)
        {
            if (m_player == null || !m_player.IsOwner)
            {
                return;
            }

            m_playerInputManager ??= PlayerInputManager.Instance;
            float moveAmount = m_playerInputManager != null
                ? m_playerInputManager.MoveAmount
                : 0f;
            float currentStamina = m_player.CharacterNetworkManager != null
                ? m_player.CharacterNetworkManager.CurrentStamina.Value
                : 0f;
            SetSprinting(CanSprint(
                isSprintInputHeld,
                m_player.IsPerformingAction,
                moveAmount,
                currentStamina));
        }

        /// <summary>Stops the replicated sprint state when a committed action begins.</summary>
        public void StopSprinting()
        {
            if (m_player != null && m_player.IsOwner)
            {
                SetSprinting(false);
            }
        }

        private void HandleOwnerMovement()
        {
            m_playerInputManager ??= PlayerInputManager.Instance;
            m_playerCamera ??= PlayerCamera.Instance;

            if (m_playerInputManager == null)
            {
                SetSprinting(false);
                PublishMovementState(0f, 0f, 0f);
                m_player.PlayerAnimatorManager?.UpdateAnimatorMovementParameters(0f, 0f, false);
                HandleVerticalMovement();
                UpdateAnimatorAirParameters();
                return;
            }

            ConsumeSprintingStamina();
            HandleGroundedMovement();
            HandleJumpingMovement();
            HandleFreeFallMovement();
            HandleRotation();
            HandleVerticalMovement();

            PublishMovementState(
                m_playerInputManager.HorizontalInput,
                m_playerInputManager.VerticalInput,
                m_playerInputManager.MoveAmount);
            m_player.PlayerAnimatorManager?.UpdateAnimatorMovementParameters(
                0f,
                m_playerInputManager.MoveAmount,
                IsSprinting);
            UpdateAnimatorAirParameters();
        }

        private void HandleRemoteMovementAnimation()
        {
            CharacterNetworkManager networkManager = m_player.CharacterNetworkManager;
            if (networkManager == null)
            {
                return;
            }

            m_player.PlayerAnimatorManager?.UpdateAnimatorMovementParameters(
                0f,
                networkManager.MoveAmount.Value,
                m_player.PlayerNetworkManager != null &&
                m_player.PlayerNetworkManager.IsSprinting.Value);
            UpdateAnimatorAirParameters();
        }

        private void PublishMovementState(float horizontal, float vertical, float amount)
        {
            CharacterNetworkManager networkManager = m_player.CharacterNetworkManager;
            if (networkManager == null)
            {
                return;
            }

            networkManager.HorizontalMovement.Value = horizontal;
            networkManager.VerticalMovement.Value = vertical;
            networkManager.MoveAmount.Value = amount;
        }

        private void HandleGroundedMovement()
        {
            if (!m_player.CanMove || !m_player.IsGrounded)
            {
                return;
            }

            if (m_playerInputManager == null || m_playerCamera == null || m_playerCamera.CameraObject == null)
            {
                return;
            }

            Vector3 moveDirection = GetCameraRelativeDirection();
            moveDirection.y = 0f;
            moveDirection.Normalize();

            float normalSpeed = m_playerInputManager.MoveAmount > 0.5f
                ? m_runningSpeed
                : m_walkingSpeed;
            float movementSpeed = IsSprinting ? m_sprintingSpeed : normalSpeed;
            m_characterController.Move(moveDirection * movementSpeed * Time.deltaTime);
        }

        private void HandleJumpingMovement()
        {
            if (!m_player.IsJumping || m_jumpDirection == Vector3.zero)
            {
                return;
            }

            m_characterController.Move(
                m_jumpDirection * m_jumpForwardSpeed * Time.deltaTime);
        }

        private void HandleFreeFallMovement()
        {
            if (m_player.IsGrounded ||
                m_playerInputManager == null ||
                m_playerCamera == null ||
                m_playerCamera.CameraObject == null)
            {
                return;
            }

            Vector3 freeFallDirection = GetCameraRelativeDirection();
            freeFallDirection.y = 0f;
            freeFallDirection.Normalize();
            m_characterController.Move(
                freeFallDirection * m_freeFallingSpeed * Time.deltaTime);
        }

        private void HandleRotation()
        {
            if (!m_player.CanRotate)
            {
                return;
            }

            if (m_playerInputManager == null || m_playerCamera == null || m_playerCamera.CameraObject == null)
            {
                return;
            }

            Vector3 targetRotationDirection = GetTargetRotationDirection();
            targetRotationDirection.y = 0f;
            targetRotationDirection.Normalize();

            if (targetRotationDirection == Vector3.zero)
            {
                targetRotationDirection = transform.forward;
            }

            Quaternion targetRotation = Quaternion.LookRotation(targetRotationDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                m_rotationSpeed * Time.deltaTime);
        }

        private Vector3 GetCameraRelativeDirection()
        {
            Vector3 forward = m_playerCamera.CameraForward;
            Vector3 right = m_playerCamera.CameraRight;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return forward * m_playerInputManager.VerticalInput + right * m_playerInputManager.HorizontalInput;
        }

        private Vector3 GetTargetRotationDirection()
        {
            PlayerLockOnManager lockOnManager = m_player.LockOnManager;
            if (lockOnManager != null && lockOnManager.IsLockedOn)
            {
                return lockOnManager.CurrentTarget.transform.position - transform.position;
            }

            return GetCameraRelativeDirection();
        }

        private void PerformRoll()
        {
            if (m_playerCamera == null || m_playerCamera.CameraObject == null)
            {
                return;
            }

            Vector3 rollDirection = GetCameraRelativeDirection();
            rollDirection.y = 0f;
            rollDirection.Normalize();
            if (rollDirection == Vector3.zero)
            {
                rollDirection = transform.forward;
            }

            transform.rotation = Quaternion.LookRotation(rollDirection);
            PlayDodgeAction(CharacterActionAnimation.RollForward);
        }

        private void PerformBackstep()
        {
            PlayDodgeAction(CharacterActionAnimation.BackStep);
        }

        private void CaptureJumpDirection()
        {
            if (m_playerInputManager == null ||
                m_playerCamera == null ||
                m_playerCamera.CameraObject == null)
            {
                m_jumpDirection = Vector3.zero;
                return;
            }

            m_jumpDirection = GetCameraRelativeDirection();
            m_jumpDirection.y = 0f;
            m_jumpDirection.Normalize();
            float momentumScale = ResolveJumpMomentumScale(
                IsSprinting,
                m_playerInputManager.MoveAmount,
                m_sprintJumpMomentum,
                m_runJumpMomentum,
                m_walkJumpMomentum);
            m_jumpDirection *= momentumScale;
        }

        private void UpdateAnimatorAirParameters()
        {
            m_player.PlayerAnimatorManager?.UpdateAnimatorAirParameters(
                m_player.IsGrounded,
                InAirTimer);
        }

        private bool TryConsumeDodgeStamina()
        {
            return m_player.PlayerStatsManager != null &&
                m_player.PlayerStatsManager.TryConsumeStamina(m_dodgeStaminaCost);
        }

        private void ConsumeSprintingStamina()
        {
            if (!IsSprinting)
            {
                return;
            }

            float staminaCost = m_sprintingStaminaCost * Time.deltaTime;
            if (staminaCost <= 0f)
            {
                return;
            }

            PlayerStatsManager statsManager = m_player.PlayerStatsManager;
            if (statsManager == null ||
                !statsManager.TryConsumeStamina(staminaCost) ||
                m_player.CharacterNetworkManager.CurrentStamina.Value <= 0f)
            {
                SetSprinting(false);
            }
        }

        private void PlayDodgeAction(CharacterActionAnimation targetAnimation)
        {
            const bool k_IsPerformingAction = true;
            const bool k_ShouldApplyRootMotion = true;
            const bool k_CanRotate = false;
            const bool k_CanMove = false;

            m_player.PlayerAnimatorManager?.PlayTargetActionAnimation(
                targetAnimation,
                k_IsPerformingAction,
                k_ShouldApplyRootMotion,
                k_CanRotate,
                k_CanMove);
            m_player.CharacterNetworkManager?.NotifyServerOfActionAnimationServerRpc(
                targetAnimation,
                k_IsPerformingAction,
                k_ShouldApplyRootMotion,
                k_CanRotate,
                k_CanMove);
        }

        private static bool CanSprint(
            bool isSprintInputHeld,
            bool isPerformingAction,
            float moveAmount,
            float currentStamina)
        {
            return isSprintInputHeld &&
                !isPerformingAction &&
                moveAmount >= k_SprintMovementThreshold &&
                currentStamina > 0f;
        }

        private static bool CanJump(
            bool isPerformingAction,
            float currentStamina,
            bool isJumping,
            bool isGrounded)
        {
            return !isPerformingAction &&
                currentStamina > 0f &&
                !isJumping &&
                isGrounded;
        }

        private static float CalculateJumpVelocity(float jumpHeight, float gravityForce)
        {
            if (jumpHeight <= 0f || gravityForce >= 0f)
            {
                return 0f;
            }

            return Mathf.Sqrt(jumpHeight * -2f * gravityForce);
        }

        private static float ResolveJumpMomentumScale(
            bool isSprinting,
            float moveAmount,
            float sprintMomentum,
            float runMomentum,
            float walkMomentum)
        {
            if (moveAmount <= 0f)
            {
                return 0f;
            }

            if (isSprinting)
            {
                return sprintMomentum;
            }

            return moveAmount > k_SprintMovementThreshold
                ? runMomentum
                : walkMomentum;
        }

        private void SetSprinting(bool isSprinting)
        {
            PlayerNetworkManager networkManager = m_player.PlayerNetworkManager;
            if (networkManager == null ||
                !networkManager.IsSpawned ||
                !networkManager.IsOwner ||
                networkManager.IsSprinting.Value == isSprinting)
            {
                return;
            }

            networkManager.IsSprinting.Value = isSprinting;
        }
    }
}
