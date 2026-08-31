namespace ZZ
{
    /// <summary>Separates immediate hit presentation from owner-authoritative state writes.</summary>
    public enum DamageProcessingMode
    {
        PredictedPresentation,
        Authoritative,
        ReplicatedPresentation
    }
}
