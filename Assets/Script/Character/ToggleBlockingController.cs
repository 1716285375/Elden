using UnityEngine;

namespace ZZ
{
    /// <summary>Uses the off-hand animation set while a blocking state is active.</summary>
    public class ToggleBlockingController : StateMachineBehaviour
    {
        private static readonly int s_isBlockingParameter =
            Animator.StringToHash("isBlocking");

        public override void OnStateEnter(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            animator.GetComponentInParent<PlayerManager>()
                ?.PlayerCombatManager
                ?.ApplyBlockingAnimatorController(true);
        }

        public override void OnStateExit(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            if (!animator.GetBool(s_isBlockingParameter))
            {
                animator.GetComponentInParent<PlayerManager>()
                    ?.PlayerCombatManager
                    ?.ApplyBlockingAnimatorController(false);
            }
        }
    }
}
