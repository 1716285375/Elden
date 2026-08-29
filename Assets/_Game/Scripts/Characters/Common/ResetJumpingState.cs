using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Clears jump momentum as soon as the landing animation begins.
    /// </summary>
    public class ResetJumpingState : StateMachineBehaviour
    {
        public override void OnStateEnter(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            CharacterManager characterManager = animator.GetComponentInParent<CharacterManager>();
            characterManager?.EndJump();
        }
    }
}
