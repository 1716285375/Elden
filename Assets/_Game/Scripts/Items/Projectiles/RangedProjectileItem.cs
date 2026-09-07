using UnityEngine;

namespace ZZ
{
    /// <summary>Defines ammunition data separately from its drawn and released prefabs.</summary>
    [GameAsset(FileName = "Ranged Projectile", MenuName = "ZZ/Items/Projectile")]
    public class RangedProjectileItem : Item
    {
        [Header("Projectile Class")]
        [SerializeField] private ProjectileClass m_projectileClass =
            ProjectileClass.Arrow;

        [Header("Flight")]
        [SerializeField, Min(0f)] private float m_forwardVelocity = 30f;
        [SerializeField] private float m_upwardVelocity = 1.5f;
        [SerializeField, Min(0.001f)] private float m_ammoMass = 0.1f;

        [Header("Ammunition")]
        [SerializeField, Min(0)] private int m_maxAmmoAmount = 30;
        [SerializeField, Min(0)] private int m_currentAmmoAmount = 30;

        [Header("Damage")]
        [SerializeField, Min(0f)] private float m_physicalDamage;
        [SerializeField, Min(0f)] private float m_magicDamage;
        [SerializeField, Min(0f)] private float m_fireDamage;
        [SerializeField, Min(0f)] private float m_lightningDamage;
        [SerializeField, Min(0f)] private float m_holyDamage;
        [SerializeField, Min(0f)] private float m_poiseDamage = 5f;

        [Header("Presentation")]
        [SerializeField] private GameObject m_drawProjectileModel;
        [SerializeField] private RangedProjectileManager m_releaseProjectileModel;

        public ProjectileClass ProjectileClass => m_projectileClass;
        public float ForwardVelocity => m_forwardVelocity;
        public float UpwardVelocity => m_upwardVelocity;
        public float AmmoMass => m_ammoMass;
        public int MaxAmmoAmount => m_maxAmmoAmount;
        public int CurrentAmmoAmount => m_currentAmmoAmount;
        public float PhysicalDamage => m_physicalDamage;
        public float MagicDamage => m_magicDamage;
        public float FireDamage => m_fireDamage;
        public float LightningDamage => m_lightningDamage;
        public float HolyDamage => m_holyDamage;
        public float PoiseDamage => m_poiseDamage;
        public GameObject DrawProjectileModel => m_drawProjectileModel;
        public RangedProjectileManager ReleaseProjectileModel =>
            m_releaseProjectileModel;

        /// <summary>Consumes one unit from this per-player runtime ammunition copy.</summary>
        public bool TryConsumeAmmo()
        {
            if (m_currentAmmoAmount <= 0)
            {
                return false;
            }

            m_currentAmmoAmount--;
            return true;
        }

        /// <summary>Restores a persisted amount while respecting this ammunition's capacity.</summary>
        public void SetCurrentAmmoAmount(int currentAmmoAmount)
        {
            m_currentAmmoAmount = Mathf.Clamp(
                currentAmmoAmount,
                0,
                m_maxAmmoAmount);
        }

        private void OnValidate()
        {
            m_currentAmmoAmount = Mathf.Clamp(
                m_currentAmmoAmount,
                0,
                m_maxAmmoAmount);
        }
    }
}
