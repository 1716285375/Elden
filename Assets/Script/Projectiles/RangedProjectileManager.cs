using UnityEngine;

namespace ZZ
{
    /// <summary>Simulates one locally spawned arrow and embeds it after its first impact.</summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class RangedProjectileManager : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float m_embeddedLifetime = 20f;
        [SerializeField] private Vector2 m_penetrationDepthRange =
            new Vector2(0.1f, 0.3f);

        private Rigidbody m_rigidbody;
        private CapsuleCollider m_physicalCollider;
        private RangeProjectileDamageCollider m_damageCollider;
        private bool m_hasPenetratedSurface;

        /// <summary>Gets whether this projectile has consumed its first surface impact.</summary>
        public bool HasPenetratedSurface => m_hasPenetratedSurface;

        private void Awake()
        {
            m_rigidbody = GetComponent<Rigidbody>();
            m_physicalCollider = GetComponent<CapsuleCollider>();
            m_damageCollider = GetComponentInChildren<RangeProjectileDamageCollider>(
                true);
            m_damageCollider?.SetProjectileManager(this);
        }

        private void FixedUpdate()
        {
            if (m_hasPenetratedSurface ||
                m_rigidbody == null ||
                m_rigidbody.linearVelocity.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(
                m_rigidbody.linearVelocity.normalized,
                Vector3.up);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            Vector3 contactPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.collider.ClosestPointOnBounds(transform.position);
            CreatePenetrationIntoObject(collision.collider, contactPoint);
        }

        /// <summary>Configures flight, damage, shooter collision ignores, and initial force.</summary>
        public void Initialize(
            CharacterManager shooter,
            RangedProjectileItem projectile,
            Vector3 releaseDirection,
            bool canApplyDamage)
        {
            if (projectile == null)
            {
                return;
            }

            m_rigidbody ??= GetComponent<Rigidbody>();
            m_physicalCollider ??= GetComponent<CapsuleCollider>();
            m_damageCollider ??=
                GetComponentInChildren<RangeProjectileDamageCollider>(true);
            m_rigidbody.mass = projectile.AmmoMass;
            m_rigidbody.useGravity = true;
            m_rigidbody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;
            m_rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            Vector3 direction = releaseDirection.sqrMagnitude > Mathf.Epsilon
                ? releaseDirection.normalized
                : transform.forward;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            ConfigureDamage(shooter, projectile, canApplyDamage);
            IgnoreShooterColliders(shooter);
            m_rigidbody.AddForce(
                (direction * projectile.ForwardVelocity) +
                    (Vector3.up * projectile.UpwardVelocity),
                ForceMode.VelocityChange);
        }

        /// <summary>Stops simulation and attaches this arrow beneath a scale-compensated pivot.</summary>
        public void CreatePenetrationIntoObject(
            Collider hitCollider,
            Vector3 contactPoint)
        {
            if (m_hasPenetratedSurface || hitCollider == null)
            {
                return;
            }

            m_hasPenetratedSurface = true;
            float minimumDepth = Mathf.Min(
                m_penetrationDepthRange.x,
                m_penetrationDepthRange.y);
            float maximumDepth = Mathf.Max(
                m_penetrationDepthRange.x,
                m_penetrationDepthRange.y);
            transform.position = contactPoint +
                transform.forward * Random.Range(minimumDepth, maximumDepth);
            if (m_rigidbody != null)
            {
                m_rigidbody.linearVelocity = Vector3.zero;
                m_rigidbody.angularVelocity = Vector3.zero;
                m_rigidbody.isKinematic = true;
            }

            if (m_physicalCollider != null)
            {
                m_physicalCollider.enabled = false;
            }

            m_damageCollider?.CloseDamageCollider();
            AttachWithoutInheritedScale(hitCollider.transform);
        }

        private void ConfigureDamage(
            CharacterManager shooter,
            RangedProjectileItem projectile,
            bool canApplyDamage)
        {
            if (m_damageCollider == null)
            {
                return;
            }

            m_damageCollider.SetProjectileManager(this);
            m_damageCollider.SetDamageSource(shooter);
            m_damageCollider.SetDamageValues(
                projectile.PhysicalDamage,
                projectile.MagicDamage,
                projectile.FireDamage,
                projectile.LightningDamage,
                projectile.HolyDamage,
                projectile.PoiseDamage);
            if (canApplyDamage)
            {
                m_damageCollider.OpenDamageCollider();
            }
            else
            {
                m_damageCollider.CloseDamageCollider();
            }
        }

        private void IgnoreShooterColliders(CharacterManager shooter)
        {
            if (shooter == null)
            {
                return;
            }

            Collider[] projectileColliders = GetComponentsInChildren<Collider>(true);
            Collider[] shooterColliders = shooter.GetComponentsInChildren<Collider>(true);
            foreach (Collider projectileCollider in projectileColliders)
            {
                foreach (Collider shooterCollider in shooterColliders)
                {
                    if (projectileCollider != null && shooterCollider != null)
                    {
                        Physics.IgnoreCollision(
                            projectileCollider,
                            shooterCollider,
                            true);
                    }
                }
            }
        }

        private void AttachWithoutInheritedScale(Transform hitTransform)
        {
            Vector3 worldScale = transform.lossyScale;
            GameObject attachment = new GameObject("Projectile Attachment");
            Transform attachmentTransform = attachment.transform;
            attachmentTransform.SetPositionAndRotation(
                transform.position,
                transform.rotation);
            attachmentTransform.SetParent(hitTransform, true);
            Vector3 attachmentScale = attachmentTransform.lossyScale;
            attachmentTransform.localScale = new Vector3(
                SafeDivide(1f, attachmentScale.x),
                SafeDivide(1f, attachmentScale.y),
                SafeDivide(1f, attachmentScale.z));
            transform.SetParent(attachmentTransform, true);
            Vector3 resolvedScale = transform.lossyScale;
            transform.localScale = new Vector3(
                transform.localScale.x * SafeDivide(worldScale.x, resolvedScale.x),
                transform.localScale.y * SafeDivide(worldScale.y, resolvedScale.y),
                transform.localScale.z * SafeDivide(worldScale.z, resolvedScale.z));
            Destroy(attachment, m_embeddedLifetime);
        }

        private static float SafeDivide(float numerator, float denominator)
        {
            return Mathf.Abs(denominator) > Mathf.Epsilon
                ? numerator / denominator
                : numerator;
        }
    }
}
