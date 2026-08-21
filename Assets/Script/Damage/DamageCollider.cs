using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(Collider))]
    public class DamageCollider : MonoBehaviour
    {
        [Header("Damage Source")]
        [SerializeField] private CharacterManager m_characterCausingDamage;

        [Header("Damage")]
        [SerializeField, Min(0f)] private float m_physicalDamage;
        [SerializeField, Min(0f)] private float m_magicDamage;
        [SerializeField, Min(0f)] private float m_fireDamage;
        [SerializeField, Min(0f)] private float m_lightningDamage;
        [SerializeField, Min(0f)] private float m_holyDamage;
        [SerializeField, Min(0f)] private float m_poiseDamage;

        private readonly List<CharacterManager> m_charactersDamaged = new();
        private Collider m_damageCollider;

        /// <summary>
        /// Gets the targets already hit during the current damage window.
        /// </summary>
        public IReadOnlyList<CharacterManager> CharactersDamaged => m_charactersDamaged;

        protected virtual void Awake()
        {
            m_damageCollider = GetComponent<Collider>();
            m_damageCollider.isTrigger = true;
        }

        protected virtual void OnEnable()
        {
            ResetCharactersDamaged();
        }

        protected virtual void OnDisable()
        {
            ResetCharactersDamaged();
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            CharacterManager target = other.GetComponentInParent<CharacterManager>();
            if (target == null ||
                target == m_characterCausingDamage ||
                m_charactersDamaged.Contains(target))
            {
                return;
            }

            m_charactersDamaged.Add(target);
            Vector3 contactPoint = other.ClosestPointOnBounds(transform.position);
            Damage(target, contactPoint);
        }

        /// <summary>
        /// Starts a hit window and permits each character to be damaged once.
        /// </summary>
        public void OpenDamageCollider()
        {
            ResetCharactersDamaged();
            GetDamageCollider().enabled = true;
        }

        /// <summary>
        /// Ends the current hit window.
        /// </summary>
        public void CloseDamageCollider()
        {
            GetDamageCollider().enabled = false;
        }

        /// <summary>
        /// Sets the character responsible for this collider's damage.
        /// </summary>
        public void SetDamageSource(CharacterManager characterCausingDamage)
        {
            m_characterCausingDamage = characterCausingDamage;
        }

        protected virtual void Damage(CharacterManager target, Vector3 contactPoint)
        {
            CharacterEffectsManager effectsManager = target.CharacterEffectsManager;
            TakeDamageEffect damageTemplate =
                WorldCharacterEffectsManager.Instance?.TakeDamageEffect;
            if (effectsManager == null || damageTemplate == null)
            {
                Debug.LogWarning(
                    "Damage requires a target effects manager and the world damage template.",
                    this);
                return;
            }

            TakeDamageEffect runtimeEffect = damageTemplate.CreateRuntimeDamageEffect(
                m_characterCausingDamage,
                m_physicalDamage,
                m_magicDamage,
                m_fireDamage,
                m_lightningDamage,
                m_holyDamage,
                contactPoint,
                m_poiseDamage);
            effectsManager.ProcessRuntimeInstantEffect(runtimeEffect);
        }

        private Collider GetDamageCollider()
        {
            m_damageCollider ??= GetComponent<Collider>();
            return m_damageCollider;
        }

        private void ResetCharactersDamaged()
        {
            m_charactersDamaged.Clear();
        }
    }
}
