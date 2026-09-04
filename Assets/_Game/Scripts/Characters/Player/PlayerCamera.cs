using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace ZZ
{
    public class PlayerCamera : MonoBehaviour
    {
        /// <summary>ScaleVector2 processor applied to the &lt;Mouse&gt;/delta binding in PlayerControls.</summary>
        private const float k_PointerInputScale = 0.05f;

        /// <summary>Transform that carries camera feedback so the look rig keeps owning its own.</summary>
        private const string k_FeedbackPivotName = "Feedback Pivot";
        private const int k_ShakeVibrato = 10;
        private const float k_ShakeRandomness = 90f;

        private static PlayerCamera s_instance;
        public static PlayerCamera Instance => s_instance;

        [Header("References")]
        [FormerlySerializedAs("cameraPivotTransform")]
        [SerializeField] private Transform m_cameraPivotTransform;
        [SerializeField] private Transform m_feedbackPivot;
        [FormerlySerializedAs("cameraObject")]
        [SerializeField] private Camera m_cameraObject;

        [Header("Follow Settings")]
        [FormerlySerializedAs("cameraSmoothTime")]
        [SerializeField, Min(0.001f)] private float m_cameraSmoothTime = 0.1f;

        [Header("Spawn Presentation")]
        [SerializeField, Min(0f)] private float m_spawnIntroductionDuration = 1.25f;
        [SerializeField, Range(1f, 2f)]
        private float m_spawnIntroductionDistanceMultiplier = 1.35f;
        [SerializeField, Range(-20f, 20f)]
        private float m_spawnIntroductionPitchOffset = 8f;

        [Header("Rotation Settings")]
        [FormerlySerializedAs("leftAndRightRotationSpeed")]
        [SerializeField, Min(0f)] private float m_leftAndRightRotationSpeed = 220f;
        [FormerlySerializedAs("upAndDownRotationSpeed")]
        [SerializeField, Min(0f)] private float m_upAndDownRotationSpeed = 220f;

        [Header("Pointer Look Settings")]
        [Tooltip("Pointer look sensitivity in degrees per pixel of movement.")]
        [SerializeField, Min(0f)] private float m_pointerSensitivity = 0.18f;
        [Tooltip(
            "How fast pointer look catches up with the pointer. Higher is snappier, " +
            "lower glides longer and feels closer to a stick.")]
        [SerializeField, Min(0.01f)] private float m_pointerSmoothing = 20f;

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
        [SerializeField, Min(0f)] private float m_aimLensDuration = 0.2f;
        [SerializeField] private Ease m_aimLensEase = Ease.OutQuad;

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
        private Vector2 m_smoothedPointerLook;
        private float m_leftAndRightLookAngle;
        private float m_upAndDownLookAngle;
        private float m_cameraZPosition;
        private float m_targetCameraZPosition;
        private Vector3 m_standardPivotLocalPosition;
        private Vector3 m_standardCameraLocalPosition;
        private bool m_isAiming;
        private bool m_isSpawnIntroductionActive;
        private float m_spawnIntroductionElapsed;
        private float m_spawnIntroductionStartZ;
        private float m_spawnIntroductionCurrentZ;
        private float m_spawnIntroductionCurrentPitchOffset;
        private Tween m_feedbackTween;
        private Tween m_fieldOfViewTween;
        private Tween m_nearClipTween;

        public Camera CameraObject => m_cameraObject;
        public Vector3 CameraForward => m_cameraObject != null ? m_cameraObject.transform.forward : transform.forward;
        public Vector3 CameraRight => m_cameraObject != null ? m_cameraObject.transform.right : transform.right;
        /// <summary>Gets the normalized center-screen direction used for ranged aim.</summary>
        public Vector3 AimDirection { get; private set; } = Vector3.forward;

#if UNITY_EDITOR
        public void ConfigureRig(Transform pivot, Transform feedbackPivot, Camera mainCamera)
        {
            m_cameraPivotTransform = pivot;
            m_feedbackPivot = feedbackPivot;
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

            ResolveFeedbackPivot();
        }

        /// <summary>
        /// Camera feedback needs a transform nothing else writes to, because
        /// <see cref="HandleCollisions"/> rewrites the camera's local position every frame. The
        /// rig is authored by an editor setup step, so scenes that skipped it get a pivot here
        /// rather than keeping a stale cross-scene reference.
        /// </summary>
        private void ResolveFeedbackPivot()
        {
            if (m_cameraPivotTransform == null || m_cameraObject == null)
            {
                return;
            }

            m_feedbackPivot ??= m_cameraPivotTransform.Find(k_FeedbackPivotName);
            if (m_feedbackPivot == null)
            {
                m_feedbackPivot = new GameObject(k_FeedbackPivotName).transform;
            }

            m_feedbackPivot.SetParent(m_cameraPivotTransform, false);
            m_feedbackPivot.localPosition = Vector3.zero;
            m_feedbackPivot.localRotation = Quaternion.identity;
            m_feedbackPivot.localScale = Vector3.one;

            m_cameraObject.transform.SetParent(m_feedbackPivot, false);
        }

        /// <summary>
        /// Sells an impact by shaking the feedback pivot. Rotation is used rather than position so
        /// the spherecast origin in <see cref="HandleCollisions"/> stays where the look rig put it.
        /// </summary>
        /// <param name="strength">Shake strength in degrees.</param>
        /// <param name="duration">Shake duration in unscaled seconds.</param>
        public void Shake(float strength, float duration)
        {
            if (m_feedbackPivot == null || strength <= 0f || duration <= 0f)
            {
                return;
            }

            ClearFeedback();
            m_feedbackTween = m_feedbackPivot
                .DOShakeRotation(
                    duration,
                    strength,
                    k_ShakeVibrato,
                    k_ShakeRandomness,
                    true)
                .SetUpdate(true)
                .SetTarget(this);
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

            CancelSpawnIntroduction();
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

        /// <summary>
        /// Binds to the player's final restored position and eases from a modest
        /// establishing view into the standard third-person framing.
        /// </summary>
        public void BeginSpawnIntroduction(PlayerManager localPlayer)
        {
            SnapToPlayerAndResetRotation(localPlayer);
            if (m_cameraObject == null || m_spawnIntroductionDuration <= 0f)
            {
                return;
            }

            m_spawnIntroductionElapsed = 0f;
            m_spawnIntroductionStartZ =
                m_cameraZPosition * m_spawnIntroductionDistanceMultiplier;
            m_spawnIntroductionCurrentZ = m_spawnIntroductionStartZ;
            m_spawnIntroductionCurrentPitchOffset =
                m_spawnIntroductionPitchOffset;
            m_isSpawnIntroductionActive = true;

            m_cameraObjectPosition = m_cameraObject.transform.localPosition;
            m_cameraObjectPosition.z = m_spawnIntroductionCurrentZ;
            m_cameraObject.transform.localPosition = m_cameraObjectPosition;
            ApplyPivotRotation();
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
            UpdateSpawnIntroduction();
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
            if (isAiming)
            {
                CancelSpawnIntroduction();
            }

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
                ApplyAimLens(isAiming, forceRefresh);
                m_targetCameraZPosition = isAiming
                    ? 0f
                    : m_cameraZPosition;
                AimDirection = m_cameraObject.transform.forward.normalized;
            }

            PlayerUIManager.Instance?.PlayerUIHUDManager
                ?.SetCrosshairVisible(isAiming);
        }

        /// <summary>
        /// Eases field of view and near clip together. They are authored as a pair, so tweening
        /// only the field of view would briefly pair a wide lens with the aim near clip. Respawn
        /// and bind callers pass <paramref name="snap"/> so the lens lands before the first frame.
        /// </summary>
        private void ApplyAimLens(bool isAiming, bool snap)
        {
            m_fieldOfViewTween?.Kill();
            m_nearClipTween?.Kill();
            m_fieldOfViewTween = null;
            m_nearClipTween = null;

            float targetFieldOfView = isAiming ? m_aimFieldOfView : m_standardFieldOfView;
            float targetNearClipPlane = isAiming ? m_aimNearClipPlane : m_standardNearClipPlane;

            if (snap || m_aimLensDuration <= 0f)
            {
                m_cameraObject.fieldOfView = targetFieldOfView;
                m_cameraObject.nearClipPlane = targetNearClipPlane;
                return;
            }

            // Killing without completing lets a re-toggled aim pick up from the current lens, so
            // rapid toggling never snaps back to a framing the player has already left behind.
            m_fieldOfViewTween = DOTween.To(
                    () => m_cameraObject.fieldOfView,
                    value => m_cameraObject.fieldOfView = value,
                    targetFieldOfView,
                    m_aimLensDuration)
                .SetEase(m_aimLensEase)
                .SetUpdate(true)
                .SetTarget(this);
            m_nearClipTween = DOTween.To(
                    () => m_cameraObject.nearClipPlane,
                    value => m_cameraObject.nearClipPlane = value,
                    targetNearClipPlane,
                    m_aimLensDuration)
                .SetEase(m_aimLensEase)
                .SetUpdate(true)
                .SetTarget(this);
        }

        /// <summary>
        /// Stops any running shake and returns the feedback pivot to its rest pose, so an
        /// interrupted shake cannot leave the framing offset.
        /// </summary>
        private void ClearFeedback()
        {
            m_feedbackTween?.Kill();
            m_feedbackTween = null;

            if (m_feedbackPivot == null)
            {
                return;
            }

            m_feedbackPivot.localRotation = Quaternion.identity;
            m_feedbackPivot.localPosition = Vector3.zero;
        }

        /// <summary>
        /// Converts this frame's look input into a rotation in degrees. A pointer reports a
        /// per-frame movement, so it bypasses frame-time scaling and is smoothed instead,
        /// which gives it the same glide a stick gets from its analog travel.
        /// </summary>
        private Vector2 ConsumeLookRotation()
        {
            Vector2 lookInput = new Vector2(
                m_playerInputManager.CameraHorizontalInput,
                m_playerInputManager.CameraVerticalInput);
            if (!m_playerInputManager.IsPointerLook)
            {
                m_smoothedPointerLook = Vector2.zero;
                return new Vector2(
                    lookInput.x * m_leftAndRightRotationSpeed,
                    lookInput.y * m_upAndDownRotationSpeed) * Time.deltaTime;
            }

            Vector2 pointerDegrees =
                lookInput * (m_pointerSensitivity / k_PointerInputScale);
            float smoothingBlend = 1f - Mathf.Exp(-m_pointerSmoothing * Time.deltaTime);
            m_smoothedPointerLook = Vector2.Lerp(
                m_smoothedPointerLook,
                pointerDegrees,
                smoothingBlend);
            return m_smoothedPointerLook;
        }

        private void HandleAimRotations()
        {
            if (m_playerInputManager == null)
            {
                return;
            }

            Vector2 lookRotation = ConsumeLookRotation();
            m_leftAndRightLookAngle += lookRotation.x;
            m_upAndDownLookAngle -= lookRotation.y;
            m_upAndDownLookAngle = Mathf.Clamp(
                m_upAndDownLookAngle,
                m_minimumPivot,
                m_maximumPivot);
            transform.rotation = Quaternion.Euler(
                0f,
                m_leftAndRightLookAngle,
                0f);
            ApplyPivotRotation();
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

            // Consumed even while locked on so the pointer glide never carries stale movement.
            Vector2 lookRotation = ConsumeLookRotation();
            PlayerLockOnManager lockOnManager = m_player.LockOnManager;
            if (lockOnManager != null && lockOnManager.IsLockedOn)
            {
                CancelSpawnIntroduction();
                HandleLockOnRotations(lockOnManager.TargetAimPoint);
                return;
            }

            m_leftAndRightLookAngle += lookRotation.x;
            transform.rotation = Quaternion.Euler(0f, m_leftAndRightLookAngle, 0f);

            m_upAndDownLookAngle -= lookRotation.y;
            m_upAndDownLookAngle = Mathf.Clamp(m_upAndDownLookAngle, m_minimumPivot, m_maximumPivot);
            ApplyPivotRotation();
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
            m_targetCameraZPosition = m_isSpawnIntroductionActive
                ? m_spawnIntroductionCurrentZ
                : m_cameraZPosition;

            Vector3 direction = m_cameraObject.transform.position - m_cameraPivotTransform.position;
            direction.Normalize();

            float defaultCameraDistance = Mathf.Abs(m_targetCameraZPosition);
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
            float collisionBlend = 1f - Mathf.Exp(
                -m_cameraCollisionSmoothSpeed * Time.deltaTime);
            m_cameraObjectPosition.z = Mathf.Lerp(
                m_cameraObjectPosition.z,
                m_targetCameraZPosition,
                collisionBlend);
            m_cameraObject.transform.localPosition = m_cameraObjectPosition;
        }

        private void UpdateSpawnIntroduction()
        {
            if (!m_isSpawnIntroductionActive)
            {
                return;
            }

            m_spawnIntroductionElapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(
                m_spawnIntroductionElapsed / m_spawnIntroductionDuration);
            float easedTime = normalizedTime * normalizedTime *
                (3f - 2f * normalizedTime);
            m_spawnIntroductionCurrentZ = Mathf.Lerp(
                m_spawnIntroductionStartZ,
                m_cameraZPosition,
                easedTime);
            m_spawnIntroductionCurrentPitchOffset = Mathf.Lerp(
                m_spawnIntroductionPitchOffset,
                0f,
                easedTime);

            if (normalizedTime >= 1f)
            {
                CancelSpawnIntroduction();
            }
        }

        private void ApplyPivotRotation()
        {
            if (m_cameraPivotTransform == null)
            {
                return;
            }

            float pivotAngle = Mathf.Clamp(
                m_upAndDownLookAngle + m_spawnIntroductionCurrentPitchOffset,
                m_minimumPivot,
                m_maximumPivot);
            m_cameraPivotTransform.localRotation = Quaternion.Euler(
                pivotAngle,
                0f,
                0f);
        }

        private void CancelSpawnIntroduction()
        {
            m_isSpawnIntroductionActive = false;
            m_spawnIntroductionElapsed = 0f;
            m_spawnIntroductionCurrentZ = m_cameraZPosition;
            m_spawnIntroductionCurrentPitchOffset = 0f;
        }

        private void OnDestroy()
        {
            // Every tween this component starts carries SetTarget(this), so a single filtered kill
            // covers the shake and both lens tweens.
            DOTween.Kill(this);

            if (s_instance == this)
            {
                s_instance = null;
            }

            PlayerUIManager.Instance?.PlayerUIHUDManager
                ?.SetCrosshairVisible(false);
        }
    }
}
