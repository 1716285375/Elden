namespace ZZ
{
    /// <summary>
    /// The intent a player or AI expresses. Commands are stable; the attack they resolve to is
    /// chosen by the weapon moveset and the currently running attack definition.
    /// </summary>
    public enum CombatCommand
    {
        None = 0,
        LightAttack = 1,
        HeavyAttack = 2,
        Dodge = 3,
        Guard = 4,
        Parry = 5,
        Skill = 6,
        Jump = 7,
        UseItem = 8
    }
}
