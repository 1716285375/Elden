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

            float combinedDamage = CalculateAbsorbedDamage(
                    PhysicalDamage,
                    statsManager.ArmorPhysicalAbsorption) +
                CalculateAbsorbedDamage(MagicDamage, statsManager.ArmorMagicAbsorption) +
                CalculateAbsorbedDamage(FireDamage, statsManager.ArmorFireAbsorption) +
                CalculateAbsorbedDamage(
                    LightningDamage,
                    statsManager.ArmorLightningAbsorption) +
                CalculateAbsorbedDamage(HolyDamage, statsManager.ArmorHolyAbsorption);
            FinalDamageDealt = Mathf.Max(1, Mathf.RoundToInt(combinedDamage));
            return FinalDamageDealt;
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
                ? statsManager.RemainingPoise - Mathf.Max(0f, PoiseDamage)
                : 0f;
            AICharacterCombatManager aiCombatManager =
                character?.CharacterCombatManager as AICharacterCombatManager;
            ProjectedStance = aiCombatManager != null
                ? aiCombatManager.CurrentStance - Mathf.RoundToInt(PoiseDamage)
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
            return Mathf.Max(0f, damage) *
                (1f - Mathf.Clamp(absorption, 0f, 100f) / 100f);
        }
    }
}
