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

        [Header("Evasion")]
        [SerializeField, Min(0f)] private float m_maximumEvasionDistance = 5f;

        private readonly HashSet<CharacterManager> m_charactersDamaged = new();
        private readonly List<PlayerManager> m_playersWithinActivationRange = new();

        private AICharacterManager m_aiCharacter;
        private AICharacterAttackAction m_currentAttackAction;
        private bool m_canPerformCombo;
        private bool m_hasHitTargetDuringCombo;
        private int m_currentStance;
        private float m_stanceRegenerationTimer;
        private float m_stanceTickTimer;
        private PlayerManager m_runeRewardCandidate;

        public int MaximumStance => m_maximumStance;
        public int CurrentStance => m_currentStance;
        public float StanceRegenerationTimer => m_stanceRegenerationTimer;
        public bool IgnoreStanceBreak => m_ignoreStanceBreak;
        public AICharacterAttackAction CurrentAttackAction =>
            m_currentAttackAction;
        public bool CanPerformCombo => m_canPerformCombo;
        public bool HasHitTargetDuringCombo => m_hasHitTargetDuringCombo;

        /// <summary>Gets the number of valid players currently keeping this AI active.</summary>
        public int PlayersWithinActivationRangeCount
        {
            get
            {
                PruneMissingPlayersWithinActivationRange();
                return m_playersWithinActivationRange.Count;
            }
        }

        /// <summary>Gets the last player whose owner-authoritative hit damaged this AI.</summary>
        public PlayerManager RuneRewardCandidate => m_runeRewardCandidate;

        /// <summary>Adds one player to the activation range without duplicates.</summary>
        public bool AddPlayerToPlayersWithinRange(PlayerManager player)
        {
            PruneMissingPlayersWithinActivationRange();
            if (player == null || m_playersWithinActivationRange.Contains(player))
            {
                return false;
            }

            m_playersWithinActivationRange.Add(player);
            return true;
        }

        /// <summary>Removes one player and prunes peers that left the session.</summary>
        public bool RemovePlayerFromPlayersWithinRange(PlayerManager player)
        {
            PruneMissingPlayersWithinActivationRange();
            return player != null && m_playersWithinActivationRange.Remove(player);
        }

        /// <summary>Clears every transient player activation reference.</summary>
        public void ClearPlayersWithinActivationRange()
        {
            m_playersWithinActivationRange.Clear();
        }

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
        public bool PerformAttack(AICharacterAttackAction attackAction)
        {
            if (m_aiCharacter == null ||
                !m_aiCharacter.IsServer ||
                m_aiCharacter.IsDead ||
                m_aiCharacter.IsPerformingAction)
            {
                return false;
            }

            DisableCanDoCombo();
            m_currentAttackAction = attackAction;
            PrepareAttackDamage();
            AttackType attackType = attackAction != null
                ? attackAction.AttackType
                : AttackType.LightAttack01;
            m_aiCharacter.CharacterNetworkManager?.SetParryableState(
                attackAction?.IsParryable ?? m_defaultAttackIsParryable);
            ReplicateAttack(attackType);
            m_aiCharacter.CharacterNetworkManager
                ?.NotifyServerOfAttackActionServerRpc(attackType);
            return m_aiCharacter.IsPerformingAction;
        }

        /// <summary>Transitions from an active attack into its authored combo action.</summary>
        public bool PerformCombo(AICharacterAttackAction comboAction)
        {
            if (m_aiCharacter == null ||
                !m_aiCharacter.IsServer ||
                m_aiCharacter.IsDead ||
                !m_canPerformCombo ||
                comboAction == null)
            {
                return false;
            }

            DisableCanDoCombo();
            m_currentAttackAction = comboAction;
            PrepareAttackDamage();
            m_aiCharacter.CharacterNetworkManager?.SetParryableState(
                comboAction.IsParryable);
            ReplicateAttack(comboAction.AttackType);
            m_aiCharacter.CharacterNetworkManager
                ?.NotifyServerOfAttackActionServerRpc(comboAction.AttackType);
            return m_aiCharacter.IsPerformingAction;
        }

        /// <summary>Performs this AI type's server-authored response to a nearby attack.</summary>
        public virtual bool PerformEvasion()
        {
            PlayerManager target = m_aiCharacter?.CurrentTarget;
            if (m_aiCharacter == null ||
                !m_aiCharacter.IsServer ||
                m_aiCharacter.IsDead ||
                m_aiCharacter.IsPerformingAction ||
                target == null ||
                Vector3.Distance(
                    m_aiCharacter.transform.position,
                    target.transform.position) > m_maximumEvasionDistance)
            {
                return false;
            }

            Vector3 evasionDirection = GetBackwardEvasionDirection(
                m_aiCharacter.transform.forward);
            m_aiCharacter.StopMoving();
            m_aiCharacter.CloseAttackDamageColliders();
            m_aiCharacter.SetBlockingState(false);
            m_aiCharacter.transform.rotation = Quaternion.LookRotation(
                evasionDirection,
                Vector3.up);
            m_aiCharacter.SetInvulnerable(true);
            m_aiCharacter.CharacterAnimatorManager?.PlayTargetActionAnimation(
                CharacterActionAnimation.RollForward,
                true,
                true,
                false,
                false);
            if (!m_aiCharacter.IsPerformingAction)
            {
                m_aiCharacter.SetInvulnerable(false);
                return false;
            }

            m_aiCharacter.CharacterNetworkManager
                ?.NotifyServerOfActionAnimationServerRpc(
                    CharacterActionAnimation.RollForward,
                    true,
                    true,
                    false,
                    false);
            return true;
        }

        internal static Vector3 GetBackwardEvasionDirection(Vector3 forward)
        {
            Vector3 direction = -forward;
            direction.y = 0f;
            return direction.sqrMagnitude > Mathf.Epsilon
                ? direction.normalized
                : Vector3.back;
        }

        /// <summary>Refreshes the damage payload and clears the per-attack hit registry.</summary>
        public void PrepareAttackDamage()
        {
            m_charactersDamaged.Clear();
            m_hasHitTargetDuringCombo = false;
            ConfigureDamageColliders();
        }

        /// <summary>Opens the authored animation-frame window for one combo transition.</summary>
        public void EnableCanDoCombo()
        {
            if (m_aiCharacter != null && m_aiCharacter.IsServer)
            {
                m_canPerformCombo = true;
            }
        }

        /// <summary>Closes the combo window and clears its per-attack hit confirmation.</summary>
        public void DisableCanDoCombo()
        {
            m_canPerformCombo = false;
            m_hasHitTargetDuringCombo = false;
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

        /// <inheritdoc />
        public override void ResetActionState()
        {
            base.ResetActionState();
            DisableCanDoCombo();
        }

        /// <summary>Records the player eligible for a Rune award if this hit causes death.</summary>
        public void RecordRuneRewardCandidate(PlayerManager player)
        {
            if (m_aiCharacter != null && m_aiCharacter.IsOwner && player != null)
            {
                m_runeRewardCandidate = player;
                m_aiCharacter.SetTarget(player);
            }
        }

        /// <summary>Moves an idle, living server AI into sound investigation when allowed.</summary>
        public bool AlertCharacterToSound(Vector3 positionOfSound)
        {
            return m_aiCharacter != null &&
                m_aiCharacter.IsOwner &&
                m_aiCharacter.IsServer &&
                !m_aiCharacter.IsDead &&
                m_aiCharacter.CurrentState == AICharacterStateId.Idle &&
                m_aiCharacter.WillInvestigateSound &&
                m_aiCharacter.BeginSoundInvestigation(positionOfSound);
        }

        /// <summary>Clears stale kill credit when this AI is revived or reused.</summary>
        public void ClearRuneRewardCandidate()
        {
            m_runeRewardCandidate = null;
        }

        /// <summary>Awards this AI's configured Rune value to its locally owned killer.</summary>
        public void AwardRunesOnDeath(PlayerManager player)
        {
            int baseReward = m_aiCharacter?.CharacterStatsManager
                ?.RunesDroppedOnDeath ?? 0;
            AwardRunesOnDeath(player, baseReward);
        }

        /// <summary>Awards a server-authorized Rune value on the killer's owning peer.</summary>
        public void AwardRunesOnDeath(PlayerManager player, int baseReward)
        {
            if (player == null ||
                player.IsSpawned && !player.IsOwner ||
                m_aiCharacter == null ||
                !CanAwardRunes(
                    m_aiCharacter.CharacterGroup,
                    player.CharacterGroup))
            {
                return;
            }

            float runeGainModifier = GetRuneGainModifier(player);
            int finalReward = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Max(0, baseReward) * runeGainModifier),
                0,
                int.MaxValue);
            player.PlayerStatsManager?.AddRunes(finalReward);
        }

        /// <summary>Returns whether different factions form a valid Rune reward pair.</summary>
        public static bool CanAwardRunes(
            CharacterGroup defeatedGroup,
            CharacterGroup playerGroup)
        {
            return defeatedGroup != playerGroup;
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

        internal void RecordSuccessfulHit(CharacterManager target)
        {
            if (m_aiCharacter != null &&
                m_aiCharacter.IsServer &&
                target != null &&
                target != m_aiCharacter &&
                !target.IsDead)
            {
                m_hasHitTargetDuringCombo = true;
            }
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

        private void PruneMissingPlayersWithinActivationRange()
        {
            for (int index = m_playersWithinActivationRange.Count - 1;
                index >= 0;
                index--)
            {
                if (m_playersWithinActivationRange[index] == null)
                {
                    m_playersWithinActivationRange.RemoveAt(index);
                }
            }
        }

        private static float GetRuneGainModifier(PlayerManager player)
        {
            return player != null ? 1f : 0f;
        }

        private void ConfigureDamageCollider(AIDamageCollider damageCollider)
        {
            if (damageCollider == null)
            {
                return;
            }

            damageCollider.SetDamageSource(m_aiCharacter);
            if (m_currentAttackAction != null)
            {
                damageCollider.SetDamageValues(
                    m_currentAttackAction.PhysicalDamage,
                    m_currentAttackAction.MagicDamage,
                    m_currentAttackAction.FireDamage,
                    m_currentAttackAction.LightningDamage,
                    m_currentAttackAction.HolyDamage,
                    m_currentAttackAction.PoiseDamage);
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
