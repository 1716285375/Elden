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
        public int FinalDamageDealt { get; protected set; }
        public Vector3 ContactPoint { get; private set; }

        public bool WasBlocked { get; protected set; }
        public bool WasTargetInvulnerable { get; protected set; }
        public float PoiseDamage { get; private set; }
        public AnimationClip DamageAnimation { get; private set; }
        public float HitAngle { get; private set; }
        public bool IsPoiseBroken { get; private set; }

        // Reserved for the next damage-resolution phases.
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
            runtimeEffect.ConfigureRuntimeDamage(
                characterCausingDamage,
                physicalDamage,
                magicDamage,
                fireDamage,
                lightningDamage,
                holyDamage,
                contactPoint,
                poiseDamage);
            return runtimeEffect;
        }

        /// <summary>Assigns sanitized hit data to a transient derived damage effect.</summary>
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

        /// <summary>Calculates incoming damage after applying armor-only absorption.</summary>
        public int CalculateDamage(CharacterStatsManager statsManager)
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

            Vector3 hitDirection = CharacterCausingDamage != null
                ? (character.transform.position - CharacterCausingDamage.transform.position).normalized
                : Vector3.zero;
            character.CharacterEffectsManager?.PlayBloodSplatterVFX(
                ContactPoint,
                hitDirection);
            character.CharacterSoundFXManager?.PlayDamageGrunt();
            IsPoiseBroken = character.CharacterStatsManager == null ||
                character.CharacterStatsManager.ApplyPoiseDamage(PoiseDamage);
            character.CharacterCombatManager?.RecordPoiseDamageTaken(
                PoiseDamage);
            PlayDirectionalBasedDamageAnimation(character, IsPoiseBroken);
            CalculateStanceDamage(character);

            ApplyHealthDamage(character, CalculateDamage(character.CharacterStatsManager));
        }

        /// <summary>Applies resolved damage only on the target's owning peer.</summary>
        protected void ApplyHealthDamage(CharacterManager character, int damage)
        {
            if (character == null || !character.IsOwner)
            {
                return;
            }

            CharacterNetworkManager networkManager = character.CharacterNetworkManager;
            if (networkManager == null)
            {
                return;
            }

            float maximumHealth = Mathf.Max(0f, networkManager.MaxHealth.Value);
            float currentHealth = Mathf.Clamp(
                networkManager.CurrentHealth.Value,
                0f,
                maximumHealth);
            float remainingHealth = Mathf.Max(
                0f,
                currentHealth - Mathf.Max(0, damage));
            networkManager.CurrentHealth.Value = remainingHealth;
        }

        /// <summary>
        /// Resolves the incoming hit side and plays an appropriate damage reaction.
        /// </summary>
        public void PlayDirectionalBasedDamageAnimation(CharacterManager character)
        {
            PlayDirectionalBasedDamageAnimation(character, true);
        }

        /// <summary>
        /// Plays a full reaction on Poise break, or a non-locking Ping reaction otherwise.
        /// </summary>
        public void PlayDirectionalBasedDamageAnimation(
            CharacterManager character,
            bool isPoiseBroken)
        {
            if (character == null ||
                CharacterCausingDamage == null ||
                character.CharacterAnimatorManager == null)
            {
                return;
            }

            HitAngle = CalculateHitAngle(
                CharacterCausingDamage.transform,
                character.transform);
            DamageDirection damageDirection = GetDamageDirection(HitAngle);
            DamageAnimation = isPoiseBroken
                ? character.CharacterAnimatorManager.PlayDirectionalDamageAnimation(
                    damageDirection)
                : character.CharacterAnimatorManager.PlayDirectionalPingDamageAnimation(
                    damageDirection);
        }

        /// <summary>
        /// Calculates the signed horizontal incoming angle used by directional reactions.
        /// </summary>
        public static float CalculateHitAngle(Transform attacker, Transform target)
        {
            if (attacker == null || target == null)
            {
                return 0f;
            }

            Vector3 targetForward = Vector3.ProjectOnPlane(
                target.forward,
                Vector3.up);
            Vector3 incomingDirection = Vector3.ProjectOnPlane(
                target.position - attacker.position,
                Vector3.up);
            if (targetForward.sqrMagnitude <= Mathf.Epsilon ||
                incomingDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return 0f;
            }

            return Vector3.SignedAngle(
                targetForward.normalized,
                incomingDirection.normalized,
                Vector3.up);
        }

        /// <summary>
        /// Classifies a signed incoming angle into the struck side of the target.
        /// </summary>
        public static DamageDirection GetDamageDirection(float hitAngle)
        {
            float normalizedAngle = Mathf.DeltaAngle(0f, hitAngle);
            if (normalizedAngle >= -45f && normalizedAngle <= 45f)
            {
                return DamageDirection.Back;
            }

            if (normalizedAngle > 45f && normalizedAngle < 145f)
            {
                return DamageDirection.Left;
            }

            if (normalizedAngle < -45f && normalizedAngle > -145f)
            {
                return DamageDirection.Right;
            }

            return DamageDirection.Front;
        }

        private static float CalculateAbsorbedDamage(float damage, float absorption)
        {
            return Mathf.Max(0f, damage) *
                (1f - Mathf.Clamp(absorption, 0f, 100f) / 100f);
        }

        private void CalculateStanceDamage(CharacterManager character)
        {
            if (character is not AICharacterManager aiCharacter ||
                PoiseDamage <= 0f)
            {
                return;
            }

            AICharacterCombatManager combatManager =
                aiCharacter.GetComponent<AICharacterCombatManager>();
            combatManager?.DamageStance(Mathf.RoundToInt(PoiseDamage));
        }
    }
}
