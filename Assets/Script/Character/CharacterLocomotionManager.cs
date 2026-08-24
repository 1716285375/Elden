using UnityEngine;
using UnityEngine.Serialization;

namespace ZZ
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterLocomotionManager : MonoBehaviour
    {
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

        protected CharacterController m_characterController;
        protected CharacterManager m_characterManager;
        protected Vector3 m_verticalVelocity;

        private float m_inAirTimer;
        private bool m_hasSetFallingVelocity;
        private bool m_canRun = true;

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
        protected float GravityForce => m_gravityForce;

        /// <summary>Restricts movement to walking without disabling directional control.</summary>
        public void SetCanRun(bool canRun)
        {
            m_canRun = canRun;
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

            bool wasGrounded = m_characterManager.IsGrounded;
            bool isGrounded = Physics.CheckSphere(
                GetGroundCheckPosition(m_characterController),
                m_groundCheckRadius,
                m_groundLayers,
                QueryTriggerInteraction.Ignore);
            m_characterManager.SetGroundedState(isGrounded);

            if (isGrounded)
            {
                m_inAirTimer = 0f;
                if (!wasGrounded)
                {
                    m_characterManager.EndJump();
                }

                return;
            }

            m_inAirTimer += Time.deltaTime;
        }

        protected void HandleVerticalMovement()
        {
            if (m_characterManager == null ||
                m_characterController == null ||
                !m_characterController.enabled)
            {
                return;
            }

            if (m_characterManager.IsGrounded && m_verticalVelocity.y < 0f)
            {
                m_inAirTimer = 0f;
                m_hasSetFallingVelocity = false;
                m_verticalVelocity.y = m_groundedYVelocity;
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

        protected void ResetAirborneMovement()
        {
            m_verticalVelocity = Vector3.zero;
            m_inAirTimer = 0f;
            m_hasSetFallingVelocity = false;
            m_characterManager?.EndJump();
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
