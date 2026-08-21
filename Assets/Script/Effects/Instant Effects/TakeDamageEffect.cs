using UnityEngine;

namespace ZZ
{
    [CreateAssetMenu(
        fileName = "Take Damage Effect",
        menuName = "ZZ/Character Effects/Instant/Take Damage")]
    public class TakeDamageEffect : InstantCharacterEffect
    {
        public CharacterManager CharacterCausingDamage { get; private set; }
        public float PhysicalDamage { get; private set; }
        public float MagicDamage { get; private set; }
        public float FireDamage { get; private set; }
        public float LightningDamage { get; private set; }
        public float HolyDamage { get; private set; }
        public int FinalDamageDealt { get; private set; }
        public Vector3 ContactPoint { get; private set; }

        // Reserved for the next damage-resolution phases.
        public bool WasBlocked { get; private set; }
        public bool WasTargetInvulnerable { get; private set; }
        public float PoiseDamage { get; private set; }
        public AnimationClip DamageAnimation { get; private set; }
        public float HitAngle { get; private set; }
        public AudioClip DamageSound { get; private set; }
        public float BleedBuildup { get; private set; }
        public float PoisonBuildup { get; private set; }

        /// <summary>
        /// Creates a transient damage payload without mutating the authored template asset.
        /// </summary>
        public TakeDamageEffect CreateRuntimeDamageEffect(
            CharacterManager characterCausingDamage,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            Vector3 contactPoint,
            float poiseDamage)
        {
            TakeDamageEffect runtimeEffect =
                (TakeDamageEffect)CreateRuntimeInstance();
            runtimeEffect.CharacterCausingDamage = characterCausingDamage;
            runtimeEffect.PhysicalDamage = Mathf.Max(0f, physicalDamage);
            runtimeEffect.MagicDamage = Mathf.Max(0f, magicDamage);
            runtimeEffect.FireDamage = Mathf.Max(0f, fireDamage);
            runtimeEffect.LightningDamage = Mathf.Max(0f, lightningDamage);
            runtimeEffect.HolyDamage = Mathf.Max(0f, holyDamage);
            runtimeEffect.ContactPoint = contactPoint;
            runtimeEffect.PoiseDamage = Mathf.Max(0f, poiseDamage);
            return runtimeEffect;
        }

        /// <summary>
        /// Calculates the combined damage, rounded to an integer with a minimum of one.
        /// </summary>
        public int CalculateDamage()
        {
            float combinedDamage = PhysicalDamage +
                MagicDamage +
                FireDamage +
                LightningDamage +
                HolyDamage;
            FinalDamageDealt = Mathf.Max(1, Mathf.RoundToInt(combinedDamage));
            return FinalDamageDealt;
        }

        /// <inheritdoc />
        public override void ProcessEffect(CharacterManager character)
        {
            if (character == null || !character.IsOwner || character.IsDead)
            {
                return;
            }

            CharacterNetworkManager networkManager = character.CharacterNetworkManager;
            if (networkManager == null)
            {
                return;
            }

            int damage = CalculateDamage();
            float maximumHealth = Mathf.Max(0f, networkManager.MaxHealth.Value);
            float currentHealth = Mathf.Clamp(
                networkManager.CurrentHealth.Value,
                0f,
                maximumHealth);
            float remainingHealth = Mathf.Max(0f, currentHealth - damage);
            networkManager.CurrentHealth.Value = remainingHealth;
        }
    }
}
