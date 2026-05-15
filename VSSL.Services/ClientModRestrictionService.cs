using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     客户端模组限制服务默认实现
/// </summary>
public class ClientModRestrictionService(IInstanceServerConfigService serverConfigService) : IClientModRestrictionService
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string[] TimestampFormats =
    [
        "d.M.yyyy H:mm:ss",
        "d.M.yyyy HH:mm:ss",
        "dd.MM.yyyy H:mm:ss",
        "dd.MM.yyyy HH:mm:ss"
    ];

    private static readonly Regex TimestampRegex = new(
        @"^(?<ts>\d{1,2}\.\d{1,2}\.\d{4}\s+\d{1,2}:\d{2}:\d{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ClientModsPayloadRegex = new(
        @"(?:client(?:side)?\s*mods?|mods?\s*from\s*client)\s*[:=]\s*(?<payload>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex JsonClientModsPayloadRegex = new(
        "\"clientmods?\"\\s*:\\s*(?<payload>\\[[^\\]]*\\])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex KeyValueModIdRegex = new(
        "\"?(?:modid|id)\"?\\s*[:=]\\s*\"?(?<id>[a-z0-9][a-z0-9._-]{1,63})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ModTokenRegex = new(
        @"(?<id>[a-z0-9][a-z0-9._-]{1,63})(?:@(?<version>[0-9a-z._-]+))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ExcludedTokens =
    [
        "client", "clients", "mod", "mods", "modid", "id", "server", "servers", "notification", "event",
        "warning", "error", "name", "uid", "true", "false", "null", "version", "versions", "world",
        "sorted", "dependency", "dependencies", "disabled", "enabled", "game", "creative", "survival",
        "json", "payload"
    ];

    /// <inheritdoc />
    public Task<IReadOnlyList<ClientModHistoryEntry>> GetHistoricalClientModsAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        var logsPath = WorkspacePathHelper.GetProfileLogsPath(profile.DirectoryPath);
        if (!Directory.Exists(logsPath))
            return Task.FromResult<IReadOnlyList<ClientModHistoryEntry>>([]);

        var seenCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastSeenByModId = new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase);

        foreach (var logFile in EnumerateServerMainLogs(logsPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var line in ReadLinesWithSharing(logFile, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var hasModId = ContainsIgnoreCase(line, "modid");
                var hasClientAndMod = ContainsIgnoreCase(line, "client") && ContainsIgnoreCase(line, "mod");
                if (!hasClientAndMod && !hasModId) continue;
                if (!ClientModsPayloadRegex.IsMatch(line)
                    && !JsonClientModsPayloadRegex.IsMatch(line)
                    && !hasModId)
                {
                    continue;
                }

                var lastSeenUtc = TryParseLogTimestamp(line)?.ToUniversalTime();
                foreach (var modId in ExtractClientModIdsFromLine(line))
                {
                    if (!seenCounters.TryAdd(modId, 1))
                        seenCounters[modId]++;

                    if (!lastSeenByModId.TryGetValue(modId, out var existing) ||
                        (lastSeenUtc.HasValue && (!existing.HasValue || existing.Value < lastSeenUtc.Value)))
                    {
                        lastSeenByModId[modId] = lastSeenUtc;
                    }
                }
            }
        }

        IReadOnlyList<ClientModHistoryEntry> result = seenCounters
            .Select(pair => new ClientModHistoryEntry
            {
                ModId = pair.Key,
                SeenCount = pair.Value,
                LastSeenUtc = lastSeenByModId.GetValueOrDefault(pair.Key)
            })
            .OrderByDescending(static x => x.SeenCount)
            .ThenBy(static x => x.ModId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetBlacklistedModIdsAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        var root = await LoadConfigRootAsync(profile, cancellationToken);
        return ReadBlacklist(root);
    }

    /// <inheritdoc />
    public async Task<int> AddModIdsToBlacklistAsync(
        InstanceProfile profile,
        IReadOnlyCollection<string> modIds,
        CancellationToken cancellationToken = default)
    {
        if (modIds.Count == 0) return 0;

        var root = await LoadConfigRootAsync(profile, cancellationToken);
        var blacklist = ReadBlacklist(root);

        var changed = 0;
        foreach (var modId in modIds)
        {
            var normalized = NormalizeModId(modId);
            if (string.IsNullOrWhiteSpace(normalized)) continue;

            if (blacklist.Add(normalized))
                changed++;
        }

        if (changed == 0) return 0;

        root["ModIdBlackList"] = BuildBlacklistArray(blacklist);
        await SaveConfigRootAsync(profile, root, cancellationToken);
        return changed;
    }

    /// <inheritdoc />
    public async Task<int> RemoveModIdsFromBlacklistAsync(
        InstanceProfile profile,
        IReadOnlyCollection<string> modIds,
        CancellationToken cancellationToken = default)
    {
        if (modIds.Count == 0) return 0;

        var root = await LoadConfigRootAsync(profile, cancellationToken);
        var blacklist = ReadBlacklist(root);

        var changed = 0;
        foreach (var modId in modIds)
        {
            var normalized = NormalizeModId(modId);
            if (string.IsNullOrWhiteSpace(normalized)) continue;

            if (blacklist.Remove(normalized))
                changed++;
        }

        if (changed == 0) return 0;

        root["ModIdBlackList"] = BuildBlacklistArray(blacklist);
        await SaveConfigRootAsync(profile, root, cancellationToken);
        return changed;
    }

    private async Task<JsonObject> LoadConfigRootAsync(InstanceProfile profile, CancellationToken cancellationToken)
    {
        var rawJson = await serverConfigService.LoadRawJsonAsync(profile, cancellationToken);
        return JsonNode.Parse(rawJson) as JsonObject
               ?? throw new InvalidOperationException("配置格式错误。");
    }

    private async Task SaveConfigRootAsync(InstanceProfile profile, JsonObject root, CancellationToken cancellationToken)
    {
        await serverConfigService.SaveRawJsonAsync(
            profile,
            root.ToJsonString(JsonWriteOptions),
            cancellationToken);
    }

    private static HashSet<string> ReadBlacklist(JsonObject root)
    {
        if (root["ModIdBlackList"] is not JsonArray array)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return array
            .Where(static item => item is not null)
            .Select(static item => NormalizeModId(item!.GetValue<string>()))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static JsonArray BuildBlacklistArray(HashSet<string> blacklist)
    {
        var array = new JsonArray();
        foreach (var modId in blacklist.OrderBy(static x => x, StringComparer.OrdinalIgnoreCase))
            array.Add(modId);

        return array;
    }

    private static IEnumerable<string> EnumerateServerMainLogs(string logsPath)
    {
        var primary = Path.Combine(logsPath, "server-main.log");
        if (File.Exists(primary))
            yield return primary;

        if (!Directory.Exists(logsPath)) yield break;

        foreach (var file in Directory.EnumerateFiles(logsPath, "server-main*.log", SearchOption.TopDirectoryOnly)
                     .Where(path => !path.Equals(primary, StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(static path => File.GetLastWriteTimeUtc(path)))
        {
            yield return file;
        }
    }

    private static IEnumerable<string> ReadLinesWithSharing(string filePath, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = reader.ReadLine();
            if (line is null) continue;
            yield return line;
        }
    }

    private static IEnumerable<string> ExtractClientModIdsFromLine(string line)
    {
        var candidates = new List<string>();

        foreach (Match match in JsonClientModsPayloadRegex.Matches(line))
        {
            if (match.Groups["payload"].Success)
                candidates.Add(match.Groups["payload"].Value);
        }

        foreach (Match match in ClientModsPayloadRegex.Matches(line))
        {
            if (match.Groups["payload"].Success)
                candidates.Add(match.Groups["payload"].Value);
        }

        if (candidates.Count == 0)
        {
            if (!KeyValueModIdRegex.IsMatch(line))
                return [];
            candidates.Add(line);
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var text in candidates)
        {
            foreach (Match match in KeyValueModIdRegex.Matches(text))
            {
                var id = NormalizeModId(match.Groups["id"].Value);
                if (IsLikelyModId(id)) result.Add(id);
            }

            foreach (Match match in ModTokenRegex.Matches(text))
            {
                var id = NormalizeModId(match.Groups["id"].Value);
                if (IsLikelyModId(id)) result.Add(id);
            }
        }

        return result;
    }

    private static string NormalizeModId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var candidate = value.Trim().Trim('"', '\'', ',', ';', '[', ']', '{', '}');
        var atIndex = candidate.IndexOf('@');
        if (atIndex > 0)
            candidate = candidate[..atIndex];

        return candidate.Trim().ToLowerInvariant();
    }

    private static bool IsLikelyModId(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        if (candidate.Length < 2 || candidate.Length > 64) return false;
        if (ExcludedTokens.Contains(candidate)) return false;
        if (candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return false;
        if (candidate.StartsWith("vintagestory.", StringComparison.OrdinalIgnoreCase)) return false;
        if (candidate.Contains('\\') || candidate.Contains('/')) return false;

        return true;
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        return source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? TryParseLogTimestamp(string line)
    {
        var match = TimestampRegex.Match(line);
        if (!match.Success) return null;

        var text = match.Groups["ts"].Value;
        if (!DateTimeOffset.TryParseExact(
                text,
                TimestampFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var parsed))
        {
            return null;
        }

        return parsed;
    }
}
