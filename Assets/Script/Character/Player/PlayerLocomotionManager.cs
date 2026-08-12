using UnityEngine;

namespace ZZ
{
    public class PlayerLocomotionManager : CharacterLocomotionManager
    {
        [Header("Movement Speeds")]
        [SerializeField, Min(0f)] private float walkingSpeed = 2f;
        [SerializeField, Min(0f)] private float runningSpeed = 5f;
        [SerializeField, Min(0f)] private float rotationSpeed = 15f;
        [SerializeField] private float gravity = -20f;

        private PlayerManager player;
        private PlayerInputManager playerInputManager;
        private PlayerCamera playerCamera;
        private float verticalVelocity;

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerManager>();
        }

        public void HandleAllMovement()
        {
            playerInputManager ??= PlayerInputManager.Instance;
            playerCamera ??= PlayerCamera.Instance;

            HandleGroundedMovement();
            HandleRotation();
        }

        private void HandleGroundedMovement()
        {
            if (playerInputManager == null || playerCamera == null || playerCamera.CameraObject == null)
            {
                return;
            }

            Vector3 moveDirection = GetCameraRelativeDirection();
            moveDirection.y = 0f;
            moveDirection.Normalize();

            float movementSpeed = playerInputManager.MoveAmount > 0.5f ? runningSpeed : walkingSpeed;

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            verticalVelocity += gravity * Time.deltaTime;
            Vector3 velocity = moveDirection * movementSpeed;
            velocity.y = verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }

        private void HandleRotation()
        {
            if (playerInputManager == null || playerCamera == null || playerCamera.CameraObject == null)
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
                rotationSpeed * Time.deltaTime);
        }

        private Vector3 GetCameraRelativeDirection()
        {
            Vector3 forward = playerCamera.CameraForward;
            Vector3 right = playerCamera.CameraRight;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return forward * playerInputManager.VerticalInput + right * playerInputManager.HorizontalInput;
        }
    }
}
