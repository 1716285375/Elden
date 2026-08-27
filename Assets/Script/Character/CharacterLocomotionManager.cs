using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace ZZ
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterLocomotionManager : MonoBehaviour
    {
        private const int k_PlayerLayer = 8;
        private const int k_DamageableCharacterLayer = 10;

        [Header("Ground Check")]
        [SerializeField] private Transform m_groundCheckPoint;
        [SerializeField, Min(0.01f)] private float m_groundCheckRadius = 0.2f;
        [SerializeField] private LayerMask m_groundLayers = 1;

        [Header("Vertical Movement")]
        [SerializeField] private float m_groundedYVelocity = -20f;
        [SerializeField] private float m_fallStartYVelocity = -5f;
        [FormerlySerializedAs("m_gravity")]
        [FormerlySerializedAs("gravity")]
        [SerializeField] private float m_gravityForce = -40f;

        [Header("Slope Sliding")]
        [SerializeField, Min(0f)] private float m_slopeSlideStartPositionYOffset = 1f;
        [SerializeField, Min(0f)] private float m_slopeSlideSphereCastMaxDistance = 2f;
        [SerializeField, Range(0f, 90f)] private float m_slipperySurfaceMaxAngle = 15f;
        [SerializeField, Min(0f)] private float m_slopeSlideSpeed = 11f;
        [SerializeField, Min(0f)] private float m_slopeSlideSpeedMultiplier = 3f;
        [SerializeField, Range(-100f, 0f)] private float m_slopeSlideForce = -5f;
        [SerializeField] private bool m_ignoreGravity;

        [Header("Character Sliding")]
        [SerializeField, Min(1f)]
        private float m_characterCollisionCheckSphereMultiplier = 1.5f;
        [SerializeField, Min(0.01f)]
        private float m_characterSlideOffHeadCollisionMaxDistance = 5f;

        protected CharacterController m_characterController;
        protected CharacterManager m_characterManager;
        protected Vector3 m_verticalVelocity;
        protected Vector3 m_slopeSlideVelocity;
        protected bool m_isSliding;
        protected bool m_slideUntilGrounded;
        protected bool m_isSlidingOffCharacter;

        private float m_inAirTimer;
        private bool m_hasSetFallingVelocity;
        private bool m_canRun = true;
        private bool m_canRoll = true;
        private Vector3 m_characterSlideVelocity;
        private Coroutine m_slideOffCharacterCoroutine;

        /// <summary>
        /// Gets the uninterrupted time in seconds since the ground probe lost contact.
        /// </summary>
        public float InAirTimer => m_inAirTimer;

        /// <summary>
        /// Gets the currently integrated vertical movement velocity.
        /// </summary>
        public float VerticalVelocity => m_verticalVelocity.y;
        /// <summary>Gets whether the current action permits run-speed movement.</summary>
        public bool CanRun => m_canRun;
        /// <summary>Gets whether the current action permits a dodge.</summary>
        public bool CanRoll => m_canRoll;
        /// <summary>Gets whether this character is currently sliding down a detected surface.</summary>
        public bool IsSliding => m_isSliding;
        /// <summary>Gets the current surface-projected slope velocity.</summary>
        public Vector3 SlopeSlideVelocity => m_slopeSlideVelocity;
        /// <summary>Gets the EP126 continuation flag reserved by the slope state machine.</summary>
        public bool SlideUntilGrounded => m_slideUntilGrounded;
        /// <summary>Gets whether a temporary character-surface slide is active.</summary>
        public bool IsSlidingOffCharacter => m_isSlidingOffCharacter;
        /// <summary>Gets the current velocity projected along another character.</summary>
        public Vector3 CharacterSlideVelocity => m_characterSlideVelocity;
        /// <summary>Gets whether gravity and slope movement are temporarily disabled.</summary>
        public bool IsIgnoringGravity => m_ignoreGravity;
        protected float GravityForce => m_gravityForce;

        /// <summary>Restricts movement to walking without disabling directional control.</summary>
        public void SetCanRun(bool canRun)
        {
            m_canRun = canRun;
        }

        /// <summary>Allows authored upper-body actions to gate dodge input.</summary>
        public void SetCanRoll(bool canRoll)
        {
            m_canRoll = canRoll;
        }

        /// <summary>Enables or disables gravity-driven vertical and slope movement.</summary>
        public void SetIgnoreGravity(bool shouldIgnoreGravity)
        {
            m_ignoreGravity = shouldIgnoreGravity;
            if (m_ignoreGravity)
            {
                ClearSlopeSlideState();
                StopSlidingOffCharacter();
                m_slideUntilGrounded = false;
                m_verticalVelocity = Vector3.zero;
            }
        }

        protected virtual void Awake()
        {
            m_characterController = GetComponent<CharacterController>();
            m_characterManager = GetComponent<CharacterManager>();
        }

        protected void HandleGroundCheck()
        {
            if (m_characterManager == null || m_characterController == null)
            {
                return;
            }

            if (m_characterManager.IsJumping && m_verticalVelocity.y > 0f)
            {
                UpdateGroundedState(false);
                m_inAirTimer += Time.deltaTime;
                return;
            }

            int groundLayerMask = m_groundLayers.value;
            if (WorldUtilityManager.Instance != null)
            {
                groundLayerMask |= WorldUtilityManager.Instance
                    .GetGroundLayers().value;
            }

            bool isGrounded = Physics.CheckSphere(
                GetGroundCheckPosition(m_characterController),
                m_groundCheckRadius,
                groundLayerMask,
                QueryTriggerInteraction.Ignore);
            UpdateGroundedState(isGrounded);

            if (isGrounded)
            {
                m_inAirTimer = 0f;
                return;
            }

            m_inAirTimer += Time.deltaTime;
        }

        protected void HandleVerticalMovement()
        {
            if (m_characterManager == null ||
                m_characterController == null ||
                !m_characterController.enabled ||
                m_ignoreGravity)
            {
                return;
            }

            if (m_characterManager.IsGrounded && m_verticalVelocity.y < 0f)
            {
                m_inAirTimer = 0f;
                m_hasSetFallingVelocity = false;
            }
            else
            {
                if (!m_characterManager.IsGrounded &&
                    !m_characterManager.IsJumping &&
                    !m_hasSetFallingVelocity)
                {
                    m_verticalVelocity.y = m_fallStartYVelocity;
                    m_hasSetFallingVelocity = true;
                }

                m_verticalVelocity.y += m_gravityForce * Time.deltaTime;
            }

            m_characterController.Move(m_verticalVelocity * Time.deltaTime);
        }

        /// <summary>Chooses falling or grounded slope surfaces without duplicating probe logic.</summary>
        protected void HandleSlopeSlideCheck()
        {
            if (m_characterManager == null || m_ignoreGravity)
            {
                ClearSlopeSlideState();
                return;
            }

            HandleCharacterCollisionCheck();
            if (m_isSlidingOffCharacter)
            {
                ClearSlopeSlideState();
                return;
            }

            WorldUtilityManager utilityManager = WorldUtilityManager.Instance;
            LayerMask layerMask;
            if (m_characterManager.IsGrounded)
            {
                layerMask = utilityManager != null
                    ? utilityManager.GetSlipperyEnviroLayers()
                    : 0;
            }
            else if (m_slideUntilGrounded)
            {
                layerMask = utilityManager != null
                    ? utilityManager.GetEnvironmentLayers()
                    : m_groundLayers;
            }
            else
            {
                layerMask = 0;
            }

            SetSlopeSlideVelocity(layerMask);
        }

        /// <summary>Runs once when the ground probe enters a grounded state.</summary>
        protected virtual void OnIsGrounded()
        {
            m_slideUntilGrounded = false;
            StopSlidingOffCharacter();
            m_characterManager?.EndJump();
        }

        /// <summary>Runs once when the ground probe leaves a grounded state.</summary>
        protected virtual void OnIsNotGrounded()
        {
        }

        /// <summary>Starts one replacement coroutine that slides off a character surface.</summary>
        protected void SlideOffCharacter()
        {
            StopSlidingOffCharacter();
            m_isSlidingOffCharacter = true;
            m_slideOffCharacterCoroutine = StartCoroutine(
                SlideOffCharacterCoroutine());
        }

        /// <summary>Projects vertical velocity along another character's surface.</summary>
        public static Vector3 CalculateCharacterSlideVelocity(
            float verticalVelocity,
            Vector3 surfaceNormal)
        {
            return Vector3.ProjectOnPlane(
                new Vector3(0f, verticalVelocity, 0f),
                surfaceNormal);
        }

        /// <summary>Applies the previously detected slope velocity and grounded vertical force.</summary>
        protected void SetGroundedVelocity()
        {
            if (m_characterManager == null ||
                m_characterController == null ||
                !m_characterController.enabled ||
                m_ignoreGravity)
            {
                return;
            }

            if (m_characterManager.IsJumping && m_verticalVelocity.y > 0f)
            {
                ClearSlopeSlideState();
                return;
            }

            if (m_isSliding)
            {
                float maximumDownwardVelocity =
                    m_groundedYVelocity + m_slopeSlideForce;
                m_verticalVelocity.y = Mathf.Max(
                    m_verticalVelocity.y + m_slopeSlideForce * Time.deltaTime,
                    maximumDownwardVelocity);
                m_characterController.Move(
                    m_slopeSlideVelocity * Time.deltaTime);
                return;
            }

            if (m_characterManager.IsGrounded && m_verticalVelocity.y < 0f)
            {
                m_verticalVelocity.y = m_groundedYVelocity;
            }
        }

        /// <summary>Snaps a severely desynchronized non-owner back to replicated position.</summary>
        protected void CorrectRemotePositionDesynchronization()
        {
            if (m_characterManager == null ||
                m_characterManager.IsOwner ||
                m_characterManager.CharacterNetworkManager == null)
            {
                return;
            }

            Vector3 networkPosition = m_characterManager
                .CharacterNetworkManager.NetworkPosition.Value;
            if ((transform.position - networkPosition).sqrMagnitude <= 6.25f)
            {
                return;
            }

            m_verticalVelocity = Vector3.zero;
            ClearSlopeSlideState();
            StopSlidingOffCharacter();
            m_slideUntilGrounded = false;
            bool controllerWasEnabled = m_characterController != null &&
                m_characterController.enabled;
            if (controllerWasEnabled)
            {
                m_characterController.enabled = false;
            }

            transform.position = networkPosition;
            if (controllerWasEnabled)
            {
                m_characterController.enabled = true;
            }
        }

        /// <summary>Calculates a velocity tangent to a surface from a downward force.</summary>
        public static Vector3 CalculateSlopeSlideVelocity(
            Vector3 downwardForce,
            Vector3 surfaceNormal,
            float slideSpeed)
        {
            Vector3 slideDirection = Vector3.ProjectOnPlane(
                downwardForce,
                surfaceNormal);
            if (slideDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            return slideDirection.normalized * Mathf.Max(0f, slideSpeed);
        }

        protected void SetSlopeSlideVelocity(LayerMask layerMask)
        {
            if (m_characterController == null || layerMask.value == 0)
            {
                ClearSlopeSlideState();
                return;
            }

            Vector3 probeOrigin = transform.position +
                Vector3.up * m_slopeSlideStartPositionYOffset;
            bool hitSurface = Physics.SphereCast(
                probeOrigin,
                m_groundCheckRadius,
                Vector3.down,
                out RaycastHit hitInfo,
                m_slopeSlideSphereCastMaxDistance,
                layerMask,
                QueryTriggerInteraction.Ignore);
            float slopeAngle = hitSurface
                ? Vector3.Angle(hitInfo.normal, Vector3.up)
                : 0f;
            if (!hitSurface || slopeAngle < m_slipperySurfaceMaxAngle)
            {
                ClearSlopeSlideState();
                return;
            }

            Vector3 targetVelocity = CalculateSlopeSlideVelocity(
                Vector3.down,
                hitInfo.normal,
                m_slopeSlideSpeed);
            float acceleration = m_slopeSlideSpeed *
                m_slopeSlideSpeedMultiplier;
            m_slopeSlideVelocity = Vector3.MoveTowards(
                m_slopeSlideVelocity,
                targetVelocity,
                acceleration * Time.deltaTime);
            m_isSliding = m_slopeSlideVelocity.sqrMagnitude > Mathf.Epsilon;
            if (m_characterManager.IsJumping && m_verticalVelocity.y > 0f)
            {
                ClearSlopeSlideState();
            }
        }

        protected void ResetAirborneMovement()
        {
            m_verticalVelocity = Vector3.zero;
            ClearSlopeSlideState();
            StopSlidingOffCharacter();
            m_slideUntilGrounded = false;
            m_inAirTimer = 0f;
            m_hasSetFallingVelocity = false;
            m_characterManager?.EndJump();
        }

        private void HandleCharacterCollisionCheck()
        {
            if (m_characterManager == null ||
                m_characterController == null ||
                m_characterManager.IsGrounded ||
                m_isSlidingOffCharacter)
            {
                return;
            }

            LayerMask characterLayers = GetCharacterLayers();
            float checkRadius = m_groundCheckRadius *
                m_characterCollisionCheckSphereMultiplier;
            Collider[] colliders = Physics.OverlapSphere(
                GetGroundCheckPosition(m_characterController),
                checkRadius,
                characterLayers,
                QueryTriggerInteraction.Ignore);
            foreach (Collider characterCollider in colliders)
            {
                if (characterCollider.transform.root == transform.root)
                {
                    continue;
                }

                CharacterController otherCharacterController =
                    characterCollider.GetComponent<CharacterController>();
                if (otherCharacterController == null)
                {
                    continue;
                }

                if ((m_characterController.collisionFlags &
                        CollisionFlags.Below) == 0)
                {
                    continue;
                }

                SlideOffCharacter();
                break;
            }
        }

        private IEnumerator SlideOffCharacterCoroutine()
        {
            while (m_characterManager != null &&
                !m_characterManager.IsGrounded)
            {
                if (TryGetCharacterSurfaceHit(out RaycastHit hitInfo))
                {
                    m_verticalVelocity.y += m_slopeSlideForce *
                        Time.deltaTime;
                    m_characterSlideVelocity =
                        CalculateCharacterSlideVelocity(
                            m_verticalVelocity.y,
                            hitInfo.normal);
                    if (m_characterController != null &&
                        m_characterController.enabled)
                    {
                        m_characterController.Move(
                            m_characterSlideVelocity * Time.deltaTime);
                    }
                }

                yield return null;
            }

            m_characterSlideVelocity = Vector3.zero;
            m_isSlidingOffCharacter = false;
            m_slideOffCharacterCoroutine = null;
        }

        private bool TryGetCharacterSurfaceHit(out RaycastHit surfaceHit)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                transform.position,
                m_groundCheckRadius,
                Vector3.down,
                m_characterSlideOffHeadCollisionMaxDistance,
                GetCharacterLayers(),
                QueryTriggerInteraction.Ignore);
            surfaceHit = default;
            bool foundSurface = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null ||
                    hit.collider.transform.root == transform.root ||
                    hit.collider.GetComponent<CharacterController>() == null)
                {
                    continue;
                }

                if (!foundSurface || hit.distance < surfaceHit.distance)
                {
                    surfaceHit = hit;
                    foundSurface = true;
                }
            }

            return foundSurface;
        }

        private LayerMask GetCharacterLayers()
        {
            if (WorldUtilityManager.Instance != null)
            {
                return WorldUtilityManager.Instance.GetCharacterLayers();
            }

            return (1 << k_PlayerLayer) |
                (1 << k_DamageableCharacterLayer);
        }

        private void UpdateGroundedState(bool isGrounded)
        {
            bool wasGrounded = m_characterManager.IsGrounded;
            m_characterManager.SetGroundedState(isGrounded);
            if (wasGrounded == isGrounded)
            {
                return;
            }

            if (isGrounded)
            {
                OnIsGrounded();
                return;
            }

            OnIsNotGrounded();
        }

        private void StopSlidingOffCharacter()
        {
            if (m_slideOffCharacterCoroutine != null)
            {
                StopCoroutine(m_slideOffCharacterCoroutine);
                m_slideOffCharacterCoroutine = null;
            }

            m_characterSlideVelocity = Vector3.zero;
            m_isSlidingOffCharacter = false;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit != null && m_characterManager?.IsGrounded == false)
            {
                m_slideUntilGrounded = true;
            }
        }

        private void OnDisable()
        {
            StopSlidingOffCharacter();
        }

        private void ClearSlopeSlideState()
        {
            m_slopeSlideVelocity = Vector3.zero;
            m_isSliding = false;
        }

        private Vector3 GetGroundCheckPosition(CharacterController controller)
        {
            if (m_groundCheckPoint != null)
            {
                return m_groundCheckPoint.position;
            }

            float bottomOffset = Mathf.Max(
                0f,
                (controller.height * 0.5f) - (m_groundCheckRadius * 0.5f));
            return transform.TransformPoint(
                controller.center + Vector3.down * bottomOffset);
        }

        private void OnDrawGizmosSelected()
        {
            CharacterController controller = m_characterController;
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            if (controller == null)
            {
                return;
            }

            bool isGrounded = m_characterManager != null && m_characterManager.IsGrounded;
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(
                GetGroundCheckPosition(controller),
                m_groundCheckRadius);
        }
    }
}
