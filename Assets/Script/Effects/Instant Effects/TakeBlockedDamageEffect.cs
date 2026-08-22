using UnityEngine;

namespace ZZ
{
    /// <summary>Applies damage-type absorption and feedback for one valid block.</summary>
    [CreateAssetMenu(
        fileName = "Take Blocked Damage Effect",
        menuName = "ZZ/Character Effects/Instant/Take Blocked Damage")]
    public class TakeBlockedDamageEffect : TakeDamageEffect
    {
        private const float k_LightPoiseThreshold = 10f;
        private const float k_MediumPoiseThreshold = 30f;
        private const float k_HeavyPoiseThreshold = 70f;
        private const float k_ColossalPoiseThreshold = 120f;

        [SerializeField] private AudioClip[] m_blockSounds =
            System.Array.Empty<AudioClip>();

        public float BlockingPhysicalAbsorption { get; private set; }
        public float BlockingMagicAbsorption { get; private set; }
        public float BlockingFireAbsorption { get; private set; }
        public float BlockingLightningAbsorption { get; private set; }
        public float BlockingHolyAbsorption { get; private set; }
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
            float blockingHolyAbsorption)
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
            runtimeEffect.WasBlocked = true;
            runtimeEffect.DamageIntensity =
                GetDamageIntensityBasedOnPoiseDamage(runtimeEffect.PoiseDamage);
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
            if (poiseDamage >= k_ColossalPoiseThreshold)
            {
                return DamageIntensity.Colossal;
            }

            if (poiseDamage >= k_HeavyPoiseThreshold)
            {
                return DamageIntensity.Heavy;
            }

            if (poiseDamage >= k_MediumPoiseThreshold)
            {
                return DamageIntensity.Medium;
            }

            return poiseDamage >= k_LightPoiseThreshold
                ? DamageIntensity.Light
                : DamageIntensity.Ping;
        }

        /// <inheritdoc />
        public override void ProcessEffect(CharacterManager character)
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
            DamageIntensity = GetDamageIntensityBasedOnPoiseDamage(PoiseDamage);
            character.CharacterSoundFXManager?.PlayBlockSound(
                GetBlockSound(DamageIntensity));
            ApplyHealthDamage(character, CalculateBlockedDamage());
        }

        private AudioClip GetBlockSound(DamageIntensity damageIntensity)
        {
            int soundIndex = (int)damageIntensity;
            return soundIndex >= 0 && soundIndex < m_blockSounds.Length
                ? m_blockSounds[soundIndex]
                : null;
        }

        private static float CalculateAbsorbedDamage(
            float damage,
            float absorption)
        {
            return Mathf.Max(0f, damage) *
                (1f - ClampAbsorption(absorption) / 100f);
        }

        private static float ClampAbsorption(float absorption)
        {
            return Mathf.Clamp(absorption, 0f, 100f);
        }
    }
}
