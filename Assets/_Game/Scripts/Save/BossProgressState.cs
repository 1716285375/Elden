namespace ZZ
{
    /// <summary>Persistent lifecycle state for a uniquely identified boss.</summary>
    public enum BossProgressState : byte
    {
        Dormant = 0,
        Awakened = 1,
        Defeated = 2
    }
}
