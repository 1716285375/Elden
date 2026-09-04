namespace ZZ
{
    /// <summary>Loads portrait armor models while suppressing Stats and effect mutations.</summary>
    public sealed class ProfileIconMakerEquipmentManager : PlayerEquipmentManager
    {
        protected override void ApplyArmorGameplayEffect(ArmorItem armorItem)
        {
        }

        protected override void RemoveArmorGameplayEffect(ArmorItem armorItem)
        {
        }

        protected override void RecalculateArmorValues()
        {
        }
    }
}
