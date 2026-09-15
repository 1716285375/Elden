using System;

namespace ZZ
{
    [Flags]
    public enum CombatWindowType
    {
        None = 0,
        Hit = 1,
        Combo = 2,
        RollCancel = 4,
        MoveCancel = 8,
        Rotation = 16,
        Parry = 32,
        Parryable = 64,
        Invulnerability = 128,
        WeaponTrail = 256,
        HyperArmor = 512
    }
}
