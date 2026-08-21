using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterNetworkManager))]
    public class CharacterManager : NetworkBehaviour
    {
        [SerializeField] private Animator m_animator;
        [SerializeField] private CharacterAnimatorManager m_characterAnimatorManager;
        [SerializeField] private CharacterNetworkManager m_characterNetworkManager;

        private CharacterStatsManager m_characterStatsManager;
        private bool m_isGrounded = true;
        private bool m_isJumping;
        private bool m_isPerformingAction;
        private bool m_canMove = true;
        private bool m_canRotate = true;
        private bool m_shouldApplyRootMotion;

        public CharacterAnimatorManager CharacterAnimatorManager => m_characterAnimatorManager;
        public CharacterNetworkManager CharacterNetworkManager => m_characterNetworkManager;
        public CharacterStatsManager CharacterStatsManager => m_characterStatsManager;
        /// <summary>
        /// Gets whether the character's ground probe currently detects walkable environment.
        /// </summary>
        public bool IsGrounded => m_isGrounded;

        /// <summary>
        /// Gets whether the character is performing a deliberate jump rather than an ordinary fall.
        /// </summary>
        public bool IsJumping => m_isJumping;
        public bool IsPerformingAction => m_isPerformingAction;
        public bool CanMove => m_canMove;
        public bool CanRotate => m_canRotate;
        public bool ShouldApplyRootMotion => m_shouldApplyRootMotion;

        protected virtual void Awake()
        {
            m_animator = GetComponent<Animator>();
            if (m_animator == null)
            {
                m_animator = GetComponentInChildren<Animator>(true);
            }

            m_characterAnimatorManager = GetComponentInChildren<CharacterAnimatorManager>(true);
            m_characterNetworkManager = GetComponent<CharacterNetworkManager>();
            m_characterStatsManager = GetComponent<CharacterStatsManager>();
            m_characterAnimatorManager?.Initialize(m_animator);
        }

        /// <summary>
        /// Applies the movement restrictions and root-motion policy for the current character action.
        /// </summary>
        public void SetActionState(
            bool isPerformingAction,
            bool shouldApplyRootMotion,
            bool canRotate,
            bool canMove)
        {
            m_isPerformingAction = isPerformingAction;
            m_shouldApplyRootMotion = shouldApplyRootMotion;
            m_canRotate = canRotate;
            m_canMove = canMove;
        }

        /// <summary>
        /// Restores the default action state after an action animation returns to Empty.
        /// </summary>
        public void ResetActionFlags()
        {
            m_isPerformingAction = false;
            m_canMove = true;
            m_canRotate = true;
            m_shouldApplyRootMotion = false;
            EndJump();
        }

        /// <summary>
        /// Updates the shared grounded state from the character's ground probe.
        /// </summary>
        public void SetGroundedState(bool isGrounded)
        {
            m_isGrounded = isGrounded;
        }

        /// <summary>
        /// Marks the character as performing a deliberate jump.
        /// </summary>
        public void BeginJump()
        {
            m_isJumping = true;
        }

        /// <summary>
        /// Clears the deliberate jump state after landing or an animation fail-safe.
        /// </summary>
        public void EndJump()
        {
            m_isJumping = false;
        }
    }
}
