using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Applies projectile damage without Parry and resolves blocking from the projectile origin.
    /// </summary>
    public class SpellProjectileDamageCollider : DamageCollider
    {
        private SpellManager m_spellManager;

        /// <summary>Connects this damage child to its projectile lifetime owner.</summary>
        public void SetSpellManager(SpellManager spellManager)
        {
            m_spellManager = spellManager;
        }

        /// <inheritdoc />
        public override bool SupportsParry => false;

        /// <inheritdoc />
        protected override void OnTriggerEnter(Collider other)
        {
            int previousTargetCount = CharactersDamaged.Count;
            base.OnTriggerEnter(other);
            if (CharactersDamaged.Count > previousTargetCount)
            {
                m_spellManager?.Impact(other.ClosestPointOnBounds(transform.position));
            }
        }

        /// <inheritdoc />
        protected override float ResolveGuardFacingDot(CharacterManager damageTarget)
        {
            if (damageTarget == null)
            {
                return -1f;
            }

            return CalculateBlockingDot(
                damageTarget.transform.forward,
                transform.position - damageTarget.transform.position);
        }
    }
}
