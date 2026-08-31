using UnityEngine;

namespace ZZ
{
    /// <summary>Owns the ranger's bow presentation, projectile data, and draw hand.</summary>
    [DisallowMultipleComponent]
    public sealed class AIRangerEquipmentManager : CharacterEquipmentManager
    {
        [SerializeField] private RangedWeaponItem m_bow;
        [SerializeField] private GameObject m_bowObject;
        [SerializeField] private Animator m_bowAnimator;
        [SerializeField] private RangedProjectileItem m_projectile;
        [SerializeField] private Transform m_drawHand;

        public RangedWeaponItem Bow => m_bow;
        public GameObject BowObject => m_bowObject;
        public Animator BowAnimator => m_bowAnimator;
        public RangedProjectileItem Projectile => m_projectile;
        public Transform DrawHand => m_drawHand;

        protected override void Awake()
        {
            base.Awake();
            m_bowAnimator ??= m_bowObject?.GetComponentInChildren<Animator>(true);
            m_bowObject?.SetActive(true);
            m_bowObject?.GetComponentInChildren<WeaponManager>(true)
                ?.Initialize(Character, m_bow);
        }

        /// <summary>Updates the independent bow model state on every peer.</summary>
        public void SetRangedWeaponState(
            bool hasArrowNotched,
            bool isHoldingArrow)
        {
            WeaponManager bowManager =
                m_bowObject?.GetComponentInChildren<WeaponManager>(true);
            bowManager?.SetRangedWeaponState(
                hasArrowNotched,
                isHoldingArrow);
        }
    }
}
