using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// The single place where one contact is turned into an outcome. It owns validity, immunity,
    /// parry, guard, damage, reaction and the hit registry; detectors only report who they touched.
    /// </summary>
    public static class CombatResolver
    {
        /// <summary>Used only when a defender has no authored <see cref="GuardProfile"/>.</summary>
        public const float k_FallbackMinimumBlockingDot = 0.3f;

        public static HitResult Resolve(in HitCandidate candidate)
        {
            if (!IsValid(candidate))
            {
                return new HitResult(HitOutcome.Rejected, candidate.Attacker, candidate.Defender);
            }

            CharacterManager defender = candidate.Defender;
            if (defender.IsInvulnerable)
            {
                return new HitResult(HitOutcome.Invulnerable, candidate.Attacker, defender);
            }

            if (TryParry(candidate, out HitResult parried))
            {
                return parried;
            }

            bool blocked = IsBlocked(candidate);
            return ApplyDamage(candidate, blocked);
        }

        /// <summary>Rejects stale, duplicated, unauthorized and non-damageable contacts.</summary>
        public static bool IsValid(in HitCandidate candidate)
        {
            CharacterManager defender = candidate.Defender;
            if (!candidate.IsValid || defender == candidate.Attacker || defender.IsDead)
            {
                return false;
            }

            if (candidate.HurtBox != null && candidate.HurtBox.IsMovementOnly)
            {
                return false;
            }

            if (candidate.HurtBox == null && defender.HasDedicatedHurtBoxes)
            {
                return false;
            }

            if (HasHit(candidate) || !candidate.Detector.CanInitiateDamage(defender))
            {
                return false;
            }

            return candidate.Attacker == null ||
                WorldUtilityManager.CanDamageCharacter(candidate.Attacker, defender);
        }

        public static bool HasHit(in HitCandidate candidate)
        {
            HitRegistry registry = RegistryFor(candidate);
            return registry != null && registry.Contains(candidate.Defender, candidate.HitGroup);
        }

        /// <summary>Runs the shared parry rules, then hands the validated parry to the detector.</summary>
        public static bool TryParry(in HitCandidate candidate, out HitResult result)
        {
            result = default;
            if (!candidate.IsValid || !candidate.Detector.SupportsParry)
            {
                return false;
            }

            CharacterNetworkManager attackerNetwork = candidate.Attacker?.CharacterNetworkManager;
            CharacterNetworkManager defenderNetwork = candidate.Defender?.CharacterNetworkManager;
            if (attackerNetwork == null || defenderNetwork == null ||
                attackerNetwork.IsParryable.Value != true ||
                defenderNetwork.IsParrying.Value != true)
            {
                return false;
            }

            int attackLevel = candidate.Action?.Definition != null
                ? candidate.Action.Definition.ParryLevel
                : 1;
            int parryStrength = candidate.Defender.CharacterCombatManager?.ParryStrength ?? 1;
            if (!ParryRules.CanParry(attackLevel, parryStrength,
                    candidate.Detector.GetGuardFacingDot(candidate.Defender)))
            {
                return false;
            }

            if (!candidate.TryRegisterHit(out int hitIndex))
            {
                return false;
            }

            HitIdentity identity = HitIdentity.For(candidate.Attacker, candidate.Action, candidate.Defender,
                candidate.HitGroup, hitIndex);
            candidate.Detector.NotifyParry(candidate.Defender);
            result = new HitResult(HitOutcome.Parried, candidate.Attacker, candidate.Defender,
                identity,
                new HitSettlement(0f, 0f, 0f, 0f, false, false, false, false, false, false,
                    ReactionType.Critical));
            return true;
        }

        /// <summary>Resolves guard coverage from the defender's authored profile rather than one constant.</summary>
        public static bool IsBlocked(in HitCandidate candidate)
        {
            CharacterNetworkManager defenderNetwork = candidate.Defender?.CharacterNetworkManager;
            if (defenderNetwork == null || !defenderNetwork.IsBlocking.Value)
            {
                return false;
            }

            if (candidate.Action?.Definition != null && !candidate.Action.Definition.IsBlockable)
            {
                return false;
            }

            return candidate.Detector.GetGuardFacingDot(candidate.Defender) >
                ResolveMinimumBlockingDot(candidate.Defender);
        }

        public static float ResolveMinimumBlockingDot(CharacterManager defender)
        {
            GuardProfile profile = defender?.CharacterStatsManager?.CurrentGuardProfile;
            return profile != null ? profile.MinimumBlockingDot : k_FallbackMinimumBlockingDot;
        }

        /// <summary>Registers the contact once and routes the payload through the damage authority.</summary>
        public static HitResult ApplyDamage(in HitCandidate candidate, bool wasBlocked)
        {
            if (!candidate.TryRegisterHit(out int hitIndex))
            {
                return new HitResult(HitOutcome.Rejected, candidate.Attacker, candidate.Defender);
            }

            // Damage and parry callbacks may interrupt or replace the attack synchronously.
            HitIdentity identity = HitIdentity.For(candidate.Attacker, candidate.Action, candidate.Defender,
                candidate.HitGroup, hitIndex);
            ReactionProfile profile = candidate.Action?.Definition != null
                ? candidate.Action.Definition.Reaction
                : ReactionProfile.Default;
            DamagePayload payload = candidate.CreatePayload();
            // Poise is tracked as a non-positive remainder; a break resets it to zero.
            float poiseBefore = candidate.Defender.CharacterStatsManager?.TotalPoiseDamage ?? 0f;
            candidate.Detector.DeliverDamage(candidate.Defender, payload, candidate.ContactPoint, wasBlocked);
            float poiseAfter = candidate.Defender.CharacterStatsManager?.TotalPoiseDamage ?? 0f;
            bool poiseBroken = payload.Poise > 0f && poiseBefore < 0f && poiseAfter >= 0f;
            ReactionType reaction = ReactionResolver.ResolveReaction(profile, payload, poiseBroken,
                candidate.Defender.IsDead);
            HitResult result = new HitResult(wasBlocked ? HitOutcome.Blocked : HitOutcome.Damaged,
                candidate.Attacker, candidate.Defender,
                identity,
                new HitSettlement(
                    payload.Physical + payload.Magic + payload.Fire + payload.Lightning + payload.Holy,
                    0f, payload.Poise, payload.Stance, poiseBroken, false, false, false, false,
                    candidate.Defender.IsDead, reaction));
            return result;
        }

        private static HitRegistry RegistryFor(in HitCandidate candidate)
        {
            return candidate.Action != null ? candidate.Action.Hits : candidate.Detector?.Registry;
        }
    }
}
