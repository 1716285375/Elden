using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(Collider))]
    public class DamageCollider : MonoBehaviour
    {
        private const float k_MinimumBlockingDot = 0.3f;

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

            if (!WorldUtilityManager.CanDamageCharacter(
                    m_characterCausingDamage,
                    target) ||
                target.IsInvulnerable)
            {
                return;
            }

            if (CheckForParry(target))
            {
                return;
            }

            Vector3 contactPoint = other.ClosestPointOnBounds(transform.position);
            bool wasBlocked = CheckForBlock(target);
            if (!wasBlocked)
            {
                m_charactersDamaged.Add(target);
            }

            Damage(target, contactPoint, wasBlocked);
        }

        /// <summary>
        /// Resolves Parry before Block and Damage for this collider type.
        /// Custom collider types override this method so shared hit registries can participate.
        /// </summary>
        protected virtual bool CheckForParry(CharacterManager damageTarget)
        {
            return CanParryDamageTarget(damageTarget) &&
                ProcessSuccessfulParry(damageTarget);
        }

        /// <summary>Returns whether both replicated Parry flags form a valid pair.</summary>
        protected bool CanParryDamageTarget(CharacterManager damageTarget)
        {
            CharacterNetworkManager attackerNetworkManager =
                m_characterCausingDamage?.CharacterNetworkManager;
            CharacterNetworkManager targetNetworkManager =
                damageTarget?.CharacterNetworkManager;
            return damageTarget != null &&
                !m_charactersDamaged.Contains(damageTarget) &&
                attackerNetworkManager?.IsParryable.Value == true &&
                targetNetworkManager?.IsParrying.Value == true;
        }

        /// <summary>Consumes this hit and dispatches the validated Parry request.</summary>
        protected bool ProcessSuccessfulParry(CharacterManager damageTarget)
        {
            if (damageTarget == null ||
                m_charactersDamaged.Contains(damageTarget))
            {
                return false;
            }

            m_charactersDamaged.Add(damageTarget);
            CharacterNetworkManager targetNetworkManager =
                damageTarget.CharacterNetworkManager;
            if (m_characterCausingDamage != null &&
                m_characterCausingDamage.IsSpawned &&
                damageTarget.IsSpawned &&
                targetNetworkManager != null)
            {
                targetNetworkManager.RequestParry(
                    m_characterCausingDamage.NetworkObjectId);
                return true;
            }

            m_characterCausingDamage?.CharacterCombatManager
                ?.ProcessParryFromServer(damageTarget);
            return true;
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

        /// <summary>
        /// Configures the damage payload emitted during this collider's hit window.
        /// </summary>
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
        }

        /// <summary>
        /// Returns whether the target is actively blocking a hit that originates in front.
        /// Successful blocks immediately enter the per-window hit registry.
        /// </summary>
        public bool CheckForBlock(CharacterManager damageTarget)
        {
            CharacterNetworkManager targetNetworkManager =
                damageTarget?.CharacterNetworkManager;
            if (targetNetworkManager == null ||
                !targetNetworkManager.IsBlocking.Value ||
                GetBlockingDotValues(damageTarget) <= k_MinimumBlockingDot)
            {
                return false;
            }

            if (!m_charactersDamaged.Contains(damageTarget))
            {
                m_charactersDamaged.Add(damageTarget);
            }

            return true;
        }

        /// <summary>
        /// Calculates the forward-facing block dot from the target towards the melee attacker.
        /// Projectile colliders can override this to use their own transform as the origin.
        /// </summary>
        protected virtual float GetBlockingDotValues(CharacterManager damageTarget)
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

        protected virtual void Damage(
            CharacterManager target,
            Vector3 contactPoint,
            bool wasBlocked)
        {
            CharacterNetworkManager networkManager =
                m_characterCausingDamage?.CharacterNetworkManager;
            if (networkManager != null && networkManager.IsSpawned)
            {
                networkManager.RequestCharacterDamageServerRpc(
                    target.NetworkObjectId,
                    m_characterCausingDamage.NetworkObjectId,
                    m_physicalDamage,
                    m_magicDamage,
                    m_fireDamage,
                    m_lightningDamage,
                    m_holyDamage,
                    m_poiseDamage,
                    contactPoint,
                    wasBlocked);
                return;
            }

            ApplyDamageLocally(target, contactPoint, wasBlocked);
        }

        private void ApplyDamageLocally(
            CharacterManager target,
            Vector3 contactPoint,
            bool wasBlocked)
        {
            CharacterEffectsManager effectsManager = target.CharacterEffectsManager;
            if (effectsManager == null)
            {
                Debug.LogWarning(
                    "Damage requires a target effects manager.",
                    this);
                return;
            }

            InstantCharacterEffect runtimeEffect = CreateRuntimeDamageEffect(
                target,
                contactPoint,
                wasBlocked);
            if (runtimeEffect == null)
            {
                Debug.LogWarning(
                    "Damage requires the matching world damage template.",
                    this);
                return;
            }

            effectsManager.ProcessRuntimeInstantEffect(runtimeEffect);
        }

        private InstantCharacterEffect CreateRuntimeDamageEffect(
            CharacterManager target,
            Vector3 contactPoint,
            bool wasBlocked)
        {
            WorldCharacterEffectsManager effectsManager =
                WorldCharacterEffectsManager.Instance;
            if (wasBlocked)
            {
                TakeBlockedDamageEffect blockedTemplate =
                    effectsManager?.TakeBlockedDamageEffect;
                CharacterStatsManager statsManager = target.CharacterStatsManager;
                if (blockedTemplate == null || statsManager == null)
                {
                    return null;
                }

                return blockedTemplate.CreateRuntimeBlockedDamageEffect(
                    m_characterCausingDamage,
                    m_physicalDamage,
                    m_magicDamage,
                    m_fireDamage,
                    m_lightningDamage,
                    m_holyDamage,
                    contactPoint,
                    m_poiseDamage,
                    statsManager.BlockingPhysicalAbsorption,
                    statsManager.BlockingMagicAbsorption,
                    statsManager.BlockingFireAbsorption,
                    statsManager.BlockingLightningAbsorption,
                    statsManager.BlockingHolyAbsorption,
                    statsManager.BlockingStability);
            }

            return effectsManager?.TakeDamageEffect?.CreateRuntimeDamageEffect(
                m_characterCausingDamage,
                m_physicalDamage,
                m_magicDamage,
                m_fireDamage,
                m_lightningDamage,
                m_holyDamage,
                contactPoint,
                m_poiseDamage);
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
