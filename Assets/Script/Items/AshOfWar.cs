using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Defines a weapon-equipped special action with shared resource and weapon rules.
    /// </summary>
    public abstract class AshOfWar : Item
    {
        [Header("Ability Costs")]
        [SerializeField, Min(0)] private int m_focusPointsCost;
        [SerializeField, Min(0f)] private float m_staminaCost;

        [Header("Compatible Weapons")]
        [SerializeField] private List<WeaponClass> m_usableWeaponClasses = new();

        /// <summary>Gets the reserved Focus Point cost used by EP80.</summary>
        public int FocusPointsCost => m_focusPointsCost;

        /// <summary>Gets the owner-authoritative Stamina cost.</summary>
        public float StaminaCost => m_staminaCost;

        /// <summary>Gets the weapon classes that may equip this Ash of War.</summary>
        public IReadOnlyList<WeaponClass> UsableWeaponClasses =>
            m_usableWeaponClasses;

        /// <summary>Attempts the shared validation and resource-payment flow.</summary>
        public virtual void AttemptToPerformAction(PlayerManager player)
        {
            if (!CanIUseThisAbility(player))
            {
                return;
            }

            if (!DeductStaminaCost(player))
            {
                return;
            }

            DeductFocusPointCost(player);
        }

        /// <summary>Returns whether the owner may begin an Ash of War action.</summary>
        public virtual bool CanIUseThisAbility(PlayerManager player)
        {
            CharacterNetworkManager networkManager =
                player?.CharacterNetworkManager;
            return player != null &&
                player.IsOwner &&
                !player.IsDead &&
                !player.IsPerformingAction &&
                player.IsGrounded &&
                !player.IsJumping &&
                networkManager != null &&
                networkManager.CurrentStamina.Value > 0f &&
                networkManager.CurrentStamina.Value >= m_staminaCost;
        }

        /// <summary>Returns whether this Ash can be used by the supplied weapon.</summary>
        public bool CanUseWithWeapon(WeaponItem weapon)
        {
            return weapon != null &&
                m_usableWeaponClasses.Contains(weapon.WeaponClass);
        }

        /// <summary>Consumes the authored Stamina cost from the owning player.</summary>
        public bool DeductStaminaCost(PlayerManager player)
        {
            return m_staminaCost <= 0f ||
                player?.PlayerStatsManager?.TryConsumeStamina(m_staminaCost) ==
                    true;
        }

        /// <summary>
        /// Reserves the Focus Point payment boundary until the EP80 FP system exists.
        /// </summary>
        public virtual bool DeductFocusPointCost(PlayerManager player)
        {
            return player != null;
        }
    }
}
