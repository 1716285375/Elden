using UnityEngine;

namespace ZZ
{
    /// <summary>Represents a stackable catalog material consumed by weapon upgrades.</summary>
    [CreateAssetMenu(
        menuName = "Items/Upgrade Material",
        fileName = "New Upgrade Material")]
    public sealed class UpgradeMaterial : Item
    {
        [SerializeField] private UpgradeStone m_upgradeStone;

        /// <summary>Gets this material's weapon-upgrade progression tier.</summary>
        public UpgradeStone UpgradeStone => m_upgradeStone;
    }
}
