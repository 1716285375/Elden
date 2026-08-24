using UnityEngine;

namespace ZZ
{
    /// <summary>Allows aimed walking and rotation while preventing sprint during bow hold.</summary>
    public class ToggleNotchedArrowMovement : StateMachineBehaviour
    {
        public override void OnStateEnter(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            CharacterManager character =
                animator.GetComponentInParent<CharacterManager>();
            character?.SetCanMove(true);
            character?.SetCanRotate(true);
            if (character is PlayerManager player)
            {
                player.LocomotionManager?.SetCanRun(false);
            }
        }

        public override void OnStateExit(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            PlayerManager player = animator.GetComponentInParent<PlayerManager>();
            player?.LocomotionManager?.SetCanRun(true);
        }
    }
}
