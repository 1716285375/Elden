using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Reports who a weapon or projectile touched. Every rule about what that contact means lives in
    /// <see cref="CombatResolver"/>; this component only owns its volume and its authored payload.
    /// </summary>
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

        [Header("Status Buildup")]
        [SerializeField, Min(0f)] private float m_poisonBuildup;
        [SerializeField, Min(0f)] private float m_bleedBuildup;
        [SerializeField, Min(0f)] private float m_frostBuildup;

        [Header("Detection")]
        [Tooltip("Restrict swept detection to these layers. Set to the HurtBox layer in production.")]
        [SerializeField] private LayerMask m_hurtBoxLayers = ~0;
        [Tooltip("Upper bound on swept samples per frame. Keeps many simultaneous attackers affordable.")]
        [SerializeField, Range(1, 64)] private int m_maximumTraceSamples = 12;

        private readonly HitRegistry m_registry = new();
        private Collider m_damageCollider;
        private WeaponTrace m_weaponTrace;
        private float m_stanceDamage;
        private int m_hitGroup;

        public void SetStanceDamage(float damage) => m_stanceDamage = Mathf.Max(0f, damage);

        protected virtual bool UsesWeaponTrace => false;

        /// <summary>Whether this detector can be parried at all. Projectiles opt out.</summary>
        public virtual bool SupportsParry => true;

        /// <summary>
        /// Whether settled hits use the committed <see cref="AttackSnapshot"/> instead of live
        /// weapon and stat reads. Weapon colliders opt in; AI colliders keep their authored values
        /// until their attacks are migrated.
        /// </summary>
        public virtual bool UsesActionSnapshot => false;

        /// <summary>Strike group of the currently open hit window. One group hits each target once.</summary>
        public int HitGroup => m_hitGroup;

        /// <summary>Layers swept detection is allowed to query.</summary>
        public LayerMask HurtBoxLayers => m_hurtBoxLayers;

        /// <summary>Upper bound on swept samples per frame.</summary>
        public int MaximumTraceSamples => Mathf.Max(1, m_maximumTraceSamples);

        /// <summary>Targets already settled during the current hit window.</summary>
        public IReadOnlyCollection<CharacterManager> CharactersDamaged => m_registry.Targets;

        /// <summary>Deduplication store used when no action instance is running.</summary>
        public HitRegistry Registry => m_registry;

        protected virtual void Awake()
        {
            m_damageCollider = GetComponent<Collider>();
            m_damageCollider.isTrigger = true;
            if (UsesWeaponTrace && m_damageCollider is BoxCollider box)
            {
                m_weaponTrace = new WeaponTrace(this, box);
            }
        }

        protected virtual void OnEnable()
        {
            ResetCharactersDamaged();
        }

        protected virtual void OnDisable()
        {
            ResetCharactersDamaged();
        }

        private void LateUpdate()
        {
            m_weaponTrace?.Sample();
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            TryProcessHit(other);
        }

        /// <summary>Trigger and swept contacts share validity, guard, parry and deduplication.</summary>
        public HitResult TryProcessHit(Collider other)
        {
            if (other == null || !isActiveAndEnabled || !GetDamageCollider().enabled)
            {
                return new HitResult(HitOutcome.Rejected, m_characterCausingDamage, null);
            }

            return CombatResolver.Resolve(CreateCandidate(other));
        }

        /// <summary>Describes one contact without deciding anything about it.</summary>
        public HitCandidate CreateCandidate(Collider other)
        {
            return new HitCandidate(this, m_characterCausingDamage, other);
        }

        /// <summary>Builds the authored payload, scaled by the struck body part.</summary>
        public DamagePayload CreatePayload(HurtBox hurtBox = null)
        {
            float damageMultiplier = hurtBox != null ? hurtBox.DamageMultiplier : 1f;
            float poiseMultiplier = hurtBox != null ? hurtBox.PoiseMultiplier : 1f;
            return new DamagePayload(m_physicalDamage * damageMultiplier, m_magicDamage * damageMultiplier,
                m_fireDamage * damageMultiplier, m_lightningDamage * damageMultiplier,
                m_holyDamage * damageMultiplier, m_poiseDamage * poiseMultiplier, m_stanceDamage,
                m_poisonBuildup, m_bleedBuildup, m_frostBuildup);
        }

        /// <summary>Routes a settled payload to the damage authority. AI colliders share one registry here.</summary>
        public void DeliverDamage(CharacterManager target, in DamagePayload payload, Vector3 contactPoint,
            bool wasBlocked)
        {
            DispatchDamage(target, payload, contactPoint, wasBlocked);
        }

        protected virtual void DispatchDamage(CharacterManager target, in DamagePayload payload,
            Vector3 contactPoint, bool wasBlocked)
        {
            HitDamageDispatcher.Dispatch(m_characterCausingDamage, target, payload, contactPoint, wasBlocked);
        }

        /// <summary>Dispatches a validated parry to the parrying character.</summary>
        public virtual void NotifyParry(CharacterManager parryingCharacter)
        {
            if (parryingCharacter == null)
            {
                return;
            }

            CharacterNetworkManager targetNetworkManager = parryingCharacter.CharacterNetworkManager;
            if (m_characterCausingDamage != null &&
                m_characterCausingDamage.IsSpawned &&
                parryingCharacter.IsSpawned &&
                targetNetworkManager != null)
            {
                targetNetworkManager.RequestParry(m_characterCausingDamage.NetworkObjectId);
                return;
            }

            m_characterCausingDamage?.CharacterCombatManager
                ?.ProcessParryFromServer(parryingCharacter);
        }

        /// <summary>Starts the default strike group's hit window.</summary>
        public void OpenDamageCollider()
        {
            OpenDamageCollider(0);
        }

        /// <summary>
        /// Starts a hit window belonging to <paramref name="hitGroup"/>. Multi-hit attacks give each
        /// window its own group so the same target can be struck again.
        /// The action's registry is cleared once per action in <c>CombatActionInstance.Begin</c>,
        /// never here: identity is (Sequence, HitGroup, Target), not (HitWindow, Target).
        /// </summary>
        public void OpenDamageCollider(int hitGroup)
        {
            m_hitGroup = hitGroup;
            ResetCharactersDamaged();
            GetDamageCollider().enabled = true;
            m_weaponTrace?.Reset();
            m_weaponTrace?.Sample();
        }

        /// <summary>Ends the current hit window.</summary>
        public void CloseDamageCollider()
        {
            GetDamageCollider().enabled = false;
            m_weaponTrace?.Reset();
        }

        /// <summary>Sets the character responsible for this collider's damage.</summary>
        public void SetDamageSource(CharacterManager characterCausingDamage)
        {
            m_characterCausingDamage = characterCausingDamage;
        }

        /// <summary>Configures the damage payload emitted during this collider's hit window.</summary>
        public void SetDamageValues(
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            float poiseDamage)
        {
            m_physicalDamage = Mathf.Max(0f, physicalDamage);
            m_magicDamage = Mathf.Max(0f, magicDamage);
            m_fireDamage = Mathf.Max(0f, fireDamage);
            m_lightningDamage = Mathf.Max(0f, lightningDamage);
            m_holyDamage = Mathf.Max(0f, holyDamage);
            m_poiseDamage = Mathf.Max(0f, poiseDamage);
            SetStanceDamage(poiseDamage);
            m_poisonBuildup = 0f;
            m_bleedBuildup = 0f;
            m_frostBuildup = 0f;
        }

        /// <summary>Configures status accumulation emitted by the current damage source.</summary>
        public void SetBuildupValues(
            float poisonBuildup,
            float bleedBuildup,
            float frostBuildup)
        {
            m_poisonBuildup = Mathf.Max(0f, poisonBuildup);
            m_bleedBuildup = Mathf.Max(0f, bleedBuildup);
            m_frostBuildup = Mathf.Max(0f, frostBuildup);
        }

        /// <summary>Returns whether the defender's guard covers this contact.</summary>
        public bool CheckForBlock(CharacterManager damageTarget)
        {
            return CombatResolver.IsBlocked(CreateCandidateFor(damageTarget));
        }

        /// <summary>
        /// Calculates the forward-facing block dot from the target towards the attacker.
        /// Projectile colliders override this to use their own transform as the origin.
        /// </summary>
        public float GetGuardFacingDot(CharacterManager damageTarget)
        {
            return ResolveGuardFacingDot(damageTarget);
        }

        protected virtual float ResolveGuardFacingDot(CharacterManager damageTarget)
        {
            if (damageTarget == null || m_characterCausingDamage == null)
            {
                return -1f;
            }

            Vector3 targetForward = Vector3.ProjectOnPlane(
                damageTarget.transform.forward,
                Vector3.up);
            Vector3 directionToAttackOrigin = Vector3.ProjectOnPlane(
                m_characterCausingDamage.transform.position -
                    damageTarget.transform.position,
                Vector3.up);
            if (targetForward.sqrMagnitude <= Mathf.Epsilon ||
                directionToAttackOrigin.sqrMagnitude <= Mathf.Epsilon)
            {
                return -1f;
            }

            return CalculateBlockingDot(
                targetForward,
                directionToAttackOrigin);
        }

        /// <summary>Calculates a normalized horizontal facing dot for deterministic tests.</summary>
        public static float CalculateBlockingDot(
            Vector3 targetForward,
            Vector3 directionToAttackOrigin)
        {
            Vector3 horizontalForward = Vector3.ProjectOnPlane(
                targetForward,
                Vector3.up);
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(
                directionToAttackOrigin,
                Vector3.up);
            if (horizontalForward.sqrMagnitude <= Mathf.Epsilon ||
                horizontalDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return -1f;
            }

            return Vector3.Dot(
                horizontalDirection.normalized,
                horizontalForward.normalized);
        }

        /// <summary>Selects one peer to initiate prediction: defender for AI hits, otherwise the attacker's owner.</summary>
        public bool CanInitiateDamage(CharacterManager target)
        {
            if (target == null || !target.IsSpawned)
            {
                return true;
            }

            if (m_characterCausingDamage is AICharacterManager)
            {
                return m_characterCausingDamage.IsServer;
            }

            if (m_characterCausingDamage == null)
            {
                return target.IsOwner;
            }

            return m_characterCausingDamage.IsOwner;
        }

        private HitCandidate CreateCandidateFor(CharacterManager damageTarget)
        {
            if (damageTarget == null)
            {
                return default;
            }

            Collider targetCollider = damageTarget.GetComponentInChildren<HurtBox>(true) is HurtBox hurtBox
                ? hurtBox.GetComponent<Collider>()
                : damageTarget.GetComponent<Collider>();
            return new HitCandidate(this, m_characterCausingDamage, targetCollider);
        }

        private Collider GetDamageCollider()
        {
            m_damageCollider ??= GetComponent<Collider>();
            return m_damageCollider;
        }

        private void ResetCharactersDamaged()
        {
            m_registry.Clear();
        }
    }
}
