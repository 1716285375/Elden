using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Resolves the locally owned player's equipped weapon action and executes combat logic.
    /// </summary>
    [RequireComponent(typeof(PlayerManager))]
    public class PlayerCombatManager : CharacterCombatManager
    {
        private PlayerManager m_player;

        /// <summary>Gets the weapon currently selected by the player's action hand.</summary>
        public WeaponItem CurrentWeaponBeingUsed => ResolveCurrentWeapon();

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponent<PlayerManager>();
        }

        /// <summary>
        /// Executes the supplied weapon action against the supplied weapon.
        /// </summary>
        public void PerformWeaponBasedAction(WeaponItemBasedAction weaponAction, WeaponItem weapon)
        {
            if (weaponAction == null || weapon == null)
            {
                return;
            }

            weaponAction.AttemptToPerformAction(m_player, weapon);
        }

        /// <summary>
        /// Consumes the stamina cost of the current attack on the locally owned player.
        /// Called from an attack animation event.
        /// </summary>
        public void DrainStaminaBasedOnAttack()
        {
            if (m_player == null || !m_player.IsOwner)
            {
                return;
            }

            WeaponItem weapon = CurrentWeaponBeingUsed;
            if (weapon == null)
            {
                return;
            }

            float staminaCost = weapon.BaseStaminaCost *
                weapon.GetStaminaCostMultiplier(CurrentAttackType);
            m_player.PlayerStatsManager?.TryConsumeStamina(staminaCost);
        }

        private WeaponItem ResolveCurrentWeapon()
        {
            if (m_player == null || m_player.InventoryManager == null)
            {
                return null;
            }

            bool isUsingRightHand = m_player.PlayerNetworkManager == null ||
                m_player.PlayerNetworkManager.IsUsingRightHand.Value;
            return isUsingRightHand
                ? m_player.InventoryManager.CurrentRightHandWeapon
                : m_player.InventoryManager.CurrentLeftHandWeapon;
        }
    }
}
