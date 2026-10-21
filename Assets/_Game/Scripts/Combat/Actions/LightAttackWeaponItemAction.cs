using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Resolves a light attack intent into a validated, replicated attack animation.
    /// </summary>
    [GameAsset(FileName = "Light Attack", MenuName = "ZZ/Actions/Light Attack")]
    public class LightAttackWeaponItemAction : WeaponItemBasedAction
    {
        [SerializeField] private AttackType m_attackType = AttackType.LightAttack01;

        /// <inheritdoc />
        public override void AttemptToPerformAction(PlayerManager player, WeaponItem weapon)
        {
            // Root motion can briefly lose ground contact during a committed swing.
            // Its authored combo remains authoritative until that action ends.
            if (player?.CharacterNetworkManager?.IsAttacking.Value == true)
            {
                player.PlayerCombatManager?.TryPerformMainHandCombo(m_attackType);
                return;
            }

            JumpAttackContext jumpContext = ResolveJumpAttackContext(
                player?.IsGrounded == true,
                player?.IsJumping == true);
            if (jumpContext == JumpAttackContext.Airborne)
            {
                TryPerformJumpingAttack(
                    player,
                    weapon,
                    AttackType.LightJumpingAttack01);
                return;
            }

            if (jumpContext == JumpAttackContext.Takeoff)
            {
                return;
            }

            if (player?.IsPerformingAction == true)
            {
                if (player.PlayerCombatManager?.TryPerformCommittedAttack(weapon) == true)
                {
                    return;
                }

                player.PlayerCombatManager?.TryPerformMainHandCombo(m_attackType);
                return;
            }

            if (player?.PlayerCombatManager?.AttemptCriticalAttack() == true)
            {
                return;
            }

            if (player?.PlayerCombatManager?.TryPerformRunningAttack(weapon) == true)
            {
                return;
            }

            if (!CanPerformAttack(player))
            {
                return;
            }

            bool isCrouching = player.CharacterNetworkManager?.IsSneaking.Value == true;
            PerformAttack(player, weapon, isCrouching ? AttackType.RollAttack01 : m_attackType);
        }
    }
}
