namespace ZZ
{
    /// <summary>
    /// Where an action is in its lifecycle. It never derives from a window and no window derives
    /// from it: <c>Active</c> with a closed hit window is a legal state.
    /// </summary>
    public enum CombatActionPhase
    {
        /// <summary>No action is running.</summary>
        None = 0,

        /// <summary>Wind-up: the action is committed but nothing can land yet.</summary>
        Startup = 1,

        /// <summary>The authored active window. Windows may open and close freely inside it.</summary>
        Active = 2,

        /// <summary>Trailing frames after the last active window.</summary>
        Recovery = 3,

        /// <summary>The clip ran to its end. Distinct from <see cref="None"/> so callers can tell "finished" from "never started".</summary>
        Finished = 4
    }
}
