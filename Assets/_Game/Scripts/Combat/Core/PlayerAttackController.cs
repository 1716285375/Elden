namespace ZZ
{
    /// <summary>Commits an owned melee attack once, then broadcasts presentation using the legacy protocol.</summary>
    public sealed class PlayerAttackController
    {
        private readonly PlayerManager m_player;

        public PlayerAttackController(PlayerManager player)
        {
            m_player = player;
        }

        public bool TryStart(AttackType attackType, WeaponItem weapon, bool isGuardCounter = false)
        {
            PlayerCombatManager combat = m_player.PlayerCombatManager;
            PlayerNetworkManager network = m_player.PlayerNetworkManager;
            if (!m_player.IsOwner || !m_player.IsSpawned || m_player.IsDead || weapon == null ||
                network == null || m_player.PlayerStatsManager == null || combat.IsUsingItem ||
                m_player.PlayerAnimatorManager == null)
            {
                return false;
            }

            if (network.IsAttacking.Value &&
                !combat.ActionRunner.Windows.IsOpen(CombatWindowType.Combo))
            {
                return false;
            }

            bool isJumpAttack = attackType == AttackType.LightJumpingAttack01 ||
                attackType == AttackType.HeavyJumpingAttack01 || attackType == AttackType.DualJumpAttack;
            if (m_player.IsPerformingAction && !network.IsAttacking.Value && !isGuardCounter &&
                !(isJumpAttack && m_player.IsJumping && !m_player.IsGrounded) &&
                !combat.CanPerformRollingAttack() && !combat.CanPerformBackstepAttack())
            {
                return false;
            }

            if (!weapon.MeetsRequirements(m_player) ||
                !m_player.PlayerAnimatorManager.CanPlayAttack(attackType, weapon) ||
                !CombatActionRunner.TryPay(network.CurrentStamina.Value, weapon.GetAttackStaminaCost(attackType),
                    m_player.PlayerStatsManager.TryConsumeStamina))
            {
                return false;
            }

            combat.CloseAllDamageColliders();
            combat.DisableCanCombo();
            combat.DisableCanPerformCommittedAttack();
            network.SetCharacterActionHand(!network.IsTwoHandingLeftWeapon.Value &&
                (network.IsTwoHandingWeapon.Value || weapon != m_player.InventoryManager.CurrentLeftHandWeapon));
            combat.ReplicateAttack(attackType, weapon);
            network.NotifyServerOfAttackActionServerRpc(attackType);
            return true;
        }
    }
}
