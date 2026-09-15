namespace ZZ
{
    /// <summary>The numeric outcome of a settled hit, independent of who dealt it.</summary>
    public readonly struct HitSettlement
    {
        public readonly float HealthDamage;
        public readonly float StaminaDamage;
        public readonly float PoiseDamage;
        public readonly float StanceDamage;
        public readonly bool PoiseBroken;
        public readonly bool StanceBroken;
        public readonly bool BleedTriggered;
        public readonly bool FrostTriggered;
        public readonly bool PoisonTriggered;
        public readonly bool Killed;
        public readonly ReactionType Reaction;

        public HitSettlement(float healthDamage, float staminaDamage, float poiseDamage,
            float stanceDamage, bool poiseBroken, bool stanceBroken, bool bleedTriggered,
            bool frostTriggered, bool poisonTriggered, bool killed, ReactionType reaction)
        {
            HealthDamage = healthDamage;
            StaminaDamage = staminaDamage;
            PoiseDamage = poiseDamage;
            StanceDamage = stanceDamage;
            PoiseBroken = poiseBroken;
            StanceBroken = stanceBroken;
            BleedTriggered = bleedTriggered;
            FrostTriggered = frostTriggered;
            PoisonTriggered = poisonTriggered;
            Killed = killed;
            Reaction = reaction;
        }

        public static HitSettlement None => new HitSettlement(0f, 0f, 0f, 0f, false, false,
            false, false, false, false, ReactionType.None);
    }
}
