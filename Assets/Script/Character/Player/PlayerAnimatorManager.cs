using UnityEngine;

namespace ZZ
{
    public class PlayerAnimatorManager : CharacterAnimatorManager
    {
        private PlayerManager m_player;
        private CharacterController m_characterController;

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponentInParent<PlayerManager>();
            m_characterController = GetComponentInParent<CharacterController>();
        }

        /// <summary>
        /// Forwards the Jump Start animation event to the locally owned locomotion authority.
        /// </summary>
        public void ApplyJumpingVelocity()
        {
            m_player?.LocomotionManager?.ApplyJumpingVelocity();
        }

        private void OnAnimatorMove()
        {
            if (m_player == null ||
                !m_player.IsOwner ||
                !m_player.ShouldApplyRootMotion ||
                CharacterAnimator == null ||
                m_characterController == null ||
                !m_characterController.enabled)
            {
                return;
            }

            m_characterController.Move(CharacterAnimator.deltaPosition);
            m_player.transform.rotation *= CharacterAnimator.deltaRotation;
        }
    }
}
