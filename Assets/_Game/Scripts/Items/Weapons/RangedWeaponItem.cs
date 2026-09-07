using UnityEngine;

namespace ZZ
{
    /// <summary>Defines a ranged weapon's compatible ammunition and draw/release audio.</summary>
    [GameAsset(FileName = "Ranged Weapon", MenuName = "ZZ/Items/Ranged Weapon")]
    public class RangedWeaponItem : WeaponItem
    {
        [Header("Projectile Compatibility")]
        [SerializeField] private ProjectileClass m_projectileClass =
            ProjectileClass.Arrow;

        [Header("Ranged Sound Effects")]
        [SerializeField] private AudioClip[] m_drawSoundEffects =
            System.Array.Empty<AudioClip>();
        [SerializeField] private AudioClip[] m_releaseSoundEffects =
            System.Array.Empty<AudioClip>();

        /// <summary>Gets the ammunition class accepted by this ranged weapon.</summary>
        public ProjectileClass ProjectileClass => m_projectileClass;

        /// <summary>Gets the clips randomized when ammunition is notched.</summary>
        public AudioClip[] DrawSoundEffects => m_drawSoundEffects;

        /// <summary>Gets the clips randomized when the projectile is released.</summary>
        public AudioClip[] ReleaseSoundEffects => m_releaseSoundEffects;

        /// <summary>Returns whether this weapon can fire the supplied ammunition.</summary>
        public bool CanFireProjectile(RangedProjectileItem projectile)
        {
            return projectile != null &&
                projectile.ProjectileClass == m_projectileClass;
        }
    }
}
