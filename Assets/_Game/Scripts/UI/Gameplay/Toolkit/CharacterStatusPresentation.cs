using UnityEngine.UIElements;

namespace ZZ
{
    /// <summary>Renders a read-only snapshot using the same values consumed by combat and equipment.</summary>
    internal static class CharacterStatusPresentation
    {
        public static void Populate(VisualElement root, PlayerManager player)
        {
            PlayerNetworkManager network = player.PlayerNetworkManager;
            PlayerStatsManager stats = player.PlayerStatsManager;
            root.Q<Label>("status-name").text = WorldSaveGameManager.Instance?.CurrentCharacterData?.CharacterName;
            VisualElement attributes = Reset(root, "status-attributes");
            int level = PlayerUILevelUpManager.CalculateCharacterLevel(network.Vitality.Value, network.Mind.Value,
                network.Endurance.Value, network.Strength.Value, network.Dexterity.Value,
                network.Intelligence.Value, network.Faith.Value, network.Arcane.Value);
            Row(attributes, "等级", level.ToString());
            Row(attributes, "持有卢恩", stats.Runes.ToString("N0"));
            Heading(attributes, "能力值");
            Attributes(attributes, network);
            VisualElement resources = Reset(root, "status-resources");
            Heading(resources, "基础数值");
            Row(resources, "生命值", $"{network.CurrentHealth.Value:0} / {network.MaxHealth.Value:0}");
            Row(resources, "专注值", $"{network.CurrentFocusPoints.Value:0} / {network.MaxFocusPoints.Value:0}");
            Row(resources, "精力", $"{network.CurrentStamina.Value:0} / {network.MaxStamina.Value:0}");
            Row(resources, "强韧度", stats.BasePoiseDefense.ToString("0.#"));
            Heading(resources, "攻击力");
            for (int index = 0; index < 6; index++)
            {
                WeaponItem weapon = player.InventoryManager.GetEquipmentSlotItem((EquipmentSlotType)index) as WeaponItem;
                float power = 0f;
                if (weapon != null)
                {
                    power = weapon.GetChannelAttackPower(DamageType.Physical, player) +
                        weapon.GetChannelAttackPower(DamageType.Magic, player) +
                        weapon.GetChannelAttackPower(DamageType.Fire, player) +
                        weapon.GetChannelAttackPower(DamageType.Lightning, player) +
                        weapon.GetChannelAttackPower(DamageType.Holy, player);
                }
                Row(resources, $"{(index < 3 ? "右手" : "左手")}武器 {index % 3 + 1}", power.ToString("0"));
            }
            VisualElement defense = Reset(root, "status-defense");
            Heading(defense, "防御力 / 减伤率");
            Defense(defense, "物理", DamageType.Physical, stats);
            Defense(defense, "魔力", DamageType.Magic, stats);
            Defense(defense, "火", DamageType.Fire, stats);
            Defense(defense, "雷", DamageType.Lightning, stats);
            Defense(defense, "圣", DamageType.Holy, stats);
            Heading(defense, "防具抵抗力");
            Row(defense, "免疫力", stats.ArmorImmunity.ToString("0"));
            Row(defense, "健壮度", stats.ArmorRobustness.ToString("0"));
            Row(defense, "理智度", stats.ArmorFocus.ToString("0"));
            Row(defense, "抗死度", stats.ArmorVitality.ToString("0"));
        }

        public static void PopulateSummary(VisualElement column, PlayerManager player)
        {
            column.Clear();
            PlayerNetworkManager network = player.PlayerNetworkManager;
            Heading(column, "角色状态");
            Row(column, "等级", PlayerUILevelUpManager.CalculateCharacterLevel(network.Vitality.Value,
                network.Mind.Value, network.Endurance.Value, network.Strength.Value, network.Dexterity.Value,
                network.Intelligence.Value, network.Faith.Value, network.Arcane.Value).ToString());
            Row(column, "持有卢恩", player.PlayerStatsManager.Runes.ToString());
            Heading(column, "能力值");
            Attributes(column, network);
            Heading(column, "基础数值");
            Row(column, "生命值", $"{network.CurrentHealth.Value:0} / {network.MaxHealth.Value:0}");
            Row(column, "专注值", $"{network.CurrentFocusPoints.Value:0} / {network.MaxFocusPoints.Value:0}");
            Row(column, "精力", network.MaxStamina.Value.ToString("0"));
            Row(column, "强韧度", player.PlayerStatsManager.BasePoiseDefense.ToString("0.#"));
        }

        private static void Attributes(VisualElement column, PlayerNetworkManager network)
        {
            Row(column, "生命力", network.Vitality.Value.ToString());
            Row(column, "集中力", network.Mind.Value.ToString());
            Row(column, "耐力", network.Endurance.Value.ToString());
            Row(column, "力量", network.Strength.Value.ToString());
            Row(column, "灵巧", network.Dexterity.Value.ToString());
            Row(column, "智力", network.Intelligence.Value.ToString());
            Row(column, "信仰", network.Faith.Value.ToString());
            Row(column, "感应", network.Arcane.Value.ToString());
        }

        private static VisualElement Reset(VisualElement root, string name)
        {
            VisualElement column = root.Q(name);
            column.Clear();
            return column;
        }

        private static void Heading(VisualElement column, string text)
        {
            var heading = new Label(text);
            heading.AddToClassList("character-status__heading");
            column.Add(heading);
        }

        private static void Defense(VisualElement column, string label, DamageType damageType, PlayerStatsManager stats)
        {
            Row(column, label, $"{stats.GetDefense(damageType):0} / {stats.GetArmorAbsorption(damageType):0.000}");
        }

        private static void Row(VisualElement column, string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("character-status__row");
            row.Add(new Label(label));
            var number = new Label(value);
            number.AddToClassList("character-status__value");
            row.Add(number);
            column.Add(row);
        }
    }
}
