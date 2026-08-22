using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Resolves the locally owned player's equipped weapon action and executes combat logic.
    /// </summary>
    [RequireComponent(typeof(PlayerManager))]
    public class PlayerCombatManager : CharacterCombatManager
    {
        [SerializeField, Min(0f)] private float m_fullyChargedDuration = 0.8f;

        private PlayerManager m_player;
        private WeaponItem m_chargingWeapon;
        private float m_chargeStartTime;

        /// <summary>Gets the weapon currently selected by the player's action hand.</summary>
        public WeaponItem CurrentWeaponBeingUsed => ResolveCurrentWeapon();

        /// <summary>Gets whether the locally owned player is holding a heavy attack.</summary>
        public bool IsChargingAttack => m_chargingWeapon != null;

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
        /// Begins an owner-controlled heavy attack charge using the equipped right-hand weapon.
        /// </summary>
        public void BeginChargingHeavyAttack()
        {
            WeaponItem weapon = m_player?.InventoryManager?.CurrentRightHandWeapon;
            if (IsChargingAttack ||
                weapon?.RightHandHeavyAction == null ||
                !CanBeginChargingAttack())
            {
                return;
            }

            m_chargingWeapon = weapon;
            m_chargeStartTime = Time.time;
            m_player.PlayerNetworkManager?.SetCharacterActionHand(true);
            m_player.CharacterNetworkManager?.SetChargingAttackState(true);
        }

        /// <summary>
        /// Releases a short hold as a heavy attack and a completed hold as a charged attack.
        /// </summary>
        public void ReleaseChargingHeavyAttack()
        {
            if (!IsChargingAttack)
            {
                return;
            }

            WeaponItem weapon = m_chargingWeapon;
            float chargeDuration = Mathf.Max(0f, Time.time - m_chargeStartTime);
            WeaponItemBasedAction weaponAction =
                ShouldUseChargedAttack(chargeDuration, m_fullyChargedDuration) &&
                weapon.RightHandChargedAction != null
                    ? weapon.RightHandChargedAction
                    : weapon.RightHandHeavyAction;
            ClearChargingState();
            m_player.ResetActionFlags();
            PerformWeaponBasedAction(weaponAction, weapon);
        }

        /// <summary>Aborts an active charge without releasing an attack.</summary>
        public void CancelChargingAttack()
        {
            if (!IsChargingAttack)
            {
                return;
            }

            ClearChargingState();
            m_player?.ResetActionFlags();
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

        private bool CanBeginChargingAttack()
        {
            return m_player != null &&
                m_player.IsOwner &&
                !m_player.IsPerformingAction &&
                m_player.IsGrounded &&
                m_player.CharacterNetworkManager != null &&
                m_player.CharacterNetworkManager.CurrentStamina.Value > 0f;
        }

        private void ClearChargingState()
        {
            m_chargingWeapon = null;
            m_chargeStartTime = 0f;
            m_player?.CharacterNetworkManager?.SetChargingAttackState(false);
        }

        private static bool ShouldUseChargedAttack(
            float chargeDuration,
            float fullyChargedDuration)
        {
            return chargeDuration >= Mathf.Max(0f, fullyChargedDuration);
        }
    }
}
