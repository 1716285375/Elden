namespace ZZ
{
    /// <summary>Defines the mutually exclusive contents of the three player inventory entries.</summary>
    public static class ItemInventoryFilter
    {
        public static bool Matches(Item item, InventoryCategory category)
        {
            if (item == null || item is WeaponItem weapon && weapon.IsUnarmed)
            {
                return false;
            }
            return category switch
            {
                InventoryCategory.Weapons => item is WeaponItem,
                InventoryCategory.Armor => item is ArmorItem,
                InventoryCategory.Items => item is not (WeaponItem or ArmorItem),
                _ => false
            };
        }
    }
}
