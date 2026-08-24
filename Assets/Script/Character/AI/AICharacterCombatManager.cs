using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Owns the server-authoritative AI attack and its animation-driven hit windows.
    /// </summary>
    [RequireComponent(typeof(AICharacterManager))]
    public class AICharacterCombatManager : CharacterCombatManager
    {
        [Header("Damage Colliders")]
        [SerializeField] private AIDamageCollider m_leftHandDamageCollider;
        [SerializeField] private AIDamageCollider m_rightHandDamageCollider;

        [Header("Attack Damage")]
        [SerializeField, Min(0f)] private float m_physicalDamage = 25f;
        [SerializeField, Min(0f)] private float m_poiseDamage = 15f;
        [SerializeField] private bool m_defaultAttackIsParryable = true;

        [Header("Stance")]
        [SerializeField, Min(1)] private int m_maximumStance = 80;
        [SerializeField] private bool m_ignoreStanceBreak;
        [SerializeField, Min(0)] private int m_stanceRegeneratedPerSecond = 15;
        [SerializeField, Min(0f)] private float
            m_defaultTimeUntilStanceRegenerationBegins = 3f;

        private readonly HashSet<CharacterManager> m_charactersDamaged = new();

        private AICharacterManager m_aiCharacter;
        private BossAttackData m_currentBossAttack;
        private int m_currentStance;
        private float m_stanceRegenerationTimer;
        private float m_stanceTickTimer;

        public int MaximumStance => m_maximumStance;
        public int CurrentStance => m_currentStance;
        public float StanceRegenerationTimer => m_stanceRegenerationTimer;
        public bool IgnoreStanceBreak => m_ignoreStanceBreak;

        protected override void Awake()
        {
            base.Awake();
            m_aiCharacter = GetComponent<AICharacterManager>();
            m_currentStance = Mathf.Max(1, m_maximumStance);
            ConfigureDamageColliders();
            CloseDamageColliders();
        }

        private void FixedUpdate()
        {
            if (m_aiCharacter == null ||
                !m_aiCharacter.IsOwner ||
                m_aiCharacter.IsDead)
            {
                return;
            }

            HandleStanceBreak();
            RegenerateStance(Time.fixedDeltaTime);
        }

        /// <summary>Applies owner-authoritative Stance damage and resets its recovery delay.</summary>
        public void DamageStance(int stanceDamage)
        {
            if (m_aiCharacter == null ||
                !m_aiCharacter.IsOwner ||
                stanceDamage <= 0)
            {
                return;
            }

            m_stanceRegenerationTimer =
                m_defaultTimeUntilStanceRegenerationBegins;
            m_stanceTickTimer = 0f;
            m_currentStance -= stanceDamage;
        }

        /// <summary>Allows authored transitions to consume a break without playing it.</summary>
        public void SetIgnoreStanceBreak(bool shouldIgnoreStanceBreak)
        {
            m_ignoreStanceBreak = shouldIgnoreStanceBreak;
        }

        /// <summary>Starts the predicted server attack and replicates it to clients.</summary>
        public bool PerformAttack()
        {
            return PerformAttack(null);
        }

        /// <summary>Starts one server-selected data-driven attack.</summary>
        public bool PerformAttack(BossAttackData bossAttack)
        {
            if (m_aiCharacter == null ||
                !m_aiCharacter.IsServer ||
                m_aiCharacter.IsDead ||
                m_aiCharacter.IsPerformingAction)
            {
                return false;
            }

            m_currentBossAttack = bossAttack;
            PrepareAttackDamage();
            AttackType attackType = bossAttack != null
                ? bossAttack.AttackType
                : AttackType.LightAttack01;
            m_aiCharacter.CharacterNetworkManager?.SetParryableState(
                bossAttack?.IsParryable ?? m_defaultAttackIsParryable);
            ReplicateAttack(attackType);
            m_aiCharacter.CharacterNetworkManager
                ?.NotifyServerOfAttackActionServerRpc(attackType);
            return m_aiCharacter.IsPerformingAction;
        }

        /// <summary>Refreshes the damage payload and clears the per-attack hit registry.</summary>
        public void PrepareAttackDamage()
        {
            m_charactersDamaged.Clear();
            ConfigureDamageColliders();
        }

        /// <summary>Opens the left-hand active frames on the server.</summary>
        public void OpenLeftHandDamageCollider()
        {
            if (m_aiCharacter != null && m_aiCharacter.IsServer)
            {
                m_leftHandDamageCollider?.OpenDamageCollider();
            }
        }

        /// <summary>Closes the left-hand active frames.</summary>
        public void CloseLeftHandDamageCollider()
        {
            m_leftHandDamageCollider?.CloseDamageCollider();
        }

        /// <summary>Opens the right-hand active frames on the server.</summary>
        public void OpenRightHandDamageCollider()
        {
            if (m_aiCharacter != null && m_aiCharacter.IsServer)
            {
                m_rightHandDamageCollider?.OpenDamageCollider();
            }
        }

        /// <summary>Closes every active AI damage collider.</summary>
        public void CloseDamageColliders()
        {
            m_leftHandDamageCollider?.CloseDamageCollider();
            m_rightHandDamageCollider?.CloseDamageCollider();
        }

        /// <inheritdoc />
        public override void CloseAllDamageColliders()
        {
            CloseDamageColliders();
        }

        internal bool TryRegisterDamageTarget(CharacterManager target)
        {
            return m_aiCharacter != null &&
                m_aiCharacter.IsServer &&
                target != null &&
                target != m_aiCharacter &&
                !target.IsDead &&
                m_charactersDamaged.Add(target);
        }

        private void HandleStanceBreak()
        {
            if (m_currentStance > 0)
            {
                return;
            }

            CharacterNetworkManager networkManager =
                m_aiCharacter.CharacterNetworkManager;
            DamageIntensity previousDamageIntensity =
                WorldUtilityManager.GetDamageIntensityBasedOnPoiseDamage(
                    PreviousPoiseDamageTaken);
            if (previousDamageIntensity == DamageIntensity.Colossal ||
                networkManager?.IsBeingCriticallyDamaged.Value == true)
            {
                m_currentStance = 1;
                return;
            }

            m_currentStance = Mathf.Max(1, m_maximumStance);
            m_stanceRegenerationTimer = 0f;
            m_stanceTickTimer = 0f;
            if (m_ignoreStanceBreak)
            {
                return;
            }

            m_aiCharacter.CloseAttackDamageColliders();
            m_aiCharacter.StopMoving();
            m_aiCharacter.CharacterAnimatorManager
                ?.PlayTargetActionAnimationInstantly(
                    CharacterActionAnimation.StanceBreak,
                    true);
            if (m_aiCharacter.IsSpawned)
            {
                networkManager?.NotifyServerOfInstantActionAnimationServerRpc(
                    CharacterActionAnimation.StanceBreak,
                    true,
                    false,
                    false,
                    false);
            }
        }

        private void RegenerateStance(float deltaTime)
        {
            if (m_currentStance >= m_maximumStance)
            {
                m_currentStance = m_maximumStance;
                m_stanceRegenerationTimer = 0f;
                m_stanceTickTimer = 0f;
                return;
            }

            if (m_stanceRegenerationTimer > 0f)
            {
                m_stanceRegenerationTimer = Mathf.Max(
                    0f,
                    m_stanceRegenerationTimer - deltaTime);
                return;
            }

            m_stanceTickTimer += deltaTime;
            while (m_stanceTickTimer >= 1f)
            {
                m_stanceTickTimer -= 1f;
                m_currentStance = Mathf.Min(
                    m_maximumStance,
                    m_currentStance + m_stanceRegeneratedPerSecond);
            }
        }

        private void ConfigureDamageColliders()
        {
            ConfigureDamageCollider(m_leftHandDamageCollider);
            ConfigureDamageCollider(m_rightHandDamageCollider);
        }

        private void ConfigureDamageCollider(AIDamageCollider damageCollider)
        {
            if (damageCollider == null)
            {
                return;
            }

            damageCollider.SetDamageSource(m_aiCharacter);
            if (m_currentBossAttack != null)
            {
                damageCollider.SetDamageValues(
                    m_currentBossAttack.PhysicalDamage,
                    m_currentBossAttack.MagicDamage,
                    m_currentBossAttack.FireDamage,
                    m_currentBossAttack.LightningDamage,
                    m_currentBossAttack.HolyDamage,
                    m_currentBossAttack.PoiseDamage);
                return;
            }

            damageCollider.SetDamageValues(
                m_physicalDamage,
                0f,
                0f,
                0f,
                0f,
                m_poiseDamage);
        }
    }
}
