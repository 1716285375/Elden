using UnityEngine;

namespace ZZ
{
    /// <summary>Applies damage-type absorption and feedback for one valid block.</summary>
    [CreateAssetMenu(
        fileName = "Take Blocked Damage Effect",
        menuName = "ZZ/Character Effects/Instant/Take Blocked Damage")]
    public class TakeBlockedDamageEffect : TakeDamageEffect
    {
        [SerializeField] private AudioClip[] m_blockSounds =
            System.Array.Empty<AudioClip>();

        public float BlockingPhysicalAbsorption { get; private set; }
        public float BlockingMagicAbsorption { get; private set; }
        public float BlockingFireAbsorption { get; private set; }
        public float BlockingLightningAbsorption { get; private set; }
        public float BlockingHolyAbsorption { get; private set; }
        public float BlockingStability { get; private set; }
        public float StaminaDamage { get; private set; }
        public DamageIntensity DamageIntensity { get; private set; }

        /// <summary>Creates a transient blocked-hit payload from the shared template.</summary>
        public TakeBlockedDamageEffect CreateRuntimeBlockedDamageEffect(
            CharacterManager characterCausingDamage,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            Vector3 contactPoint,
            float poiseDamage,
            float blockingPhysicalAbsorption,
            float blockingMagicAbsorption,
            float blockingFireAbsorption,
            float blockingLightningAbsorption,
            float blockingHolyAbsorption,
            float blockingStability)
        {
            TakeBlockedDamageEffect runtimeEffect =
                (TakeBlockedDamageEffect)CreateRuntimeInstance();
            runtimeEffect.ConfigureRuntimeDamage(
                characterCausingDamage,
                physicalDamage,
                magicDamage,
                fireDamage,
                lightningDamage,
                holyDamage,
                contactPoint,
                poiseDamage);
            runtimeEffect.BlockingPhysicalAbsorption = ClampAbsorption(
                blockingPhysicalAbsorption);
            runtimeEffect.BlockingMagicAbsorption = ClampAbsorption(
                blockingMagicAbsorption);
            runtimeEffect.BlockingFireAbsorption = ClampAbsorption(
                blockingFireAbsorption);
            runtimeEffect.BlockingLightningAbsorption = ClampAbsorption(
                blockingLightningAbsorption);
            runtimeEffect.BlockingHolyAbsorption = ClampAbsorption(
                blockingHolyAbsorption);
            runtimeEffect.BlockingStability = ClampAbsorption(blockingStability);
            runtimeEffect.StaminaDamage =
                CharacterStatsManager.CalculateBlockingStaminaDamage(
                    runtimeEffect.PoiseDamage,
                    runtimeEffect.BlockingStability);
            runtimeEffect.WasBlocked = true;
            runtimeEffect.DamageIntensity =
                WorldUtilityManager.GetDamageIntensityBasedOnPoiseDamage(
                    runtimeEffect.PoiseDamage);
            return runtimeEffect;
        }

        /// <summary>Calculates total damage after applying each damage-type absorption.</summary>
        public int CalculateBlockedDamage()
        {
            float combinedDamage = CalculateAbsorbedDamage(
                    PhysicalDamage,
                    BlockingPhysicalAbsorption) +
                CalculateAbsorbedDamage(MagicDamage, BlockingMagicAbsorption) +
                CalculateAbsorbedDamage(FireDamage, BlockingFireAbsorption) +
                CalculateAbsorbedDamage(
                    LightningDamage,
                    BlockingLightningAbsorption) +
                CalculateAbsorbedDamage(HolyDamage, BlockingHolyAbsorption);
            FinalDamageDealt = Mathf.Max(0, Mathf.RoundToInt(combinedDamage));
            return FinalDamageDealt;
        }

        /// <summary>Classifies a hit using the EP55 poise-damage thresholds.</summary>
        public static DamageIntensity GetDamageIntensityBasedOnPoiseDamage(
            float poiseDamage)
        {
            return WorldUtilityManager.GetDamageIntensityBasedOnPoiseDamage(
                poiseDamage);
        }

        /// <inheritdoc />
        public override void ProcessDamage(
            CharacterManager character,
            DamageProcessingMode processingMode)
        {
            if (character == null || character.IsDead)
            {
                return;
            }

            if (character.IsInvulnerable)
            {
                WasTargetInvulnerable = true;
                return;
            }

            WasBlocked = true;
            DamageIntensity =
                WorldUtilityManager.GetDamageIntensityBasedOnPoiseDamage(
                    PoiseDamage);
            int resolvedDamage = CalculateBlockedDamage();
            UpdateProjectedState(character, resolvedDamage);
            if (ProjectedHealth <= 0f)
            {
                character.SetPredictedDead(true);
            }

            character.CharacterSoundFXManager?.PlayBlockingSoundEffect();
            if (processingMode == DamageProcessingMode.Authoritative)
            {
                ApplyHealthDamage(character, resolvedDamage);
                character.CharacterStatsManager?.ApplyBlockingStaminaDamage(
                    PoiseDamage,
                    BlockingStability);
            }
        }

        private static float ClampAbsorption(float absorption)
        {
            return Mathf.Clamp(absorption, 0f, 100f);
        }
    }
}
