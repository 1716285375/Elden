using UnityEngine;

namespace ZZ
{
    /// <summary>Classifies incoming contacts and turns them into one authored reaction.</summary>
    public static class ReactionResolver
    {
        private const float k_LightStaggerPower = 15f;
        private const float k_MediumStaggerPower = 35f;
        private const float k_HeavyStaggerPower = 60f;
        private const float k_LaunchPower = 90f;

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

        /// <summary>
        /// Turns an attack's reaction profile, the delivered poise and the defender's break state
        /// into one reaction, so no caller has to branch on weapon or boss types.
        /// </summary>
        public static ReactionType ResolveReaction(in ReactionProfile profile, float poiseDamage,
            bool poiseBroken, bool isDead)
        {
            if (isDead)
            {
                return ReactionType.Death;
            }

            if (poiseBroken && profile.PoiseBreakReaction != ReactionType.None)
            {
                return profile.PoiseBreakReaction;
            }

            float power = profile.ReactionPower + Mathf.Max(0f, poiseDamage);
            if (power >= k_LaunchPower)
            {
                return profile.CanLaunch ? ReactionType.Launch : ReactionType.HeavyStagger;
            }

            if (power >= k_HeavyStaggerPower)
            {
                return ReactionType.HeavyStagger;
            }

            if (power >= k_MediumStaggerPower)
            {
                return ReactionType.MediumStagger;
            }

            if (power >= k_LightStaggerPower)
            {
                return ReactionType.LightStagger;
            }

            return power > 0f ? ReactionType.Ping : ReactionType.None;
        }

        /// <summary>Convenience overload for callers that still hold a full damage payload.</summary>
        public static ReactionType ResolveReaction(in ReactionProfile profile, in DamagePayload payload,
            bool poiseBroken, bool isDead)
        {
            return ResolveReaction(profile, payload.Poise, poiseBroken, isDead);
        }
    }
}
