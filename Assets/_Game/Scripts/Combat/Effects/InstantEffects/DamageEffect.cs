using UnityEngine;

namespace ZZ
{
    /// <summary>Base payload shared by predicted, authoritative, and replicated damage.</summary>
    public abstract class DamageEffect : InstantCharacterEffect
    {
        public CharacterManager CharacterCausingDamage { get; protected set; }
        public float PhysicalDamage { get; protected set; }
        public float MagicDamage { get; protected set; }
        public float FireDamage { get; protected set; }
        public float LightningDamage { get; protected set; }
        public float HolyDamage { get; protected set; }
        public int FinalDamageDealt { get; protected set; }
        public Vector3 ContactPoint { get; protected set; }
        public bool WasBlocked { get; protected set; }
        public bool WasTargetInvulnerable { get; protected set; }
        public float PoiseDamage { get; protected set; }
        public float StanceDamage { get; private set; }

        /// <summary>Snapshots stance separately from reaction poise before the effect is dispatched.</summary>
        public void SetStanceDamage(float damage)
        {
            StanceDamage = Mathf.Max(0f, damage);
        }
        public float ProjectedHealth { get; private set; }
        public float ProjectedPoise { get; private set; }
        public int ProjectedStance { get; private set; }

        /// <summary>Routes legacy instant-effect calls through the target's current authority.</summary>
        public sealed override void ProcessEffect(CharacterManager character)
        {
            DamageProcessingMode processingMode =
                character == null || !character.IsSpawned || character.IsOwner
                    ? DamageProcessingMode.Authoritative
                    : DamageProcessingMode.ReplicatedPresentation;
            ProcessDamage(character, processingMode);
        }

        /// <summary>Processes this payload without conflating state mutation and presentation.</summary>
        public abstract void ProcessDamage(
            CharacterManager character,
            DamageProcessingMode processingMode);

        /// <summary>Assigns sanitized hit data to one transient damage payload.</summary>
        protected void ConfigureRuntimeDamage(
            CharacterManager characterCausingDamage,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            Vector3 contactPoint,
            float poiseDamage)
        {
            CharacterCausingDamage = characterCausingDamage;
            PhysicalDamage = Mathf.Max(0f, physicalDamage);
            MagicDamage = Mathf.Max(0f, magicDamage);
            FireDamage = Mathf.Max(0f, fireDamage);
            LightningDamage = Mathf.Max(0f, lightningDamage);
            HolyDamage = Mathf.Max(0f, holyDamage);
            ContactPoint = contactPoint;
            PoiseDamage = Mathf.Max(0f, poiseDamage);
            SetStanceDamage(poiseDamage);
        }

        /// <summary>Calculates combined raw damage with the existing minimum-hit rule.</summary>
        public virtual int CalculateDamage()
        {
            float combinedDamage = PhysicalDamage + MagicDamage + FireDamage +
                LightningDamage + HolyDamage;
            FinalDamageDealt = Mathf.Max(1, Mathf.RoundToInt(combinedDamage));
            return FinalDamageDealt;
        }

        /// <summary>Calculates damage from the replicated armor snapshot.</summary>
        public virtual int CalculateDamage(CharacterStatsManager statsManager)
        {
            if (statsManager == null)
            {
                return CalculateDamage();
            }

            float combinedDamage =
                ResolveChannelDamage(DamageType.Physical, PhysicalDamage, statsManager) +
                ResolveChannelDamage(DamageType.Magic, MagicDamage, statsManager) +
                ResolveChannelDamage(DamageType.Fire, FireDamage, statsManager) +
                ResolveChannelDamage(DamageType.Lightning, LightningDamage, statsManager) +
                ResolveChannelDamage(DamageType.Holy, HolyDamage, statsManager);
            FinalDamageDealt = Mathf.Max(1, Mathf.RoundToInt(combinedDamage));
            return FinalDamageDealt;
        }

        /// <summary>Runs one channel through the shared defense and absorption formula.</summary>
        protected static float ResolveChannelDamage(
            DamageType damageType,
            float damage,
            CharacterStatsManager statsManager)
        {
            return DamageResolver.ResolveChannel(
                damage,
                statsManager.GetDefense(damageType),
                statsManager.GetArmorAbsorption(damageType));
        }

        /// <summary>Hands one settled hit to the feedback layer as a complete result.</summary>
        protected void PresentImpactFeedback(
            CharacterManager target,
            HitOutcome outcome,
            float impactPoiseDamage)
        {
            bool poiseBroken = ProjectedPoise <= 0f;
            CombatHitFeedback.Present(new HitResult(outcome, CharacterCausingDamage, target, default,
                new HitSettlement(FinalDamageDealt, 0f, impactPoiseDamage, 0f, poiseBroken, false,
                    false, false, false, ProjectedHealth <= 0f,
                    ReactionResolver.ResolveReaction(ReactionProfile.Default, impactPoiseDamage,
                        poiseBroken, false))));
        }

        /// <summary>Updates deterministic presentation projections without mutating network state.</summary>
        protected void UpdateProjectedState(CharacterManager character, int damage)
        {
            CharacterNetworkManager networkManager =
                character?.CharacterNetworkManager;
            CharacterStatsManager statsManager = character?.CharacterStatsManager;
            float currentHealth = networkManager != null
                ? networkManager.CurrentHealth.Value
                : 0f;
            ProjectedHealth = CalculateProjectedHealth(currentHealth, damage);
            ProjectedPoise = statsManager != null
                ? statsManager.RemainingPoise - (character.CharacterCombatManager != null
                    ? character.CharacterCombatManager.ActionRunner.PreviewPoiseDamage(PoiseDamage)
                    : Mathf.Max(0f, PoiseDamage))
                : 0f;
            AICharacterCombatManager aiCombatManager =
                character?.CharacterCombatManager as AICharacterCombatManager;
            ProjectedStance = aiCombatManager != null
                ? aiCombatManager.CurrentStance - Mathf.RoundToInt(StanceDamage)
                : 0;
        }

        /// <summary>Applies resolved health damage only on the target's owning peer.</summary>
        protected void ApplyHealthDamage(CharacterManager character, int damage)
        {
            if (character == null || character.IsSpawned && !character.IsOwner)
            {
                return;
            }

            CharacterNetworkManager networkManager =
                character.CharacterNetworkManager;
            if (networkManager == null)
            {
                return;
            }

            float maximumHealth = Mathf.Max(0f, networkManager.MaxHealth.Value);
            float currentHealth = maximumHealth > 0f
                ? Mathf.Clamp(networkManager.CurrentHealth.Value, 0f, maximumHealth)
                : Mathf.Max(0f, networkManager.CurrentHealth.Value);
            float remainingHealth = CalculateProjectedHealth(currentHealth, damage);
            if (damage > 0 &&
                character.CharacterCombatManager is
                    AICharacterCombatManager aiCombatManager)
            {
                aiCombatManager.RecordRuneRewardCandidate(
                    CharacterCausingDamage as PlayerManager);
            }

            networkManager.CurrentHealth.Value = remainingHealth;
        }

        /// <summary>Returns the non-negative projected Health after one resolved hit.</summary>
        public static float CalculateProjectedHealth(float currentHealth, int damage)
        {
            return Mathf.Max(0f, currentHealth - Mathf.Max(0, damage));
        }

        protected static float CalculateAbsorbedDamage(
            float damage,
            float absorption)
        {
            return DamageResolver.ResolveChannel(damage, 0f, absorption);
        }
    }
}
