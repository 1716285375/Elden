using UnityEngine;

namespace ZZ
{
    /// <summary>Resolves a held ranged-attack input into one notched ammunition slot.</summary>
    [CreateAssetMenu(
        fileName = "Fire Projectile",
        menuName = "ZZ/Actions/Fire Projectile")]
    public class FireProjectileWeaponItemAction : WeaponItemAction
    {
        [SerializeField] private ProjectileSlot m_projectileSlot =
            ProjectileSlot.Main;

        public ProjectileSlot ProjectileSlot => m_projectileSlot;

        /// <inheritdoc />
        public override void AttemptToPerformAction(
            PlayerManager player,
            WeaponItem weapon)
        {
            player?.PlayerCombatManager?.BeginNotchingProjectile(
                weapon as RangedWeaponItem,
                m_projectileSlot);
        }
    }
}
