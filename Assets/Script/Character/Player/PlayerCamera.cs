using UnityEngine;
using UnityEngine.Serialization;

namespace ZZ
{
    public class PlayerCamera : MonoBehaviour
    {
        private static PlayerCamera s_instance;
        public static PlayerCamera Instance => s_instance;

        [Header("References")]
        [FormerlySerializedAs("cameraPivotTransform")]
        [SerializeField] private Transform m_cameraPivotTransform;
        [FormerlySerializedAs("cameraObject")]
        [SerializeField] private Camera m_cameraObject;

        [Header("Follow Settings")]
        [FormerlySerializedAs("cameraSmoothTime")]
        [SerializeField, Min(0.001f)] private float m_cameraSmoothTime = 0.1f;

        [Header("Rotation Settings")]
        [FormerlySerializedAs("leftAndRightRotationSpeed")]
        [SerializeField, Min(0f)] private float m_leftAndRightRotationSpeed = 220f;
        [FormerlySerializedAs("upAndDownRotationSpeed")]
        [SerializeField, Min(0f)] private float m_upAndDownRotationSpeed = 220f;
        [FormerlySerializedAs("minimumPivot")]
        [SerializeField] private float m_minimumPivot = -30f;
        [FormerlySerializedAs("maximumPivot")]
        [SerializeField] private float m_maximumPivot = 60f;
        [SerializeField, Min(0f)] private float m_lockOnRotationSpeed = 12f;

        [Header("Aim Settings")]
        [SerializeField, Min(1f)] private float m_standardFieldOfView = 60f;
        [SerializeField, Min(1f)] private float m_aimFieldOfView = 40f;
        [SerializeField, Min(0.01f)] private float m_standardNearClipPlane = 0.3f;
        [SerializeField, Min(0.01f)] private float m_aimNearClipPlane = 1.3f;

        [Header("Collision Settings")]
        [FormerlySerializedAs("cameraCollisionRadius")]
        [SerializeField, Min(0.01f)] private float m_cameraCollisionRadius = 0.2f;
        [FormerlySerializedAs("cameraCollisionSmoothSpeed")]
        [SerializeField, Min(0f)] private float m_cameraCollisionSmoothSpeed = 10f;
        [FormerlySerializedAs("collideWithLayers")]
        [SerializeField] private LayerMask m_collideWithLayers = 1;

        [FormerlySerializedAs("player")]
        [SerializeField] private PlayerManager m_player;
        private PlayerInputManager m_playerInputManager;
        private Vector3 m_cameraVelocity;
        private Vector3 m_cameraObjectPosition;
        private float m_leftAndRightLookAngle;
        private float m_upAndDownLookAngle;
        private float m_cameraZPosition;
        private float m_targetCameraZPosition;
        private Vector3 m_standardPivotLocalPosition;
        private Vector3 m_standardCameraLocalPosition;
        private bool m_isAiming;

        public Camera CameraObject => m_cameraObject;
        public Vector3 CameraForward => m_cameraObject != null ? m_cameraObject.transform.forward : transform.forward;
        public Vector3 CameraRight => m_cameraObject != null ? m_cameraObject.transform.right : transform.right;
        /// <summary>Gets the normalized center-screen direction used for ranged aim.</summary>
        public Vector3 AimDirection { get; private set; } = Vector3.forward;

#if UNITY_EDITOR
        public void ConfigureRig(Transform pivot, Camera mainCamera)
        {
            m_cameraPivotTransform = pivot;
            m_cameraObject = mainCamera;
        }
#endif

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);

            if (m_cameraObject == null)
            {
                m_cameraObject = GetComponentInChildren<Camera>();
            }

            if (m_cameraPivotTransform == null && m_cameraObject != null)
            {
                m_cameraPivotTransform = m_cameraObject.transform.parent;
            }

            if (m_cameraObject != null)
            {
                m_standardCameraLocalPosition =
                    m_cameraObject.transform.localPosition;
                m_cameraZPosition = m_cameraObject.transform.localPosition.z;
                m_targetCameraZPosition = m_cameraZPosition;
            }

