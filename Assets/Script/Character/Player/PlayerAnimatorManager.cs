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

        /// <summary>
        /// Forwards the attack hit-frame event to enable the current weapon's damage collider.
        /// </summary>
        public void OpenDamageCollider()
        {
            m_player?.EquipmentManager?.OpenDamageCollider();
        }

        /// <summary>
        /// Forwards the attack end-frame event to disable the current weapon's damage collider.
        /// </summary>
        public void CloseDamageCollider()
        {
            m_player?.EquipmentManager?.CloseDamageCollider();
        }

        /// <summary>
        /// Forwards the attack animation event that drains attack stamina on the owner.
        /// </summary>
        public void DrainStaminaBasedOnAttack()
        {
            m_player?.PlayerCombatManager?.DrainStaminaBasedOnAttack();
        }

        /// <summary>Allows rotation again during an attack's recovery window.</summary>
        public void EnableCanRotate()
        {
            m_player?.SetCanRotate(true);
        }

        /// <summary>Locks rotation during an attack's active frames.</summary>
        public void DisableCanRotate()
        {
            m_player?.SetCanRotate(false);
        }

        // The following receivers satisfy authored attack animation events.
        // They are reserved for the combo, roll-cancel, move-cancel and weapon
        // trail systems planned for later episodes.
        public void EnableCanDoCombo() { }

        public void DisableCanDoCombo() { }

        public void EnableCanRoll() { }

        public void EnableCanMoveCancel() { }

        public void ActivateMainHandWeaponTrail() { }

        public void DeactivateMainHandWeaponTrail() { }

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
