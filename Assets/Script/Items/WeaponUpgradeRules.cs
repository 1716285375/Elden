namespace ZZ
{
    /// <summary>Maps the current weapon level to the next catalog material cost.</summary>
    public static class WeaponUpgradeRules
    {
        public const int MaximumUpgradeLevel = 10;

        /// <summary>Returns the material tier and amount required for the next level.</summary>
        public static bool TryGetUpgradeCost(
            UpgradeLevel currentLevel,
            out UpgradeStone upgradeStone,
            out int requiredAmount)
        {
            switch (currentLevel)
            {
                case UpgradeLevel.Level0:
                    upgradeStone = UpgradeStone.Small;
                    requiredAmount = 1;
                    return true;
                case UpgradeLevel.Level1:
                    upgradeStone = UpgradeStone.Small;
                    requiredAmount = 2;
                    return true;
                case UpgradeLevel.Level2:
                    upgradeStone = UpgradeStone.Small;
                    requiredAmount = 4;
                    return true;
                case UpgradeLevel.Level3:
                    upgradeStone = UpgradeStone.Medium;
                    requiredAmount = 1;
                    return true;
                case UpgradeLevel.Level4:
                    upgradeStone = UpgradeStone.Medium;
                    requiredAmount = 2;
                    return true;
                case UpgradeLevel.Level5:
                    upgradeStone = UpgradeStone.Medium;
                    requiredAmount = 4;
                    return true;
                case UpgradeLevel.Level6:
                    upgradeStone = UpgradeStone.Large;
                    requiredAmount = 1;
                    return true;
                case UpgradeLevel.Level7:
                    upgradeStone = UpgradeStone.Large;
                    requiredAmount = 2;
                    return true;
                case UpgradeLevel.Level8:
                    upgradeStone = UpgradeStone.Large;
                    requiredAmount = 4;
                    return true;
                case UpgradeLevel.Level9:
                    upgradeStone = UpgradeStone.Large;
                    requiredAmount = 6;
                    return true;
                default:
                    upgradeStone = UpgradeStone.Large;
                    requiredAmount = 0;
                    return false;
            }
        }
    }
}
