using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Restores character action permissions when the action layer returns to Empty.
    /// </summary>
    public class ResetActionFlags : StateMachineBehaviour
    {
        public override void OnStateEnter(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            CharacterManager characterManager = animator.GetComponentInParent<CharacterManager>();
            characterManager?.ResetActionFlags();
        }
    }
}
