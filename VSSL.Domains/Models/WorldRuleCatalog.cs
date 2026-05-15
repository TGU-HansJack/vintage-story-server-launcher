namespace VSSL.Domains.Models;

/// <summary>
///     世界规则目录（V1 子集）
/// </summary>
public static class WorldRuleCatalog
{
    public static IReadOnlyList<WorldRuleDefinition> DefaultRules { get; } =
    [
        new()
        {
            Key = "gameMode",
            LabelZh = "游戏模式",
            LabelEn = "Game Mode",
            Type = WorldRuleType.Choice,
            Choices = ["survival", "creative"],
            ChoiceNames = ["Survival", "Creative"],
            DefaultValue = "survival"
        },
        new()
        {
            Key = "playerlives",
            LabelZh = "玩家生命次数",
            LabelEn = "Player Lives",
            Type = WorldRuleType.Choice,
            Choices = ["1", "2", "3", "4", "5", "10", "20", "-1"],
            ChoiceNames = ["1", "2", "3", "4", "5", "10", "20", "infinite"],
            DefaultValue = "-1"
        },
        new()
        {
            Key = "creatureHostility",
            LabelZh = "生物敌对性",
            LabelEn = "Creature Hostility",
            Type = WorldRuleType.Choice,
            Choices = ["aggressive", "passive", "off"],
            ChoiceNames = ["Aggressive", "Passive", "Never hostile"],
            DefaultValue = "aggressive"
        },
        new()
        {
            Key = "temporalStorms",
            LabelZh = "时空风暴频率",
            LabelEn = "Temporal Storm Frequency",
            Type = WorldRuleType.Choice,
            Choices = ["off", "veryrare", "rare", "sometimes", "often", "veryoften"],
            ChoiceNames =
            [
                "Off",
                "Every 30-40 days, increase strength/frequency by 2.5% each time, capped at +25%",
                "Approx. every 20-30 days, increase strength/frequency by 5% each time, capped at +50%",
                "Approx. every 10-20 days, increase strength/frequency by +10% each time, capped at 100%",
                "Approx. every 5-10 days, increase strength/frequency by 15% each time, capped at +150%",
                "Approx. every 3-6 days, increase strength/frequency by 20% each time, capped at +200%"
            ],
            DefaultValue = "sometimes"
        },
        new() { Key = "allowMap", LabelZh = "允许地图", LabelEn = "Allow Map", Type = WorldRuleType.Boolean, DefaultValue = "true" },
        new()
        {
            Key = "allowCoordinateHud", LabelZh = "允许坐标 HUD", LabelEn = "Allow Coordinate HUD", Type = WorldRuleType.Boolean, DefaultValue = "true"
        },
        new()
        {
            Key = "colorAccurateWorldmap", LabelZh = "彩色地图", LabelEn = "Color Map", Type = WorldRuleType.Boolean, DefaultValue = "false"
        },
        new()
        {
            Key = "allowLandClaiming", LabelZh = "允许领地声明", LabelEn = "Allow Land Claiming", Type = WorldRuleType.Boolean, DefaultValue = "true"
        },
        new()
        {
            Key = "surfaceCopperDeposits",
            LabelZh = "地表铜矿生成率",
            LabelEn = "Surface Copper Deposits",
            Type = WorldRuleType.Choice,
            Choices = ["1", "0.5", "0.2", "0.12", "0.05", "0.015", "0"],
            ChoiceNames = ["Very common", "Common", "Uncommon", "Rare", "Very Rare", "Extremly rare", "Never"],
            DefaultValue = "0.12"
        },
        new()
        {
            Key = "surfaceTinDeposits",
            LabelZh = "地表锡矿生成率",
            LabelEn = "Surface Tin Deposits",
            Type = WorldRuleType.Choice,
            Choices = ["0.5", "0.25", "0.12", "0.03", "0.014", "0.007", "0"],
            ChoiceNames = ["Very common", "Common", "Uncommon", "Rare", "Very Rare", "Extremly rare", "Never"],
            DefaultValue = "0.007"
        },
        new()
        {
            Key = "globalDepositSpawnRate",
            LabelZh = "矿物总体生成率",
            LabelEn = "Global Deposit Spawn Rate",
            Type = WorldRuleType.Choice,
            Choices = ["3", "2", "1.8", "1.6", "1.4", "1.2", "1", "0.8", "0.6", "0.4", "0.2"],
            ChoiceNames = ["300%", "200%", "180%", "160%", "140%", "120%", "100%", "80%", "60%", "40%", "20%"],
            DefaultValue = "1"
        },
        new()
        {
            Key = "daysPerMonth",
            LabelZh = "每月天数",
            LabelEn = "Days Per Month",
            Type = WorldRuleType.Choice,
            Choices = ["30", "20", "12", "9", "6", "3"],
            ChoiceNames =
            [
                "30 days (24 real life hours)",
                "20 days (16 real life hours)",
                "12 days (9.6 real life hours)",
                "9 days (7.2 real life hours)",
                "6 days (4.8 real life hours)",
                "3 days (2.4 real life hours)"
            ],
            DefaultValue = "9"
        },
        new()
        {
            Key = "worldWidth",
            LabelZh = "世界宽度",
            LabelEn = "World Width",
            Type = WorldRuleType.Choice,
            Choices =
            [
                "8192000", "4096000", "2048000", "1024000", "600000", "512000", "384000", "256000", "102400", "51200",
                "25600", "10240", "5120", "1024", "512", "384", "256", "128", "64", "32"
            ],
            ChoiceNames =
            [
                "8 mil blocks", "4 mil blocks", "2 mil blocks", "1 mil blocks", "600k blocks", "512k blocks", "384k blocks",
                "256k blocks", "102k blocks", "51k blocks", "25k blocks", "10k blocks", "5120 blocks", "1024 blocks",
                "512 blocks", "384 blocks", "256 blocks", "128 blocks", "64 blocks", "32 blocks"
            ],
            DefaultValue = "1024000"
        },
        new()
        {
            Key = "worldLength",
            LabelZh = "世界长度",
            LabelEn = "World Length",
            Type = WorldRuleType.Choice,
            Choices =
            [
                "8192000", "4096000", "2048000", "1024000", "600000", "512000", "384000", "256000", "102400", "51200",
                "25600", "10240", "5120", "1024", "512", "384", "256", "128", "64", "32"
            ],
            ChoiceNames =
            [
                "8 mil blocks", "4 mil blocks", "2 mil blocks", "1 mil blocks", "600k blocks", "512k blocks", "384k blocks",
                "256k blocks", "102k blocks", "51k blocks", "25k blocks", "10k blocks", "5120 blocks", "1024 blocks",
                "512 blocks", "384 blocks", "256 blocks", "128 blocks", "64 blocks", "32 blocks"
            ],
            DefaultValue = "1024000"
        },
        new()
        {
            Key = "worldEdge",
            LabelZh = "世界边界类型",
            LabelEn = "World Edge Type",
            Type = WorldRuleType.Choice,
            Choices = ["blocked", "traversable"],
            ChoiceNames = ["Blocked", "Traversable (Can fall down)"],
            DefaultValue = "traversable"
        },
        new() { Key = "snowAccum", LabelZh = "积雪堆积", LabelEn = "Snow Accumulation", Type = WorldRuleType.Boolean, DefaultValue = "true" }
    ];
}
