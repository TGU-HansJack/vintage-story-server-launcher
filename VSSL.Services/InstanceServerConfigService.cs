using System.Text.Json;
using System.Text.Json.Nodes;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     实例服务器配置服务默认实现
/// </summary>
public class InstanceServerConfigService : IInstanceServerConfigService
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };

    /// <inheritdoc />
    public async Task<ServerCommonSettings> LoadServerSettingsAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(profile, cancellationToken);
        return new ServerCommonSettings
        {
            ServerName = ReadString(root["ServerName"], "Vintage Story Server"),
            Ip = ReadNullableString(root["Ip"]),
            Port = ReadInt(root["Port"], 42420),
            MaxClients = ReadInt(root["MaxClients"], 16),
            Password = ReadNullableString(root["Password"]),
            AdvertiseServer = ReadBool(root["AdvertiseServer"], false),
            WhitelistMode = ReadInt(root["WhitelistMode"], 0),
            AllowPvP = ReadBool(root["AllowPvP"], true),
            AllowFireSpread = ReadBool(root["AllowFireSpread"], true),
            AllowFallingBlocks = ReadBool(root["AllowFallingBlocks"], true)
        };
    }

    /// <inheritdoc />
    public async Task<WorldSettings> LoadWorldSettingsAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(profile, cancellationToken);
        var worldConfig = GetOrCreateObject(root, "WorldConfig");
        var worldRules = GetOrCreateObject(worldConfig, "WorldConfiguration");

        var mapSizeY = ReadNullableInt(worldConfig["MapSizeY"]);
        if (mapSizeY is null)
            mapSizeY = ReadNullableInt(worldRules["worldHeight"]);

        var defaultSaveFile = string.IsNullOrWhiteSpace(profile.ActiveSaveFile)
            ? WorkspacePathHelper.GetProfileDefaultSaveFile(profile.Id)
            : profile.ActiveSaveFile;

        return new WorldSettings
        {
            Seed = ReadString(worldConfig["Seed"], "123456789"),
            WorldName = ReadString(worldConfig["WorldName"], "A new world"),
            SaveFileLocation = ReadString(worldConfig["SaveFileLocation"], defaultSaveFile),
            PlayStyle = ReadString(worldConfig["PlayStyle"], "surviveandbuild"),
            WorldType = ReadString(worldConfig["WorldType"], "standard"),
            WorldHeight = mapSizeY ?? 256
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorldRuleValue>> LoadWorldRulesAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(profile, cancellationToken);
        var worldConfig = GetOrCreateObject(root, "WorldConfig");
        var worldRules = GetOrCreateObject(worldConfig, "WorldConfiguration");

        return WorldRuleCatalog.DefaultRules
            .Select(rule => new WorldRuleValue
            {
                Definition = rule,
                Value = ReadFlexibleString(worldRules[rule.Key])
                        ?? ReadRuleFallbackValue(rule.Key, root, worldConfig)
                        ?? rule.DefaultValue
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task SaveSettingsAsync(
        InstanceProfile profile,
        ServerCommonSettings serverSettings,
        WorldSettings worldSettings,
        IReadOnlyList<WorldRuleValue> rules,
        CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(profile, cancellationToken);

        root["ServerName"] = serverSettings.ServerName;
        root["Ip"] = string.IsNullOrWhiteSpace(serverSettings.Ip) ? null : serverSettings.Ip;
        root["Port"] = serverSettings.Port;
        root["MaxClients"] = serverSettings.MaxClients;
        root["Password"] = string.IsNullOrWhiteSpace(serverSettings.Password) ? null : serverSettings.Password;
        root["AdvertiseServer"] = serverSettings.AdvertiseServer;
        root["WhitelistMode"] = serverSettings.WhitelistMode;
        root["AllowPvP"] = serverSettings.AllowPvP;
        root["AllowFireSpread"] = serverSettings.AllowFireSpread;
        root["AllowFallingBlocks"] = serverSettings.AllowFallingBlocks;

        var worldConfig = GetOrCreateObject(root, "WorldConfig");
        worldConfig["Seed"] = worldSettings.Seed;
        worldConfig["WorldName"] = worldSettings.WorldName;
        worldConfig["SaveFileLocation"] = worldSettings.SaveFileLocation;
        worldConfig["PlayStyle"] = worldSettings.PlayStyle;
        worldConfig["WorldType"] = worldSettings.WorldType;
        worldConfig["MapSizeY"] = worldSettings.WorldHeight;

        var worldRules = GetOrCreateObject(worldConfig, "WorldConfiguration");
        if (worldSettings.WorldHeight.HasValue)
            worldRules["worldHeight"] = worldSettings.WorldHeight.Value;

        foreach (var rule in rules)
        {
            var normalizedValue = rule.Value?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
                continue;

            if (string.Equals(rule.Definition.Key, "worldWidth", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(normalizedValue, out var worldWidth))
            {
                root["MapSizeX"] = worldWidth;
                worldRules[rule.Definition.Key] = worldWidth;
                continue;
            }

            if (string.Equals(rule.Definition.Key, "worldLength", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(normalizedValue, out var worldLength))
            {
                root["MapSizeZ"] = worldLength;
                worldRules[rule.Definition.Key] = worldLength;
                continue;
            }

            if (rule.Definition.Type == WorldRuleType.Boolean &&
                bool.TryParse(normalizedValue, out var boolValue))
            {
                worldRules[rule.Definition.Key] = boolValue;
            }
            else if (rule.Definition.Type == WorldRuleType.Number &&
                     int.TryParse(normalizedValue, out var intValue))
            {
                worldRules[rule.Definition.Key] = intValue;
            }
            else
            {
                worldRules[rule.Definition.Key] = normalizedValue;
            }
        }

        await SaveRootAsync(profile, root, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> LoadRawJsonAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(profile, cancellationToken);
        return root.ToJsonString(JsonWriteOptions);
    }

    /// <inheritdoc />
    public async Task SaveRawJsonAsync(
        InstanceProfile profile,
        string json,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("JSON 内容为空。");

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"JSON 语法错误：{ex.Message}", ex);
        }

        if (node is not JsonObject root)
            throw new InvalidOperationException("配置根节点必须是 JSON 对象。");
        if (root["WorldConfig"] is not JsonObject)
            throw new InvalidOperationException("配置必须包含 WorldConfig 对象。");

        await SaveRootAsync(profile, root, cancellationToken);
    }

    private static async Task<JsonObject> LoadRootAsync(InstanceProfile profile, CancellationToken cancellationToken)
    {
        var configPath = WorkspacePathHelper.GetProfileConfigPath(profile.DirectoryPath);
        var configDirectory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(configDirectory))
            Directory.CreateDirectory(configDirectory);

        TryEnsureGeneratedConfig(profile, forceRegenerate: false);

        if (!File.Exists(configPath))
        {
            var defaultRoot = BuildDefaultRoot(profile);
            await File.WriteAllTextAsync(configPath, defaultRoot.ToJsonString(JsonWriteOptions), cancellationToken);
            return defaultRoot;
        }

        var parsedRoot = await TryParseRootAsync(configPath, cancellationToken);
        if (parsedRoot is not null) return parsedRoot;

        // 文件损坏时强制重新生成一份完整配置，避免启动时出现默认组缺失导致秒退。
        TryEnsureGeneratedConfig(profile, forceRegenerate: true);
        parsedRoot = await TryParseRootAsync(configPath, cancellationToken);
        if (parsedRoot is not null) return parsedRoot;

        var fallbackRoot = BuildDefaultRoot(profile);
        await File.WriteAllTextAsync(configPath, fallbackRoot.ToJsonString(JsonWriteOptions), cancellationToken);
        return fallbackRoot;
    }

    private static async Task<JsonObject?> TryParseRootAsync(string configPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var node = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
            return node as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static void TryEnsureGeneratedConfig(InstanceProfile profile, bool forceRegenerate)
    {
        try
        {
            var installPath = WorkspacePathHelper.GetServerInstallPath(profile.Version);
            var serverExe = Path.Combine(installPath, "VintagestoryServer.exe");
            if (!File.Exists(serverExe)) return;

            ServerConfigBootstrapper.EnsureGenerated(installPath, profile.DirectoryPath, forceRegenerate);
        }
        catch
        {
            // 保持配置页面可用，生成失败时回落到现有逻辑。
        }
    }

    private static async Task SaveRootAsync(InstanceProfile profile, JsonObject root, CancellationToken cancellationToken)
    {
        var configPath = WorkspacePathHelper.GetProfileConfigPath(profile.DirectoryPath);
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(configPath, root.ToJsonString(JsonWriteOptions), cancellationToken);
    }

    private static JsonObject BuildDefaultRoot(InstanceProfile profile)
    {
        var defaultSaveFile = string.IsNullOrWhiteSpace(profile.ActiveSaveFile)
            ? WorkspacePathHelper.GetProfileDefaultSaveFile(profile.Id)
            : profile.ActiveSaveFile;

        var root = new JsonObject
        {
            ["ServerName"] = "Vintage Story Server",
            ["Ip"] = null,
            ["Port"] = 42420,
            ["MaxClients"] = 16,
            ["Password"] = null,
            ["AdvertiseServer"] = false,
            ["WhitelistMode"] = 0,
            ["AllowPvP"] = true,
            ["AllowFireSpread"] = true,
            ["AllowFallingBlocks"] = true
        };

        var worldConfig = new JsonObject
        {
            ["Seed"] = "123456789",
            ["WorldName"] = "A new world",
            ["SaveFileLocation"] = defaultSaveFile,
            ["PlayStyle"] = "surviveandbuild",
            ["WorldType"] = "standard",
            ["MapSizeY"] = 256
        };
        worldConfig["WorldConfiguration"] = new JsonObject
        {
            ["gameMode"] = "survival",
            ["allowMap"] = true,
            ["allowCoordinateHud"] = true,
            ["allowLandClaiming"] = true,
            ["worldWidth"] = 1024000,
            ["worldLength"] = 1024000,
            ["worldEdge"] = "blocked",
            ["snowAccum"] = true
        };
        root["WorldConfig"] = worldConfig;
        return root;
    }

    private static JsonObject GetOrCreateObject(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonObject obj)
            return obj;

        var created = new JsonObject();
        root[propertyName] = created;
        return created;
    }

    private static string ReadString(JsonNode? node, string defaultValue)
    {
        return node?.GetValue<string>() ?? defaultValue;
    }

    private static string? ReadNullableString(JsonNode? node)
    {
        return node is null ? null : node.GetValue<string?>();
    }

    private static int ReadInt(JsonNode? node, int defaultValue)
    {
        if (node is null) return defaultValue;

        if (node.GetValueKind() == JsonValueKind.Number &&
            node is JsonValue numericValue &&
            numericValue.TryGetValue<int>(out var value))
            return value;

        if (node.GetValueKind() == JsonValueKind.String &&
            int.TryParse(node.GetValue<string>(), out value))
            return value;

        return defaultValue;
    }

    private static int? ReadNullableInt(JsonNode? node)
    {
        if (node is null) return null;

        if (node.GetValueKind() == JsonValueKind.Number &&
            node is JsonValue numericValue &&
            numericValue.TryGetValue<int>(out var numeric))
            return numeric;

        if (node.GetValueKind() == JsonValueKind.String &&
            int.TryParse(node.GetValue<string>(), out numeric))
            return numeric;

        return null;
    }

    private static bool ReadBool(JsonNode? node, bool defaultValue)
    {
        if (node is null) return defaultValue;

        if (node.GetValueKind() == JsonValueKind.True || node.GetValueKind() == JsonValueKind.False)
            return node.GetValue<bool>();

        if (node.GetValueKind() == JsonValueKind.String &&
            bool.TryParse(node.GetValue<string>(), out var parsed))
            return parsed;

        return defaultValue;
    }

    private static string? ReadFlexibleString(JsonNode? node)
    {
        if (node is null) return null;

        return node.GetValueKind() switch
        {
            JsonValueKind.String => node.GetValue<string>(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            JsonValueKind.Number => node.ToString(),
            _ => node.ToJsonString()
        };
    }

    private static string? ReadRuleFallbackValue(string key, JsonObject root, JsonObject worldConfig)
    {
        return key switch
        {
            "worldWidth" => ReadFlexibleString(root["MapSizeX"]) ?? ReadFlexibleString(worldConfig["MapSizeX"]),
            "worldLength" => ReadFlexibleString(root["MapSizeZ"]) ?? ReadFlexibleString(worldConfig["MapSizeZ"]),
            _ => null
        };
    }
}
