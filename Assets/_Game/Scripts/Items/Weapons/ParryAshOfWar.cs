using UnityEngine;

namespace ZZ
{
    /// <summary>Performs a shield Parry using one of the authored timing profiles.</summary>
    [GameAsset(
        FileName = "Parry Ash Of War",
        MenuName = "ZZ/Ashes Of War/Parry")]
    public class ParryAshOfWar : AshOfWar
    {
        [SerializeField] private ParryAnimationSpeed m_parrySpeed =
            ParryAnimationSpeed.Medium;

        /// <summary>Gets the timing profile selected by this Ash.</summary>
        public ParryAnimationSpeed ParrySpeed => m_parrySpeed;

        /// <inheritdoc />
        public override void AttemptToPerformAction(PlayerManager player)
        {
            WeaponItem weapon = player?.PlayerCombatManager
                ?.SelectWeaponToPerformAshOfWar();
            if (!CanIUseThisAbility(player) ||
                !CanUseWithWeapon(weapon))
            {
                return;
            }

            if (player.PlayerAnimatorManager?.UpdateAnimatorController(weapon) !=
                true)
            {
                return;
            }

            if (!DeductStaminaCost(player) ||
                !DeductFocusPointCost(player))
            {
                return;
            }

            player.PlayerCombatManager.SetBlocking(false);
            player.PlayerNetworkManager?.SetCharacterActionHand(false);

            CharacterActionAnimation parryAnimation = GetParryAnimation();
            player.PlayerAnimatorManager.PlayTargetActionAnimation(
                parryAnimation,
                true,
                false,
                false,
                false);
            if (player.IsSpawned)
            {
                player.CharacterNetworkManager
                    ?.NotifyServerOfActionAnimationServerRpc(
                        parryAnimation,
                        true,
                        false,
                        false,
                        false);
            }
        }

        private CharacterActionAnimation GetParryAnimation()
        {
            switch (m_parrySpeed)
            {
                case ParryAnimationSpeed.Fast:
                    return CharacterActionAnimation.ParryFast;
                case ParryAnimationSpeed.Slow:
                    return CharacterActionAnimation.ParrySlow;
                default:
                    return CharacterActionAnimation.ParryMedium;
            }
        }
    }
}
