using UnityEngine;

namespace ZZ
{
    /// <summary>Applies arrow damage once and evaluates blocking from projectile travel.</summary>
    public class RangeProjectileDamageCollider : DamageCollider
    {
        private const float k_MinimumProjectileBlockAngle = 145f;

        private RangedProjectileManager m_projectileManager;

        /// <summary>Connects the damage child to its physical projectile owner.</summary>
        public void SetProjectileManager(RangedProjectileManager projectileManager)
        {
            m_projectileManager = projectileManager;
        }

        /// <inheritdoc />
        protected override void OnTriggerEnter(Collider other)
        {
            int previousTargetCount = CharactersDamaged.Count;
            base.OnTriggerEnter(other);
            if (CharactersDamaged.Count > previousTargetCount)
            {
                m_projectileManager?.CreatePenetrationIntoObject(
                    other,
                    other.ClosestPointOnBounds(transform.position));
            }
        }

        /// <inheritdoc />
        protected override bool CheckForParry(CharacterManager damageTarget)
        {
            return false;
        }

        /// <inheritdoc />
        protected override float GetBlockingDotValues(CharacterManager damageTarget)
        {
            if (damageTarget == null ||
                !IsWithinBlockingAngle(
                    damageTarget.transform.forward,
                    transform.forward))
            {
                return -1f;
            }

            return CalculateBlockingDot(
                damageTarget.transform.forward,
                -transform.forward);
        }

        /// <summary>
        /// Returns whether an incoming projectile points against a defender's forward vector.
        /// </summary>
        public static bool IsWithinBlockingAngle(
            Vector3 blockingForward,
            Vector3 projectileForward)
        {
            Vector3 horizontalBlockingForward = Vector3.ProjectOnPlane(
                blockingForward,
                Vector3.up);
            Vector3 horizontalProjectileForward = Vector3.ProjectOnPlane(
                projectileForward,
                Vector3.up);
            if (horizontalBlockingForward.sqrMagnitude <= Mathf.Epsilon ||
                horizontalProjectileForward.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            return Vector3.Angle(
                horizontalBlockingForward,
                horizontalProjectileForward) > k_MinimumProjectileBlockAngle;
        }
    }
}
