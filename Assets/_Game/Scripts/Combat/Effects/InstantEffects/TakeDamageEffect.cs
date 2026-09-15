using UnityEngine;

namespace ZZ
{
    [GameAsset(
        FileName = "Take Damage Effect",
        MenuName = "ZZ/Character Effects/Instant/Take Damage")]
    public class TakeDamageEffect : DamageEffect
    {
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

            if (character is PlayerManager damagedPlayer)
            {
                damagedPlayer.PlayerCombatManager?.ClearGuardCounter();
            }

            int resolvedDamage = CalculateDamage(
                character.CharacterStatsManager);
            UpdateProjectedState(character, resolvedDamage);
            if (ProjectedHealth <= 0f)
            {
                character.SetPredictedDead(true);
            }

            Vector3 hitDirection = CharacterCausingDamage != null
                ? (character.transform.position - CharacterCausingDamage.transform.position).normalized
                : Vector3.zero;
            character.CharacterEffectsManager?.PlayBloodSplatterVFX(
                ContactPoint,
                hitDirection);
            character.CharacterSoundFXManager?.PlayDamageGrunt();
            PresentImpactFeedback(character, HitOutcome.Damaged, PoiseDamage);
            IsPoiseBroken = ProjectedPoise <= 0f;
            if (processingMode == DamageProcessingMode.Authoritative)
            {
                IsPoiseBroken = character.CharacterStatsManager == null ||
                    character.CharacterStatsManager.ApplyPoiseDamage(PoiseDamage);
                character.CharacterCombatManager?.RecordPoiseDamageTaken(
                    PoiseDamage);
                CalculateStanceDamage(character);
                ApplyHealthDamage(character, resolvedDamage);
            }

            if (ProjectedHealth <= 0f)
            {
                return;
            }

            bool handledByLadder = character is PlayerManager player &&
                player.LocomotionManager?.RegisterLadderHit() == true;
            if (handledByLadder)
            {
                return;
            }

            AICharacterCombatManager aiCombatManager =
                character.CharacterCombatManager as AICharacterCombatManager;
            if (processingMode != DamageProcessingMode.Authoritative &&
                aiCombatManager?.WouldBreakStance(
                    Mathf.RoundToInt(StanceDamage)) == true)
            {
                character.CharacterAnimatorManager
                    ?.PlayLocalAnimationInstantly(
                        CharacterActionAnimation.StanceBreak,
                        true);
                return;
            }

            PlayDirectionalBasedDamageAnimation(character, IsPoiseBroken);
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
            return ReactionResolver.CalculateHitAngle(attacker, target);
        }

        public static DamageDirection GetDamageDirection(float hitAngle)
        {
            return ReactionResolver.GetDamageDirection(hitAngle);
        }

        private void CalculateStanceDamage(CharacterManager character)
        {
            if (character is not AICharacterManager aiCharacter ||
                StanceDamage <= 0f)
            {
                return;
            }

            AICharacterCombatManager combatManager =
                aiCharacter.GetComponent<AICharacterCombatManager>();
            combatManager?.DamageStance(Mathf.RoundToInt(StanceDamage));
        }
    }
}
