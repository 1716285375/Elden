namespace ZZ
{
    /// <summary>
    /// Stable network identifiers for the server-authoritative AI state machine.
    /// </summary>
    public enum AICharacterStateId : byte
    {
        Idle = 0,
        PursueTarget = 1,
        CombatStance = 2,
        Attack = 3,
        Dead = 4,
        InvestigateSound = 5
    }
}
