using UnityEngine;
using UnityEngine.Serialization;

namespace ZZ
{
    public class PlayerLocomotionManager : CharacterLocomotionManager
    {
        [Header("Movement Speeds")]
        [FormerlySerializedAs("walkingSpeed")]
        [SerializeField, Min(0f)] private float m_walkingSpeed = 2f;
        [FormerlySerializedAs("runningSpeed")]
        [SerializeField, Min(0f)] private float m_runningSpeed = 5f;
        [FormerlySerializedAs("rotationSpeed")]
        [SerializeField, Min(0f)] private float m_rotationSpeed = 15f;
        [FormerlySerializedAs("gravity")]
        [SerializeField] private float m_gravity = -20f;

        private PlayerManager m_player;
        private PlayerInputManager m_playerInputManager;
        private PlayerCamera m_playerCamera;
        private float m_verticalVelocity;

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
                m_verticalVelocity = 0f;
                if (m_player.IsOwner)
                {
                    PublishMovementState(0f, 0f, 0f);
                    m_player.PlayerAnimatorManager?.UpdateAnimatorMovementParameters(0f, 0f);
                }
                return;
            }

            if (m_player.IsOwner)
            {
                HandleOwnerMovement();
                return;
            }

            HandleRemoteMovementAnimation();
        }

        public void WarpTo(Vector3 position, Quaternion rotation)
        {
            m_verticalVelocity = 0f;
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
                PerformRoll();
                return;
            }

            PerformBackstep();
        }

        private void HandleOwnerMovement()
        {
            m_playerInputManager ??= PlayerInputManager.Instance;
            m_playerCamera ??= PlayerCamera.Instance;

            if (m_playerInputManager == null)
            {
                PublishMovementState(0f, 0f, 0f);
                m_player.PlayerAnimatorManager?.UpdateAnimatorMovementParameters(0f, 0f);
                return;
            }

            HandleGroundedMovement();
            HandleRotation();

            PublishMovementState(
                m_playerInputManager.HorizontalInput,
                m_playerInputManager.VerticalInput,
                m_playerInputManager.MoveAmount);
            m_player.PlayerAnimatorManager?.UpdateAnimatorMovementParameters(
                0f,
                m_playerInputManager.MoveAmount);
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
                networkManager.MoveAmount.Value);
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
            if (!m_player.CanMove)
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

            float movementSpeed = m_playerInputManager.MoveAmount > 0.5f ? m_runningSpeed : m_walkingSpeed;

            if (m_characterController.isGrounded && m_verticalVelocity < 0f)
            {
                m_verticalVelocity = -2f;
            }

            m_verticalVelocity += m_gravity * Time.deltaTime;
            Vector3 velocity = moveDirection * movementSpeed;
            velocity.y = m_verticalVelocity;
            m_characterController.Move(velocity * Time.deltaTime);
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

            Vector3 targetRotationDirection = GetCameraRelativeDirection();
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

        private void PlayDodgeAction(CharacterActionAnimation targetAnimation)
        {
            const bool k_IsPerformingAction = true;
            const bool k_ApplyRootMotion = true;
            const bool k_CanRotate = false;
            const bool k_CanMove = false;

            m_player.PlayerAnimatorManager?.PlayTargetActionAnimation(
                targetAnimation,
                k_IsPerformingAction,
                k_ApplyRootMotion,
                k_CanRotate,
                k_CanMove);
            m_player.CharacterNetworkManager?.NotifyServerOfActionAnimationServerRpc(
                targetAnimation,
                k_IsPerformingAction,
                k_ApplyRootMotion,
                k_CanRotate,
                k_CanMove);
        }
    }
}
