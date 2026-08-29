using UnityEngine;

namespace ZZ
{
    /// <summary>Moves, steers, impacts, and expires one locally simulated spell.</summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class SpellManager : MonoBehaviour
    {
        [Header("Projectile Motion")]
        [SerializeField, Min(0f)] private float m_forwardVelocity = 18f;
        [SerializeField, Min(0f)] private float m_homingTurnSpeed = 180f;
        [SerializeField, Min(0.1f)] private float m_lifeTime = 8f;

        [Header("Impact Presentation")]
        [SerializeField] private GameObject m_impactEffect;
        [SerializeField] private GameObject m_fullChargeImpactEffect;
        [SerializeField] private AudioClip m_impactSound;

        private Rigidbody m_rigidbody;
        private SpellProjectileDamageCollider m_damageCollider;
        private Transform m_target;
        private bool m_hasImpacted;
        private bool m_isFullyCharged;

        protected virtual void Awake()
        {
            ConfigureProjectileLayerCollisions();
            m_rigidbody = GetComponent<Rigidbody>();
            m_damageCollider = GetComponentInChildren<SpellProjectileDamageCollider>(true);
            m_damageCollider?.SetSpellManager(this);
        }

        private void FixedUpdate()
        {
            if (m_hasImpacted || m_rigidbody == null)
            {
                return;
            }

            if (m_target != null)
            {
                Vector3 directionToTarget = m_target.position - transform.position;
                if (directionToTarget.sqrMagnitude > Mathf.Epsilon)
                {
                    Vector3 nextDirection = Vector3.RotateTowards(
                        transform.forward,
                        directionToTarget.normalized,
                        Mathf.Deg2Rad * m_homingTurnSpeed * Time.fixedDeltaTime,
                        0f);
                    transform.rotation = Quaternion.LookRotation(nextDirection);
                }
            }

            m_rigidbody.linearVelocity = transform.forward * m_forwardVelocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            Vector3 contactPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;
            Impact(contactPoint);
        }

        /// <summary>Assigns the caster, target, damage payload, and lifetime.</summary>
        public void Initialize(
            CharacterManager caster,
            Transform target,
            float fireDamage,
            float poiseDamage,
            bool isFullyCharged)
        {
            m_target = target;
            m_isFullyCharged = isFullyCharged;
            m_damageCollider ??=
                GetComponentInChildren<SpellProjectileDamageCollider>(true);
            m_damageCollider?.SetDamageSource(caster);
            m_damageCollider?.SetDamageValues(
                0f,
                0f,
                fireDamage,
                0f,
                0f,
                poiseDamage);
            m_damageCollider?.OpenDamageCollider();
            if (m_rigidbody != null)
            {
                m_rigidbody.linearVelocity = transform.forward * m_forwardVelocity;
            }

            Destroy(gameObject, m_lifeTime);
        }

        /// <summary>Spawns impact feedback and disposes this local projectile once.</summary>
        public void Impact(Vector3 contactPoint)
        {
            if (m_hasImpacted)
            {
                return;
            }

            m_hasImpacted = true;
            m_damageCollider?.CloseDamageCollider();
            WorldSoundFXManager.Instance?.AlertNearbyCharactersToSound(
                contactPoint,
                5f);
            GameObject impactEffect = m_isFullyCharged &&
                m_fullChargeImpactEffect != null
                    ? m_fullChargeImpactEffect
                    : m_impactEffect;
            if (impactEffect != null)
            {
                Instantiate(impactEffect, contactPoint, Quaternion.identity);
            }

            if (m_impactSound != null)
            {
                AudioSource.PlayClipAtPoint(m_impactSound, contactPoint);
            }

            Destroy(gameObject);
        }

        private static void ConfigureProjectileLayerCollisions()
        {
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer < 0)
            {
                return;
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            int damageableCharacterLayer =
                LayerMask.NameToLayer("Damageable Character");
            if (playerLayer >= 0)
            {
                Physics.IgnoreLayerCollision(projectileLayer, playerLayer, true);
            }

            if (damageableCharacterLayer >= 0)
            {
                Physics.IgnoreLayerCollision(
                    projectileLayer,
                    damageableCharacterLayer,
                    true);
            }
        }
    }
}
