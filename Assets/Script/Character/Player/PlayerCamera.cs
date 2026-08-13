using UnityEngine;

namespace ZZ
{
    public class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera instance;
        public static PlayerCamera Instance => instance;

        [Header("References")]
        [SerializeField] private Transform cameraPivotTransform;
        [SerializeField] private Camera cameraObject;

        [Header("Follow Settings")]
        [SerializeField, Min(0.001f)] private float cameraSmoothTime = 0.1f;

        [Header("Rotation Settings")]
        [SerializeField, Min(0f)] private float leftAndRightRotationSpeed = 220f;
        [SerializeField, Min(0f)] private float upAndDownRotationSpeed = 220f;
        [SerializeField] private float minimumPivot = -30f;
        [SerializeField] private float maximumPivot = 60f;

        [Header("Collision Settings")]
        [SerializeField, Min(0.01f)] private float cameraCollisionRadius = 0.2f;
        [SerializeField, Min(0f)] private float cameraCollisionSmoothSpeed = 10f;
        [SerializeField] private LayerMask collideWithLayers = 1;

        private PlayerManager player;
        private PlayerInputManager playerInputManager;
        private Vector3 cameraVelocity;
        private Vector3 cameraObjectPosition;
        private float leftAndRightLookAngle;
        private float upAndDownLookAngle;
        private float cameraZPosition;
        private float targetCameraZPosition;

        public Camera CameraObject => cameraObject;
        public Vector3 CameraForward => cameraObject != null ? cameraObject.transform.forward : transform.forward;
        public Vector3 CameraRight => cameraObject != null ? cameraObject.transform.right : transform.right;

#if UNITY_EDITOR
        public void SetCameraObject(Camera value)
        {
            cameraObject = value;
        }
#endif

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (cameraObject == null)
            {
                cameraObject = GetComponentInChildren<Camera>();
            }

            if (cameraPivotTransform == null && cameraObject != null)
            {
                cameraPivotTransform = cameraObject.transform.parent;
            }

            if (cameraObject != null)
            {
                cameraZPosition = cameraObject.transform.localPosition.z;
                targetCameraZPosition = cameraZPosition;
            }
        }

        public void BindPlayer(PlayerManager localPlayer)
        {
            player = localPlayer;
        }

        public void ClearPlayer(PlayerManager localPlayer)
        {
            if (player == localPlayer)
            {
                player = null;
            }
        }

        public void HandleAllCameraActions()
        {
            if (player == null || cameraPivotTransform == null || cameraObject == null)
            {
                return;
            }

            playerInputManager ??= PlayerInputManager.Instance;

            FollowPlayer();
            HandleRotations();
            HandleCollisions();
        }

        private void FollowPlayer()
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                player.transform.position,
                ref cameraVelocity,
                cameraSmoothTime);
        }

        private void HandleRotations()
        {
            if (playerInputManager == null)
            {
                return;
            }

            leftAndRightLookAngle += playerInputManager.CameraHorizontalInput
                * leftAndRightRotationSpeed
                * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, leftAndRightLookAngle, 0f);

            upAndDownLookAngle -= playerInputManager.CameraVerticalInput
                * upAndDownRotationSpeed
                * Time.deltaTime;
            upAndDownLookAngle = Mathf.Clamp(upAndDownLookAngle, minimumPivot, maximumPivot);
            cameraPivotTransform.localRotation = Quaternion.Euler(upAndDownLookAngle, 0f, 0f);
        }

        private void HandleCollisions()
        {
            targetCameraZPosition = cameraZPosition;

            Vector3 direction = cameraObject.transform.position - cameraPivotTransform.position;
            direction.Normalize();

            float defaultCameraDistance = Mathf.Abs(cameraZPosition);
            if (direction.sqrMagnitude > 0f && Physics.SphereCast(
                    cameraPivotTransform.position,
                    cameraCollisionRadius,
                    direction,
                    out RaycastHit hit,
                    defaultCameraDistance,
                    collideWithLayers,
                    QueryTriggerInteraction.Ignore))
            {
                float targetDistance = Mathf.Max(hit.distance - cameraCollisionRadius, 0f);
                targetCameraZPosition = -targetDistance;
            }

            cameraObjectPosition = cameraObject.transform.localPosition;
            cameraObjectPosition.z = Mathf.Lerp(
                cameraObjectPosition.z,
                targetCameraZPosition,
                cameraCollisionSmoothSpeed * Time.deltaTime);
            cameraObject.transform.localPosition = cameraObjectPosition;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
