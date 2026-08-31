namespace ZZ
{
    /// <summary>Reuses player modular-body presentation without gameplay dependencies.</summary>
    public sealed class ProfileIconMakerBodyManager : PlayerBodyManager
    {
        /// <summary>Applies the saved body type to the portrait dummy.</summary>
        public void ChangeSex(bool isMale)
        {
            ToggleBodyType(isMale);
        }
    }
}
