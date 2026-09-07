using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Resolves a heavy attack intent into a validated, replicated attack animation.
    /// </summary>
    [GameAsset(FileName = "Heavy Attack", MenuName = "ZZ/Actions/Heavy Attack")]
    public class HeavyAttackWeaponItemAction : WeaponItemBasedAction
    {
        [SerializeField] private AttackType m_attackType = AttackType.HeavyAttack01;

        /// <inheritdoc />
        public override void AttemptToPerformAction(PlayerManager player, WeaponItem weapon)
        {
            JumpAttackContext jumpContext = ResolveJumpAttackContext(
                player?.IsGrounded == true,
                player?.IsJumping == true);
            if (jumpContext == JumpAttackContext.Airborne)
            {
                TryPerformJumpingAttack(
                    player,
                    weapon,
                    AttackType.HeavyJumpingAttack01);
                return;
            }

            if (jumpContext == JumpAttackContext.Takeoff)
            {
                return;
            }

            if (player?.IsPerformingAction == true)
            {
                player.PlayerCombatManager?.TryPerformMainHandCombo(m_attackType);
                return;
            }

            if (!CanPerformAttack(player))
            {
                return;
            }

            PerformAttack(player, weapon, m_attackType);
        }
    }
}
