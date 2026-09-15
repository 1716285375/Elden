namespace ZZ
{
    /// <summary>
    /// Why an action stopped. Systems that clean up after an action (colliders, trails, root motion,
    /// buffer, network state) need this to distinguish a completed swing from one that was cut short.
    /// </summary>
    public enum ActionEndReason
    {
        /// <summary>The clip ran to the end.</summary>
        Completed = 0,

        /// <summary>The player cancelled it into another action.</summary>
        Cancelled = 1,

        /// <summary>The character was staggered out of it.</summary>
        Staggered = 2,

        /// <summary>The character's guard was broken.</summary>
        GuardBroken = 3,

        /// <summary>The attack was parried.</summary>
        Parried = 4,

        /// <summary>The character died mid-action.</summary>
        Death = 5,

        /// <summary>The weapon changed while the action was running.</summary>
        WeaponChanged = 6,

        /// <summary>Any other interruption that is not one of the above.</summary>
        Interrupted = 7
    }
}
