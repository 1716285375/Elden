using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace ZZ
{
    /// <summary>
    /// Owns the replicated Boss encounter state, health phases, and phase attack selection.
    /// </summary>
    [RequireComponent(typeof(AICharacterManager))]
    [RequireComponent(typeof(AICharacterNetworkManager))]
    [RequireComponent(typeof(CharacterStatsManager))]
    public class BossCharacterManager : NetworkBehaviour
    {
        [Header("Identity")]
        [SerializeField, Min(1)] private int m_bossID = 1;
        [SerializeField] private string m_bossName = "FALLEN WATCHER";

        [Header("Stats")]
        [SerializeField, Min(1f)] private float m_maximumHealth = 600f;

        [Header("Phases")]
        [SerializeField] private List<BossPhaseData> m_phases = new();

        private readonly NetworkVariable<bool> m_isEncounterActive =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> m_currentPhaseIndex =
            new NetworkVariable<int>(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private AICharacterManager m_aiCharacter;
        private AICharacterNetworkManager m_networkManager;
        private NavMeshAgent m_navMeshAgent;

        /// <summary>Raised on each peer when the replicated encounter state changes.</summary>
        public event Action<BossCharacterManager, bool> EncounterStateChanged;

        /// <summary>Raised on each peer when the active phase changes.</summary>
        public event Action<BossCharacterManager, int> PhaseChanged;

        public int BossID => m_bossID;
        public string BossName => m_bossName;
        public bool IsEncounterActive => m_isEncounterActive.Value;
        public bool HasBeenDefeated =>
            m_aiCharacter?.OriginSpawner?.HasBossBeenDefeated == true;
        public int CurrentPhaseIndex => m_currentPhaseIndex.Value;
        public CharacterNetworkManager CharacterNetworkManager => m_networkManager;

        private void Awake()
        {
            m_aiCharacter = GetComponent<AICharacterManager>();
            m_networkManager = GetComponent<AICharacterNetworkManager>();
            m_navMeshAgent = GetComponent<NavMeshAgent>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_isEncounterActive.OnValueChanged += OnEncounterStateChanged;
            m_currentPhaseIndex.OnValueChanged += OnPhaseIndexChanged;
            m_networkManager.CurrentHealth.OnValueChanged += OnCurrentHealthChanged;

            if (IsServer)
            {
                m_aiCharacter.RestoreBossAwakeningProgress(
                    m_aiCharacter.OriginSpawner?.HasBossBeenAwakened == true);
                InitializeServerHealth();
                m_currentPhaseIndex.Value = GetPhaseIndexForHealth(
                    m_networkManager.CurrentHealth.Value,
                    m_networkManager.MaxHealth.Value);
            }

            ApplyPhase(m_currentPhaseIndex.Value, false);
        }

        public override void OnNetworkDespawn()
        {
            m_isEncounterActive.OnValueChanged -= OnEncounterStateChanged;
            m_currentPhaseIndex.OnValueChanged -= OnPhaseIndexChanged;
            m_networkManager.CurrentHealth.OnValueChanged -= OnCurrentHealthChanged;
            base.OnNetworkDespawn();
        }

        /// <summary>Starts this encounter on the server using the entering player as its target.</summary>
        public void BeginEncounter(PlayerManager enteringPlayer)
        {
            if (!IsSpawned || !IsServer || m_isEncounterActive.Value)
            {
                return;
            }

            m_isEncounterActive.Value = true;
            m_aiCharacter.BeginBossEncounter(enteringPlayer);
        }

        /// <summary>Closes the replicated encounter when the Boss death lifecycle begins.</summary>
        public void CompleteEncounter()
        {
            if (IsSpawned && IsServer)
            {
                m_isEncounterActive.Value = false;
            }
        }

        internal bool HasAttackInRange(float targetDistance)
        {
            return GetCurrentPhase()?.HasAttackInRange(targetDistance) ?? false;
        }

        internal float GetMaximumAttackRange(float fallbackRange)
        {
            float phaseRange = GetCurrentPhase()?.GetMaximumAttackRange() ?? 0f;
            return phaseRange > 0f ? phaseRange : fallbackRange;
        }

        internal BossAttackData SelectAttack(float targetDistance)
        {
            return GetCurrentPhase()?.SelectAttack(targetDistance);
        }

        internal int GetPhaseIndexForHealth(float currentHealth, float maximumHealth)
        {
            if (m_phases.Count == 0 || maximumHealth <= 0f)
            {
                return 0;
            }

            float healthRatio = Mathf.Clamp01(currentHealth / maximumHealth);
            int selectedPhase = 0;
            float selectedThreshold = float.PositiveInfinity;
            for (int phaseIndex = 0; phaseIndex < m_phases.Count; phaseIndex++)
            {
                BossPhaseData phase = m_phases[phaseIndex];
                if (phase == null ||
                    healthRatio > phase.HealthThreshold ||
                    phase.HealthThreshold >= selectedThreshold)
                {
                    continue;
                }

                selectedPhase = phaseIndex;
                selectedThreshold = phase.HealthThreshold;
            }

            return selectedPhase;
        }

        private void InitializeServerHealth()
        {
            m_networkManager.MaxHealth.Value = Mathf.Max(1f, m_maximumHealth);
            m_networkManager.CurrentHealth.Value = m_networkManager.MaxHealth.Value;
            m_networkManager.IsDead.Value = false;
        }

        private BossPhaseData GetCurrentPhase()
        {
            return m_currentPhaseIndex.Value >= 0 &&
                m_currentPhaseIndex.Value < m_phases.Count
                ? m_phases[m_currentPhaseIndex.Value]
                : null;
        }

        private void OnCurrentHealthChanged(float previousHealth, float currentHealth)
        {
            if (!IsServer)
            {
                return;
            }

            int phaseIndex = GetPhaseIndexForHealth(
                currentHealth,
                m_networkManager.MaxHealth.Value);
            if (phaseIndex != m_currentPhaseIndex.Value)
            {
                m_currentPhaseIndex.Value = phaseIndex;
            }
        }

        private void OnEncounterStateChanged(bool wasActive, bool isActive)
        {
            EncounterStateChanged?.Invoke(this, isActive);
        }

        private void OnPhaseIndexChanged(int previousPhase, int currentPhase)
        {
            ApplyPhase(currentPhase, currentPhase > previousPhase);
            PhaseChanged?.Invoke(this, currentPhase);
        }

        private void ApplyPhase(int phaseIndex, bool playTransition)
        {
            if (phaseIndex < 0 || phaseIndex >= m_phases.Count)
            {
                return;
            }

            BossPhaseData phase = m_phases[phaseIndex];
            if (phase == null)
            {
                return;
            }

            if (m_navMeshAgent != null)
            {
                m_navMeshAgent.speed = phase.MovementSpeed;
            }

            if (playTransition)
            {
                m_aiCharacter.CloseAttackDamageColliders();
                GetComponentInChildren<AICharacterAnimatorManager>(true)
                    ?.PlayBossPhaseTransition();
            }
        }
    }
}
