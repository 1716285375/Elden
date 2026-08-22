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
        private bool m_canComboWithMainHandWeapon;

        /// <summary>Gets the weapon currently selected by the player's action hand.</summary>
        public WeaponItem CurrentWeaponBeingUsed => ResolveCurrentWeapon();

        /// <summary>Gets whether the locally owned player is holding a heavy attack.</summary>
        public bool IsChargingAttack => m_chargingWeapon != null;

        /// <summary>Gets whether the current main-hand attack accepts its next combo input.</summary>
        public bool CanComboWithMainHandWeapon => m_canComboWithMainHandWeapon;

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
            if (m_player?.IsPerformingAction == true)
            {
                weapon?.RightHandHeavyAction?.AttemptToPerformAction(m_player, weapon);
                return;
            }

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
        /// Opens the authored main-hand combo window when the current attack has a follow-up.
        /// Called by an attack animation event.
        /// </summary>
        public void EnableCanCombo()
        {
            m_canComboWithMainHandWeapon =
                m_player != null &&
                m_player.IsOwner &&
                m_player.IsPerformingAction &&
                m_player.PlayerNetworkManager != null &&
                m_player.PlayerNetworkManager.IsUsingRightHand.Value &&
                HasNextMainHandComboAttack(CurrentAttackType);
        }

        /// <summary>Closes the current main-hand combo window.</summary>
        public void DisableCanCombo()
        {
            m_canComboWithMainHandWeapon = false;
        }

        /// <summary>
        /// Consumes a valid combo window and immediately replicates the next authored attack.
        /// </summary>
        public bool TryPerformMainHandCombo(AttackType requestedOpeningAttack)
        {
            if (!m_canComboWithMainHandWeapon ||
                m_player == null ||
                !m_player.IsOwner ||
                !m_player.IsPerformingAction ||
                m_player.CharacterNetworkManager == null ||
                m_player.CharacterNetworkManager.CurrentStamina.Value <= 0f ||
                !TryGetNextMainHandComboAttack(
                    CurrentAttackType,
                    requestedOpeningAttack,
                    out AttackType nextAttack))
            {
                return false;
            }

            DisableCanCombo();
            m_player.PlayerNetworkManager?.SetCharacterActionHand(true);
            ReplicateAttack(nextAttack);
            m_player.CharacterNetworkManager.NotifyServerOfAttackActionServerRpc(
                nextAttack);
            return true;
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

        private static bool HasNextMainHandComboAttack(AttackType currentAttack)
        {
            return currentAttack == AttackType.LightAttack01 ||
                currentAttack == AttackType.LightAttack02 ||
                currentAttack == AttackType.HeavyAttack01 ||
                currentAttack == AttackType.ChargedAttack01;
        }

        private static bool TryGetNextMainHandComboAttack(
            AttackType currentAttack,
            AttackType requestedOpeningAttack,
            out AttackType nextAttack)
        {
            nextAttack = default;
            if (requestedOpeningAttack == AttackType.LightAttack01)
            {
                if (currentAttack == AttackType.LightAttack01)
                {
                    nextAttack = AttackType.LightAttack02;
                    return true;
                }

                if (currentAttack == AttackType.LightAttack02)
                {
                    nextAttack = AttackType.LightAttack03;
                    return true;
                }

                return false;
            }

            if (requestedOpeningAttack == AttackType.HeavyAttack01 &&
                (currentAttack == AttackType.HeavyAttack01 ||
                    currentAttack == AttackType.ChargedAttack01))
            {
                nextAttack = AttackType.HeavyAttack02;
                return true;
            }

            return false;
        }
    }
}
