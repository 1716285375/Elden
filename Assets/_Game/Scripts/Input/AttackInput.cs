namespace ZZ
{
    /// <summary>Identifies a buffered attack-button semantic independently of its binding.</summary>
    public enum AttackInputType : byte
    {
        Light = 0,
        Heavy = 1
    }

    /// <summary>Stores one queued attack semantic and the time at which it was pressed.</summary>
    public readonly struct AttackInput
    {
        public AttackInput(AttackInputType inputType, float timestamp)
        {
            InputType = inputType;
            Timestamp = timestamp;
        }

        public AttackInputType InputType { get; }
        public float Timestamp { get; }
    }
}
