using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Connects an instantiated weapon model to its owner, item data, and damage collider.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        [SerializeField] private MeleeWeaponDamageCollider m_meleeDamageCollider;

        /// <summary>Gets the per-character runtime weapon item driving this model.</summary>
        public WeaponItem Weapon { get; private set; }

        private void Awake()
        {
            m_meleeDamageCollider ??=
                GetComponentInChildren<MeleeWeaponDamageCollider>(true);
            m_meleeDamageCollider?.CloseDamageCollider();
        }

        /// <summary>
        /// Connects the equipped weapon data and owning character to its damage collider.
        /// </summary>
        public void Initialize(CharacterManager weaponOwner, WeaponItem weapon)
        {
            Weapon = weapon;
            m_meleeDamageCollider ??=
                GetComponentInChildren<MeleeWeaponDamageCollider>(true);
            if (m_meleeDamageCollider == null || weapon == null)
            {
                return;
            }

            m_meleeDamageCollider.SetDamageSource(weaponOwner);
            m_meleeDamageCollider.SetDamageValues(
                weapon.PhysicalDamage,
                weapon.MagicDamage,
                weapon.FireDamage,
                weapon.LightningDamage,
                weapon.HolyDamage,
                weapon.BasePoiseDamage);
            m_meleeDamageCollider.CloseDamageCollider();
        }

        /// <summary>
        /// Enables this weapon's one-hit-per-target damage window.
        /// </summary>
        public void OpenDamageCollider()
        {
            m_meleeDamageCollider?.OpenDamageCollider();
        }

        /// <summary>
        /// Disables this weapon's damage window.
        /// </summary>
        public void CloseDamageCollider()
        {
            m_meleeDamageCollider?.CloseDamageCollider();
        }
    }
}
