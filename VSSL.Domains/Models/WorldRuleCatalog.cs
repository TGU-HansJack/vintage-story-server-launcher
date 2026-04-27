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
            Key = "gameMode", LabelZh = "游戏模式", LabelEn = "Game Mode", Type = WorldRuleType.Choice,
            Choices = ["survival", "creative"], DefaultValue = "survival"
        },
        new() { Key = "playerlives", LabelZh = "玩家生命次数", LabelEn = "Player Lives", Type = WorldRuleType.Text },
        new()
        {
            Key = "creatureHostility", LabelZh = "生物敌对性", LabelEn = "Creature Hostility", Type = WorldRuleType.Choice,
            Choices = ["aggressive", "passive", "off"], DefaultValue = "aggressive"
        },
        new()
        {
            Key = "temporalStorms", LabelZh = "时空风暴频率", LabelEn = "Temporal Storm Frequency", Type = WorldRuleType.Choice,
            Choices = ["off", "veryrare", "rare", "sometimes", "often", "veryoften"], DefaultValue = "off"
        },
        new() { Key = "allowMap", LabelZh = "允许地图", LabelEn = "Allow Map", Type = WorldRuleType.Boolean, DefaultValue = "true" },
        new()
        {
            Key = "allowCoordinateHud", LabelZh = "允许坐标 HUD", LabelEn = "Allow Coordinate HUD", Type = WorldRuleType.Boolean, DefaultValue = "true"
        },
        new()
        {
            Key = "allowLandClaiming", LabelZh = "允许领地声明", LabelEn = "Allow Land Claiming", Type = WorldRuleType.Boolean, DefaultValue = "true"
        },
        new() { Key = "surfaceCopperDeposits", LabelZh = "地表铜矿生成率", LabelEn = "Surface Copper Deposits", Type = WorldRuleType.Text },
        new() { Key = "surfaceTinDeposits", LabelZh = "地表锡矿生成率", LabelEn = "Surface Tin Deposits", Type = WorldRuleType.Text },
        new() { Key = "globalDepositSpawnRate", LabelZh = "矿物总体生成率", LabelEn = "Global Deposit Spawn Rate", Type = WorldRuleType.Text },
        new() { Key = "daysPerMonth", LabelZh = "每月天数", LabelEn = "Days Per Month", Type = WorldRuleType.Number },
        new() { Key = "worldWidth", LabelZh = "世界宽度", LabelEn = "World Width", Type = WorldRuleType.Number, DefaultValue = "1024000" },
        new() { Key = "worldLength", LabelZh = "世界长度", LabelEn = "World Length", Type = WorldRuleType.Number, DefaultValue = "1024000" },
        new()
        {
            Key = "worldEdge", LabelZh = "世界边界类型", LabelEn = "World Edge Type", Type = WorldRuleType.Choice,
            Choices = ["blocked", "traversable"], DefaultValue = "blocked"
        },
        new() { Key = "snowAccum", LabelZh = "积雪堆积", LabelEn = "Snow Accumulation", Type = WorldRuleType.Boolean, DefaultValue = "true" }
    ];
}
