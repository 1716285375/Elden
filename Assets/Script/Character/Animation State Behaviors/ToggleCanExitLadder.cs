using UnityEngine;

namespace ZZ
{
    /// <summary>Allows ladder exits only while one authored hand idle is active.</summary>
    public sealed class ToggleCanExitLadder : StateMachineBehaviour
    {
        [SerializeField] private LadderHandState m_handState;

        public LadderHandState HandState => m_handState;

        /// <summary>Configures the authored idle hand in editor setup code.</summary>
        public void SetHandState(LadderHandState handState)
        {
            m_handState = handState;
        }

        public override void OnStateEnter(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            SetCanExit(animator, true);
        }

        public override void OnStateExit(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            SetCanExit(animator, false);
        }

        private void SetCanExit(Animator animator, bool canExit)
        {
            animator?.GetComponentInParent<PlayerLocomotionManager>()
                ?.SetCanExitLadder(m_handState, canExit);
        }
    }
}
