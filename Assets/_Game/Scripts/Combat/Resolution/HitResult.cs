namespace ZZ
{
    public enum HitOutcome
    {
        Rejected,
        Invulnerable,
        Parried,
        Blocked,
        Damaged
    }

    /// <summary>
    /// The complete settlement of one contact. Feedback systems consume this instead of
    /// recomputing poise, hit stop or camera shake from scratch.
    /// </summary>
    public readonly struct HitResult
    {
        public readonly HitOutcome Outcome;
        public readonly CharacterManager Attacker;
        public readonly CharacterManager Defender;
        public readonly HitIdentity Identity;
        public readonly HitSettlement Settlement;

        public float HealthDamage => Settlement.HealthDamage;
        public float StaminaDamage => Settlement.StaminaDamage;
        public float PoiseDamage => Settlement.PoiseDamage;
        public float StanceDamage => Settlement.StanceDamage;
        public bool PoiseBroken => Settlement.PoiseBroken;
        public bool StanceBroken => Settlement.StanceBroken;
        public bool BleedTriggered => Settlement.BleedTriggered;
        public bool FrostTriggered => Settlement.FrostTriggered;
        public bool PoisonTriggered => Settlement.PoisonTriggered;
        public bool Killed => Settlement.Killed;
        public ReactionType Reaction => Settlement.Reaction;

        public bool WasBlocked => Outcome == HitOutcome.Blocked;
        public bool WasParried => Outcome == HitOutcome.Parried;
        public bool WasAccepted => Outcome >= HitOutcome.Parried;

        public HitResult(HitOutcome outcome, CharacterManager attacker, CharacterManager defender)
        {
            Outcome = outcome;
            Attacker = attacker;
            Defender = defender;
            Identity = default;
            Settlement = HitSettlement.None;
        }

        public HitResult(HitOutcome outcome, CharacterManager attacker, CharacterManager defender,
            in HitIdentity identity, in HitSettlement settlement)
        {
            Outcome = outcome;
            Attacker = attacker;
            Defender = defender;
            Identity = identity;
            Settlement = settlement;
        }
    }
}
