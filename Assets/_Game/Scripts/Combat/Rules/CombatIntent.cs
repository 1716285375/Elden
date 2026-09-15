namespace ZZ
{
    /// <summary>
    /// Continuously held input intents. Unlike <see cref="CombatCommand"/> these are state, not
    /// events, so they never enter the command buffer.
    /// </summary>
    public enum CombatIntent
    {
        Guard = 0,
        Aim = 1,
        Sprint = 2,
        Charge = 3,
        LockOn = 4
    }
}
