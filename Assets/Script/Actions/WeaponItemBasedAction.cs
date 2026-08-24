using UnityEngine;

namespace ZZ
{
    /// <summary>Describes which attack branch owns the current airborne state.</summary>
    internal enum JumpAttackContext
    {
        Grounded,
        Takeoff,
        Airborne
    }

    /// <summary>
    /// Base ScriptableObject for a data-driven weapon action slot.
    /// A weapon references one action per input slot; the action decides what happens.
    /// </summary>
    public abstract class WeaponItemBasedAction : ScriptableObject
    {
        [SerializeField, HideInInspector] private int m_actionID;

        /// <summary>Gets the stable catalog identifier assigned by the world action manager.</summary>
        public int ActionID => m_actionID;

        /// <summary>
        /// Executes this action for the supplied player and weapon.
        /// </summary>
        public abstract void AttemptToPerformAction(PlayerManager player, WeaponItem weapon);

        protected static bool CanPerformAttack(PlayerManager player)
        {
            if (player == null || !player.IsOwner || player.IsPerformingAction)
            {
                return false;
            }

            if (!player.IsGrounded)
            {
                return false;
            }

            return player.CharacterNetworkManager != null &&
                player.CharacterNetworkManager.CurrentStamina.Value > 0f;
        }

        /// <summary>
        /// Resolves airborne attacks before action/combo checks so the active jump action can
        /// be replaced without leaking a grounded attack into the takeoff frame.
        /// </summary>
        internal static JumpAttackContext ResolveJumpAttackContext(
            bool isGrounded,
            bool isJumping)
        {
            if (!isGrounded)
            {
                return JumpAttackContext.Airborne;
            }

            return isJumping
                ? JumpAttackContext.Takeoff
                : JumpAttackContext.Grounded;
        }

        protected static bool TryPerformJumpingAttack(
            PlayerManager player,
            WeaponItem weapon,
            AttackType attackType)
        {
            if (player == null ||
                weapon == null ||
                !player.IsOwner ||
                player.CharacterNetworkManager == null ||
                player.CharacterNetworkManager.CurrentStamina.Value <= 0f)
            {
                return false;
            }

            PerformAttack(player, weapon, attackType);
            return true;
        }

        protected static void PerformAttack(
            PlayerManager player,
            WeaponItem weapon,
            AttackType attackType)
        {
            player.PlayerCombatManager?.DisableCanCombo();
            player.PlayerNetworkManager?.SetCharacterActionHand(
                player.PlayerNetworkManager.IsTwoHandingLeftWeapon.Value == false);
            player.PlayerCombatManager?.ReplicateAttack(attackType, weapon);
            player.CharacterNetworkManager?.NotifyServerOfAttackActionServerRpc(
                attackType);
        }

        internal void AssignActionID(int actionID)
        {
            m_actionID = actionID;
        }
    }
}
