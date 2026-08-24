namespace ZZ
{
    /// <summary>
    /// Stable identifiers for character action animations replicated over the network.
    /// </summary>
    public enum CharacterActionAnimation : byte
    {
        RollForward = 0,
        BackStep = 1,
        Death = 2,
        PassThroughFog = 3,
        RestAtSiteOfGrace = 4,
        GuardBreak = 5,
        StanceBreak = 6,
        Riposte = 7,
        Riposted = 8,
        Backstab = 9,
        Backstabbed = 10,
        ParryFast = 11,
        ParryMedium = 12,
        ParrySlow = 13,
        ParryLand = 14,
        Parried = 15,
        PickupItem = 16,
        ChargeSpellRight = 17,
        ChargeSpellLeft = 18,
        ReleaseSpellRight = 19,
        ReleaseSpellLeft = 20,
        ReleaseFullChargeSpellRight = 21,
        ReleaseFullChargeSpellLeft = 22
    }
}