            if (m_cameraPivotTransform != null)
            {
                m_standardPivotLocalPosition =
                    m_cameraPivotTransform.localPosition;
            }
        }

        public void BindPlayer(PlayerManager localPlayer)
        {
            if (localPlayer == null || m_player == localPlayer)
            {
                return;
            }

            m_player = localPlayer;
            m_cameraVelocity = Vector3.zero;
            transform.position = localPlayer.transform.position;
        }

        public void SnapToPlayerAndResetRotation(PlayerManager localPlayer)
        {
            if (localPlayer == null)
            {
                return;
            }

            m_player = localPlayer;
            m_cameraVelocity = Vector3.zero;
            transform.position = localPlayer.transform.position;

            m_leftAndRightLookAngle = 0f;
            m_upAndDownLookAngle = 0f;
            transform.rotation = Quaternion.identity;
            if (m_cameraPivotTransform != null)
            {
                m_cameraPivotTransform.localRotation = Quaternion.identity;
            }

            if (m_cameraObject != null)
            {
                m_cameraObjectPosition = m_cameraObject.transform.localPosition;
                m_cameraObjectPosition.z = m_cameraZPosition;
                m_cameraObject.transform.localPosition = m_cameraObjectPosition;
                m_targetCameraZPosition = m_cameraZPosition;
            }


            SetAimMode(false, true);
        }

        public void ClearPlayer(PlayerManager localPlayer)
        {
            if (m_player == localPlayer)
            {
                m_player = null;
            }
        }

        public void HandleAllCameraActions()
        {
            if (m_player == null || m_cameraPivotTransform == null || m_cameraObject == null)
            {
                return;
            }

            m_playerInputManager ??= PlayerInputManager.Instance;

            FollowPlayer();
            if (m_isAiming)
            {
                HandleAimRotations();
            }
            else
            {
                HandleRotations();
                HandleCollisions();
            }

            AimDirection = m_cameraObject.transform.forward.normalized;
        }

        private void FollowPlayer()
        {
            Vector3 targetPosition = m_isAiming
                ? m_player.LockOnTransform.position
                : m_player.transform.position;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref m_cameraVelocity,
                m_cameraSmoothTime);
        }

        /// <summary>Switches the local camera between third-person and center-origin aim.</summary>
        public void SetAimMode(bool isAiming, bool forceRefresh = false)
        {
            if (!forceRefresh && m_isAiming == isAiming)
            {
                return;
            }

            m_isAiming = isAiming;
            m_cameraVelocity = Vector3.zero;
            ResetAimRotations();
            if (m_cameraPivotTransform != null)
            {
                m_cameraPivotTransform.localPosition = isAiming
                    ? Vector3.zero
                    : m_standardPivotLocalPosition;
            }

            if (m_cameraObject != null)
            {
                m_cameraObject.transform.localPosition = isAiming
                    ? Vector3.zero
                    : m_standardCameraLocalPosition;
                m_cameraObject.transform.localRotation = Quaternion.identity;
                m_cameraObject.fieldOfView = isAiming
                    ? m_aimFieldOfView
                    : m_standardFieldOfView;
                m_cameraObject.nearClipPlane = isAiming
                    ? m_aimNearClipPlane
                    : m_standardNearClipPlane;
                m_targetCameraZPosition = isAiming
                    ? 0f
                    : m_cameraZPosition;
                AimDirection = m_cameraObject.transform.forward.normalized;
            }

            PlayerUIManager.Instance?.PlayerUIHUDManager
                ?.SetCrosshairVisible(isAiming);
        }

        private void HandleAimRotations()
        {
            if (m_playerInputManager == null)
            {
                return;
            }

            m_leftAndRightLookAngle += m_playerInputManager.CameraHorizontalInput
                * m_leftAndRightRotationSpeed
                * Time.deltaTime;
            m_upAndDownLookAngle -= m_playerInputManager.CameraVerticalInput
                * m_upAndDownRotationSpeed
                * Time.deltaTime;
            m_upAndDownLookAngle = Mathf.Clamp(
                m_upAndDownLookAngle,
                m_minimumPivot,
                m_maximumPivot);
            transform.rotation = Quaternion.Euler(
                0f,
                m_leftAndRightLookAngle,
                0f);
            m_cameraPivotTransform.localRotation = Quaternion.Euler(
                m_upAndDownLookAngle,
                0f,
                0f);
        }

        private void ResetAimRotations()
        {
            float playerYaw = m_player != null
                ? m_player.transform.eulerAngles.y
                : transform.eulerAngles.y;
            m_leftAndRightLookAngle = playerYaw;
            m_upAndDownLookAngle = 0f;
            transform.rotation = Quaternion.Euler(0f, playerYaw, 0f);
            if (m_cameraPivotTransform != null)
            {
                m_cameraPivotTransform.localRotation = Quaternion.identity;
            }

            if (m_cameraObject != null)
            {
                m_cameraObject.transform.localRotation = Quaternion.identity;
            }
        }

        private void HandleRotations()
        {
            if (m_playerInputManager == null)
            {
                return;
            }

            PlayerLockOnManager lockOnManager = m_player.LockOnManager;
            if (lockOnManager != null && lockOnManager.IsLockedOn)
            {
                HandleLockOnRotations(lockOnManager.TargetAimPoint);
                return;
            }

            m_leftAndRightLookAngle += m_playerInputManager.CameraHorizontalInput
                * m_leftAndRightRotationSpeed
                * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, m_leftAndRightLookAngle, 0f);

            m_upAndDownLookAngle -= m_playerInputManager.CameraVerticalInput
                * m_upAndDownRotationSpeed
                * Time.deltaTime;
            m_upAndDownLookAngle = Mathf.Clamp(m_upAndDownLookAngle, m_minimumPivot, m_maximumPivot);
            m_cameraPivotTransform.localRotation = Quaternion.Euler(m_upAndDownLookAngle, 0f, 0f);
        }

        private void HandleLockOnRotations(Vector3 targetPoint)
        {
            Vector3 horizontalDirection = targetPoint - m_player.transform.position;
            horizontalDirection.y = 0f;
            if (horizontalDirection.sqrMagnitude > Mathf.Epsilon)
            {
                Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    m_lockOnRotationSpeed * Time.deltaTime);
                m_leftAndRightLookAngle = transform.eulerAngles.y;
            }

            Vector3 pivotDirection = targetPoint - m_cameraPivotTransform.position;
            Vector3 localPivotDirection = transform.InverseTransformDirection(
                pivotDirection.normalized);
            float horizontalDistance = new Vector2(
                localPivotDirection.x,
                localPivotDirection.z).magnitude;
            float targetPivotAngle = -Mathf.Atan2(
                localPivotDirection.y,
                horizontalDistance) * Mathf.Rad2Deg;
            targetPivotAngle = Mathf.Clamp(
                targetPivotAngle,
                m_minimumPivot,
                m_maximumPivot);
            m_upAndDownLookAngle = Mathf.LerpAngle(
                m_upAndDownLookAngle,
                targetPivotAngle,
                m_lockOnRotationSpeed * Time.deltaTime);
            m_cameraPivotTransform.localRotation = Quaternion.Euler(
                m_upAndDownLookAngle,
                0f,
                0f);
        }

        private void HandleCollisions()
        {
            m_targetCameraZPosition = m_cameraZPosition;

            Vector3 direction = m_cameraObject.transform.position - m_cameraPivotTransform.position;
            direction.Normalize();

            float defaultCameraDistance = Mathf.Abs(m_cameraZPosition);
            if (direction.sqrMagnitude > 0f && Physics.SphereCast(
                    m_cameraPivotTransform.position,
                    m_cameraCollisionRadius,
                    direction,
                    out RaycastHit hit,
                    defaultCameraDistance,
                    m_collideWithLayers,
                    QueryTriggerInteraction.Ignore))
            {
                float targetDistance = Mathf.Max(hit.distance - m_cameraCollisionRadius, 0f);
                m_targetCameraZPosition = -targetDistance;
            }

            m_cameraObjectPosition = m_cameraObject.transform.localPosition;
            m_cameraObjectPosition.z = Mathf.Lerp(
                m_cameraObjectPosition.z,
                m_targetCameraZPosition,
                m_cameraCollisionSmoothSpeed * Time.deltaTime);
            m_cameraObject.transform.localPosition = m_cameraObjectPosition;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }

            PlayerUIManager.Instance?.PlayerUIHUDManager
                ?.SetCrosshairVisible(false);
        }
    }
}
