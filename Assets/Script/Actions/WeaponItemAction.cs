namespace ZZ
{
    /// <summary>
    /// Compatibility boundary for discrete weapon actions introduced by spell catalysts.
    /// Existing weapon actions may continue to derive directly from WeaponItemBasedAction.
    /// </summary>
    public abstract class WeaponItemAction : WeaponItemBasedAction
    {
    }
}
