using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Connects an instantiated weapon model to its owner, item data, and damage collider.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        public const float UpgradeDamagePerLevel = 11f;

        [SerializeField] private MeleeWeaponDamageCollider m_meleeDamageCollider;
        [SerializeField] private Animator m_weaponAnimator;
        [SerializeField] private ParticleSystem m_particleWeaponTrail;
        [SerializeField] private TrailRenderer m_rendererWeaponTrail;

        private AttackType m_currentAttackType = AttackType.LightAttack01;

        /// <summary>Gets the per-character runtime weapon item driving this model.</summary>
        public WeaponItem Weapon { get; private set; }

        /// <summary>Gets the melee hitbox supplied by the equipped runtime model.</summary>
        public MeleeWeaponDamageCollider MeleeDamageCollider =>
            m_meleeDamageCollider;

        /// <summary>Gets the authored particle trail used by this weapon, when present.</summary>
        public ParticleSystem WeaponTrailParticles => m_particleWeaponTrail;

        /// <summary>Gets whether either supported trail implementation is emitting.</summary>
        public bool IsWeaponTrailEmitting =>
            (m_particleWeaponTrail != null &&
                m_particleWeaponTrail.emission.enabled) ||
            (m_rendererWeaponTrail != null &&
                m_rendererWeaponTrail.emitting);

        /// <summary>Gets the authored spell origin embedded in this weapon model.</summary>
        public SpellInstantiationLocation SpellInstantiationLocation { get; private set; }

        private void Awake()
        {
            m_meleeDamageCollider ??=
                GetComponentInChildren<MeleeWeaponDamageCollider>(true);
            SpellInstantiationLocation ??=
                GetComponentInChildren<SpellInstantiationLocation>(true);
            m_weaponAnimator ??= GetComponentInChildren<Animator>(true);
            DiscoverWeaponTrail();
            m_meleeDamageCollider?.CloseDamageCollider();
            ToggleWeaponTrail(false);
        }

        private void OnDisable()
        {
            ToggleWeaponTrail(false);
        }

        /// <summary>
        /// Connects the equipped weapon data and owning character to its damage collider.
        /// </summary>
        public void Initialize(CharacterManager weaponOwner, WeaponItem weapon)
        {
            Weapon = weapon;
            SpellInstantiationLocation ??=
                GetComponentInChildren<SpellInstantiationLocation>(true);
            m_meleeDamageCollider ??=
                GetComponentInChildren<MeleeWeaponDamageCollider>(true);
            if (m_meleeDamageCollider == null || weapon == null)
            {
                return;
            }

            m_meleeDamageCollider.SetDamageSource(weaponOwner);
            m_meleeDamageCollider.SetDamageValues(
                GetUpgradedDamage(weapon.PhysicalDamage, weapon.UpgradeLevel),
                GetUpgradedDamage(weapon.MagicDamage, weapon.UpgradeLevel),
                GetUpgradedDamage(weapon.FireDamage, weapon.UpgradeLevel),
                GetUpgradedDamage(weapon.LightningDamage, weapon.UpgradeLevel),
                GetUpgradedDamage(weapon.HolyDamage, weapon.UpgradeLevel),
                weapon.BasePoiseDamage);
            m_meleeDamageCollider.SetBuildupValues(
                weapon.PoisonBuildup,
                weapon.BleedBuildup,
                weapon.FrostBuildup);
            m_meleeDamageCollider.CloseDamageCollider();
        }

        /// <summary>Recalculates collider damage after equipment or attribute presentation changes.</summary>
        public void SetWeaponDamage()
        {
            SetAttackType(m_currentAttackType);
        }

        /// <summary>
        /// Enables this weapon's one-hit-per-target damage window.
        /// </summary>
        public void OpenDamageCollider()
        {
            m_meleeDamageCollider?.OpenDamageCollider();
            ToggleWeaponTrail(true);
        }

        /// <summary>
        /// Disables this weapon's damage window.
        /// </summary>
        public void CloseDamageCollider()
        {
            m_meleeDamageCollider?.CloseDamageCollider();
            ToggleWeaponTrail(false);
        }

        /// <summary>
        /// Toggles the configured weapon trail without exposing its rendering implementation.
        /// </summary>
        public void ToggleWeaponTrail(bool status)
        {
            if (m_rendererWeaponTrail != null)
            {
                m_rendererWeaponTrail.emitting = status;
            }

            if (m_particleWeaponTrail == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission =
                m_particleWeaponTrail.emission;
            emission.enabled = status;
            if (status)
            {
                m_particleWeaponTrail.Play(true);
                return;
            }

            m_particleWeaponTrail.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>
        /// Applies the attack type's damage modifier to this weapon's damage collider.
        /// </summary>
        public void SetAttackType(AttackType attackType)
        {
            m_currentAttackType = attackType;
            if (Weapon == null || m_meleeDamageCollider == null)
            {
                return;
            }

            float damageModifier = Weapon.GetAttackDamageModifier(attackType);
            m_meleeDamageCollider.SetDamageValues(
                GetUpgradedDamage(Weapon.PhysicalDamage, Weapon.UpgradeLevel) *
                    damageModifier,
                GetUpgradedDamage(Weapon.MagicDamage, Weapon.UpgradeLevel) *
                    damageModifier,
                GetUpgradedDamage(Weapon.FireDamage, Weapon.UpgradeLevel) *
                    damageModifier,
                GetUpgradedDamage(Weapon.LightningDamage, Weapon.UpgradeLevel) *
                    damageModifier,
                GetUpgradedDamage(Weapon.HolyDamage, Weapon.UpgradeLevel) *
                    damageModifier,
                Weapon.BasePoiseDamage);
            m_meleeDamageCollider.SetBuildupValues(
                Weapon.PoisonBuildup,
                Weapon.BleedBuildup,
                Weapon.FrostBuildup);
        }

        /// <summary>Adds reinforcement only to damage channels authored above zero.</summary>
        public static float GetUpgradedDamage(
            float baseDamage,
            UpgradeLevel upgradeLevel)
        {
            if (baseDamage <= 0f)
            {
                return 0f;
            }

            int sanitizedLevel = Mathf.Clamp(
                (int)upgradeLevel,
                (int)UpgradeLevel.Level0,
                (int)UpgradeLevel.Level10);
            return baseDamage + sanitizedLevel * UpgradeDamagePerLevel;
        }

        /// <summary>Updates an independent bow Animator without requiring melee hitboxes.</summary>
        public void SetRangedWeaponState(
            bool hasArrowNotched,
            bool isHoldingArrow)
        {
            if (m_weaponAnimator == null)
            {
                return;
            }

            m_weaponAnimator.SetBool("hasArrowNotched", hasArrowNotched);
            m_weaponAnimator.SetBool("isHoldingArrow", isHoldingArrow);
        }

        private void DiscoverWeaponTrail()
        {
            m_rendererWeaponTrail ??=
                GetComponentInChildren<TrailRenderer>(true);
            if (m_particleWeaponTrail != null)
            {
                return;
            }

            foreach (ParticleSystem particles in
                     GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles.name.Contains("Weapon Trail"))
                {
                    m_particleWeaponTrail = particles;
                    return;
                }
            }
        }
    }
}
