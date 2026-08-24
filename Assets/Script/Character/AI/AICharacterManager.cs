using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZZ
{
    /// <summary>
    /// Coordinates one server-authoritative enemy's perception, navigation, combat, and state.
    /// </summary>
    [RequireComponent(typeof(AICharacterNetworkManager))]
    [RequireComponent(typeof(AICharacterCombatManager))]
    [RequireComponent(typeof(CharacterStatsManager))]
    [RequireComponent(typeof(CharacterEffectsManager))]
    [RequireComponent(typeof(AICharacterInventoryManager))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class AICharacterManager : CharacterManager
    {
        private const float k_NavMeshSampleDistance = 3f;
        private const float k_MinimumDirectionMagnitude = 0.0001f;

        [Header("Perception")]
        [SerializeField, Min(0f)] private float m_detectionRadius = 18f;
        [SerializeField, Min(0f)] private float m_loseTargetRadius = 24f;
        [SerializeField, Range(1f, 360f)] private float m_fieldOfView = 140f;
        [SerializeField, Min(0.02f)] private float m_detectionInterval = 0.2f;
        [SerializeField, Min(0f)] private float m_eyeHeight = 1.4f;
        [SerializeField] private LayerMask m_lineOfSightLayers = ~0;

        [Header("Combat")]
        [SerializeField, Min(0f)] private float m_combatStanceDistance = 3.2f;
        [SerializeField, Min(0f)] private float m_attackDistance = 2.1f;
        [SerializeField, Min(0f)] private float m_attackCooldown = 2.6f;

        [Header("Turning")]
        [SerializeField, Min(0f)] private float m_turningSpeed = 300f;
        [SerializeField, Range(0f, 180f)] private float m_pivotAngle = 70f;
        [SerializeField, Min(0f)] private float m_pivotCooldown = 1.1f;

        [Header("References")]
        [SerializeField] private NavMeshAgent m_navMeshAgent;
        [SerializeField] private CapsuleCollider m_bodyCollider;
        [SerializeField] private AICharacterAnimatorManager m_aiAnimatorManager;
        [SerializeField] private AICharacterNetworkManager m_aiNetworkManager;
        [SerializeField] private AICharacterCombatManager m_aiCombatManager;
        [SerializeField] private AICharacterInventoryManager m_aiInventoryManager;

        private readonly RaycastHit[] m_sightHits = new RaycastHit[16];

        private AICharacterStateMachine m_stateMachine;
        private AICharacterSpawner m_originSpawner;
        private BossCharacterManager m_bossCharacter;
        private PlayerManager m_currentTarget;
        private float m_nextDetectionTime;
        private float m_nextAttackTime;
        private float m_nextPivotTime;

        /// <summary>Gets the server-selected player target.</summary>
        public PlayerManager CurrentTarget => m_currentTarget;

        /// <summary>Gets this enemy's server-authoritative loot inventory.</summary>
        public AICharacterInventoryManager InventoryManager => m_aiInventoryManager;

        /// <summary>Gets the state currently published to the network.</summary>
        public AICharacterStateId CurrentState => m_aiNetworkManager != null
            ? m_aiNetworkManager.CurrentAIState.Value
            : m_stateMachine?.CurrentStateId ?? AICharacterStateId.Idle;

        internal bool HasValidTarget => IsValidTarget(m_currentTarget);
        internal bool IsTargetBeyondLoseDistance =>
            GetTargetDistanceSquared() > m_loseTargetRadius * m_loseTargetRadius;
        internal bool IsTargetWithinCombatRange =>
            GetTargetDistanceSquared() <=
            GetCombatStanceDistance() * GetCombatStanceDistance();
        internal bool CanStartAttack =>
            HasValidTarget &&
            !IsPerformingAction &&
            Time.time >= m_nextAttackTime &&
            IsAttackAvailableAtTargetDistance();

        protected override void Awake()
        {
            base.Awake();
            m_navMeshAgent ??= GetComponent<NavMeshAgent>();
            m_navMeshAgent.updateRotation = false;
            m_bodyCollider ??= GetComponent<CapsuleCollider>();
            m_aiAnimatorManager ??=
                GetComponentInChildren<AICharacterAnimatorManager>(true);
            m_aiNetworkManager ??= GetComponent<AICharacterNetworkManager>();
            m_aiCombatManager ??= GetComponent<AICharacterCombatManager>();
            m_aiInventoryManager ??= GetComponent<AICharacterInventoryManager>();
            m_bossCharacter = GetComponent<BossCharacterManager>();
            m_stateMachine = new AICharacterStateMachine(
                this,
                new IdleAIState(),
                new PursueTargetAIState(),
                new CombatStanceAIState(),
                new AttackAIState(),
                new DeadAIState());
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            WorldAIManager.Instance?.RegisterAI(this);

            if (m_navMeshAgent != null)
            {
                m_navMeshAgent.enabled = IsServer;
            }

            if (IsServer)
            {
                PlaceOnNavMesh();
                m_stateMachine.ChangeState(AICharacterStateId.Idle);
            }
        }

        public override void OnNetworkDespawn()
        {
            WorldAIManager.Instance?.UnregisterAI(this);
            m_originSpawner?.NotifyCharacterDespawned(this);
            CloseAttackDamageColliders();
            if (m_navMeshAgent != null)
            {
                m_navMeshAgent.enabled = false;
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer &&
                !IsDead &&
                (m_bossCharacter == null || m_bossCharacter.IsEncounterActive))
            {
                m_stateMachine.Tick(Time.deltaTime);
            }

            UpdateMovementAnimation();
        }

        /// <inheritdoc />
        public override IEnumerator ProcessDeathEvent(
            bool manuallySelectDeathAnimation = false)
        {
            if (!BeginDeathEvent(manuallySelectDeathAnimation))
            {
                yield break;
            }

            m_stateMachine.ChangeState(AICharacterStateId.Dead);
            if (m_navMeshAgent != null)
            {
                m_navMeshAgent.enabled = false;
            }

            if (m_bodyCollider != null)
            {
                m_bodyCollider.enabled = false;
            }

            if (IsServer)
            {
                m_bossCharacter?.CompleteEncounter();
                m_originSpawner?.MarkBossDefeated();
            }
        }

        /// <summary>Connects this server-spawned AI to its authored scene spawner.</summary>
        public void SetOriginSpawner(AICharacterSpawner originSpawner)
        {
            if (IsSpawned)
            {
                Debug.LogWarning(
                    "An AI origin spawner must be assigned before network spawning.",
                    this);
                return;
            }

            m_originSpawner = originSpawner;
        }

        /// <summary>Assigns the entering player and wakes a dormant server-owned Boss.</summary>
        public void BeginBossEncounter(PlayerManager enteringPlayer)
        {
            if (!IsServer || m_bossCharacter == null || !IsValidTarget(enteringPlayer))
            {
                return;
            }

            m_currentTarget = enteringPlayer;
            m_originSpawner?.MarkBossAwakened();
            m_stateMachine.ChangeState(AICharacterStateId.PursueTarget);
        }

        internal void PublishState(AICharacterStateId stateId)
        {
            m_aiNetworkManager?.SetAIState(stateId);
        }

        internal bool TryAcquireTarget()
        {
            if (HasValidTarget)
            {
                return true;
            }

            if (Time.time < m_nextDetectionTime)
            {
                return false;
            }

            m_nextDetectionTime = Time.time + m_detectionInterval;
            m_currentTarget = FindNearestVisiblePlayer();
            if (m_currentTarget != null)
            {
                m_originSpawner?.MarkBossAwakened();
            }

            return m_currentTarget != null;
        }

        internal void ClearTarget()
        {
            m_currentTarget = null;
        }

        internal void MoveTowardsTarget()
        {
            if (!HasValidTarget || !CanUseNavMeshAgent())
            {
                StopMoving();
                return;
            }

            m_navMeshAgent.isStopped = false;
            m_navMeshAgent.SetDestination(m_currentTarget.transform.position);
            Vector3 facingDirection = m_navMeshAgent.desiredVelocity;
            if (facingDirection.sqrMagnitude <= k_MinimumDirectionMagnitude)
            {
                facingDirection = m_currentTarget.transform.position - transform.position;
            }

            RotateTowards(facingDirection, true);
        }

        internal void StopMoving()
        {
            if (!CanUseNavMeshAgent())
            {
                return;
            }

            m_navMeshAgent.isStopped = true;
            m_navMeshAgent.ResetPath();
        }

        internal void FaceTarget()
        {
            if (HasValidTarget)
            {
                RotateTowards(
                    m_currentTarget.transform.position - transform.position,
                    true);
            }
        }

        internal bool TryStartAttack()
        {
            if (!CanStartAttack || m_aiCombatManager == null)
            {
                return false;
            }

            BossAttackData bossAttack = m_bossCharacter?.SelectAttack(
                Mathf.Sqrt(GetTargetDistanceSquared()));
            if (m_bossCharacter != null && bossAttack == null)
            {
                return false;
            }

            m_nextAttackTime = Time.time + (bossAttack != null
                ? bossAttack.RecoveryTime
                : m_attackCooldown);
            return m_aiCombatManager.PerformAttack(bossAttack);
        }

        internal void CloseAttackDamageColliders()
        {
            m_aiCombatManager?.CloseDamageColliders();
        }

        private PlayerManager FindNearestVisiblePlayer()
        {
            PlayerManager nearestPlayer = null;
            float nearestDistanceSquared = float.PositiveInfinity;
            IReadOnlyList<PlayerManager> sessionPlayers =
                WorldGameSessionManager.Instance?.Players;
            if (sessionPlayers != null)
            {
                for (int playerIndex = 0;
                    playerIndex < sessionPlayers.Count;
                    playerIndex++)
                {
                    EvaluateTarget(
                        sessionPlayers[playerIndex],
                        ref nearestPlayer,
                        ref nearestDistanceSquared);
                }

                return nearestPlayer;
            }

            PlayerManager[] scenePlayers = FindObjectsByType<PlayerManager>(
                FindObjectsSortMode.None);
            foreach (PlayerManager player in scenePlayers)
            {
                EvaluateTarget(
                    player,
                    ref nearestPlayer,
                    ref nearestDistanceSquared);
            }

            return nearestPlayer;
        }

        private void EvaluateTarget(
            PlayerManager player,
            ref PlayerManager nearestPlayer,
            ref float nearestDistanceSquared)
        {
            if (!IsValidTarget(player))
            {
                return;
            }

            Vector3 targetOffset = player.transform.position - transform.position;
            targetOffset.y = 0f;
            float distanceSquared = targetOffset.sqrMagnitude;
            if (distanceSquared > m_detectionRadius * m_detectionRadius ||
                distanceSquared >= nearestDistanceSquared ||
                !IsWithinFieldOfView(targetOffset) ||
                !HasLineOfSight(player))
            {
                return;
            }

            nearestPlayer = player;
            nearestDistanceSquared = distanceSquared;
        }

        private bool IsValidTarget(PlayerManager player)
        {
            return player != null &&
                player.IsSpawned &&
                !player.IsDead &&
                player.gameObject.activeInHierarchy &&
                player.gameObject.scene == gameObject.scene;
        }

        private bool IsWithinFieldOfView(Vector3 targetOffset)
        {
            if (targetOffset.sqrMagnitude <= k_MinimumDirectionMagnitude)
            {
                return true;
            }

            float minimumDot = Mathf.Cos(m_fieldOfView * 0.5f * Mathf.Deg2Rad);
            return Vector3.Dot(transform.forward, targetOffset.normalized) >= minimumDot;
        }

        private bool HasLineOfSight(PlayerManager player)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * m_eyeHeight;
            Vector3 targetPoint = player.transform.position + Vector3.up * m_eyeHeight;
            Vector3 targetDirection = targetPoint - rayOrigin;
            float targetDistance = targetDirection.magnitude;
            if (targetDistance <= Mathf.Epsilon)
            {
                return true;
            }

            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                targetDirection / targetDistance,
                m_sightHits,
                targetDistance,
                m_lineOfSightLayers,
                QueryTriggerInteraction.Ignore);
            float nearestTargetHit = float.PositiveInfinity;
            float nearestObstruction = float.PositiveInfinity;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit hit = m_sightHits[hitIndex];
                CharacterManager hitCharacter = hit.collider != null
                    ? hit.collider.GetComponentInParent<CharacterManager>()
                    : null;
                if (hitCharacter == this)
                {
                    continue;
                }

                if (hitCharacter == player)
                {
                    nearestTargetHit = Mathf.Min(nearestTargetHit, hit.distance);
                    continue;
                }

                nearestObstruction = Mathf.Min(nearestObstruction, hit.distance);
            }

            return nearestTargetHit <= nearestObstruction ||
                float.IsPositiveInfinity(nearestObstruction);
        }

        private float GetTargetDistanceSquared()
        {
            if (!HasValidTarget)
            {
                return float.PositiveInfinity;
            }

            Vector3 targetOffset = m_currentTarget.transform.position - transform.position;
            targetOffset.y = 0f;
            return targetOffset.sqrMagnitude;
        }

        private float GetCombatStanceDistance()
        {
            return m_bossCharacter != null
                ? m_bossCharacter.GetMaximumAttackRange(m_combatStanceDistance)
                : m_combatStanceDistance;
        }

        private bool IsAttackAvailableAtTargetDistance()
        {
            float targetDistanceSquared = GetTargetDistanceSquared();
            if (m_bossCharacter != null)
            {
                return m_bossCharacter.HasAttackInRange(
                    Mathf.Sqrt(targetDistanceSquared));
            }

            return targetDistanceSquared <= m_attackDistance * m_attackDistance;
        }

        private void RotateTowards(Vector3 direction, bool playPivot)
        {
            direction.y = 0f;
            if (!CanRotate || direction.sqrMagnitude <= k_MinimumDirectionMagnitude)
            {
                return;
            }

            float signedAngle = Vector3.SignedAngle(
                transform.forward,
                direction.normalized,
                Vector3.up);
            if (playPivot &&
                Mathf.Abs(signedAngle) >= m_pivotAngle &&
                Time.time >= m_nextPivotTime)
            {
                m_nextPivotTime = Time.time + m_pivotCooldown;
                m_aiNetworkManager?.ReplicatePivot(signedAngle < 0f);
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                m_turningSpeed * Time.deltaTime);
        }

        private void PlaceOnNavMesh()
        {
            if (m_navMeshAgent == null || !m_navMeshAgent.enabled)
            {
                return;
            }

            if (NavMesh.SamplePosition(
                    transform.position,
                    out NavMeshHit hit,
                    k_NavMeshSampleDistance,
                    NavMesh.AllAreas))
            {
                m_navMeshAgent.Warp(hit.position);
            }
        }

        private bool CanUseNavMeshAgent()
        {
            return m_navMeshAgent != null &&
                m_navMeshAgent.enabled &&
                m_navMeshAgent.isOnNavMesh;
        }

        private void UpdateMovementAnimation()
        {
            if (m_aiAnimatorManager == null || m_aiNetworkManager == null)
            {
                return;
            }

            float movementAmount;
            if (IsServer)
            {
                movementAmount = CanUseNavMeshAgent() && m_navMeshAgent.speed > 0f
                    ? Mathf.Clamp01(m_navMeshAgent.velocity.magnitude / m_navMeshAgent.speed)
                    : 0f;
                if (m_aiNetworkManager.IsOwner)
                {
                    m_aiNetworkManager.HorizontalMovement.Value = 0f;
                    m_aiNetworkManager.VerticalMovement.Value = movementAmount;
                    m_aiNetworkManager.MoveAmount.Value = movementAmount;
                }
            }
            else
            {
                movementAmount = m_aiNetworkManager.MoveAmount.Value;
            }

            m_aiAnimatorManager.UpdateAnimatorMovementParameters(
                0f,
                movementAmount,
                false);
        }
    }
}
