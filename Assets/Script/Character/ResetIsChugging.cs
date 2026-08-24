using UnityEngine;

namespace ZZ
{
    /// <summary>Consumes one continuation input whenever a new flask sip begins.</summary>
    public class ResetIsChugging : StateMachineBehaviour
    {
        public override void OnStateEnter(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            PlayerManager player = animator.GetComponentInParent<PlayerManager>();
            player?.PlayerCombatManager?.HandleFlaskDrinkStateEntered();
        }
    }
}
