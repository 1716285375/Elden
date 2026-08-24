using UnityEngine;

namespace ZZ
{
    /// <summary>Restores item-use permissions when the upper-body layer returns to Empty.</summary>
    public class ResetUpperBodyAction : StateMachineBehaviour
    {
        public override void OnStateEnter(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            PlayerManager player = animator.GetComponentInParent<PlayerManager>();
            player?.PlayerCombatManager?.ResetQuickSlotItemUse();
        }
    }
}
