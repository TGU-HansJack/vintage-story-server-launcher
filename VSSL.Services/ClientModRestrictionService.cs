using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     客户端模组限制服务默认实现
/// </summary>
public class ClientModRestrictionService : IClientModRestrictionService
{
    private const int HandshakeCaptureTtlSec = 15;

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex HandshakeStartPattern = new(
        @"Received identification packet from\s+(?<player>[^\r\n]+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex JoinBeginPattern = new(
        @"HandleRequestJoin:\s*Begin\.\s*Player:\s*(?<player>[^\r\n]+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

    private static readonly HashSet<string> BuiltInModIds =
    [
        "game",
        "creative",
        "survival"
    ];

    private readonly IInstanceServerConfigService _serverConfigService;
    private readonly IServerProcessService? _serverProcessService;
    private readonly IInstanceProfileService? _profileService;
    private readonly object _sync = new();
    private readonly Dictionary<string, HandshakeCaptureState> _captureByProfileId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, MutableHistoryEntry>> _historyCache = new(StringComparer.OrdinalIgnoreCase);

    public ClientModRestrictionService(IInstanceServerConfigService serverConfigService)
        : this(serverConfigService, null, null)
    {
    }

    public ClientModRestrictionService(
        IInstanceServerConfigService serverConfigService,
        IServerProcessService? serverProcessService,
        IInstanceProfileService? profileService)
    {
        _serverConfigService = serverConfigService;
        _serverProcessService = serverProcessService;
        _profileService = profileService;

        if (_serverProcessService is not null)
            _serverProcessService.OutputReceived += OnServerOutputReceived;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ClientModHistoryEntry>> GetHistoricalClientModsAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var profileId = NormalizeProfileId(profile.Id);
        Dictionary<string, MutableHistoryEntry> map;
        lock (_sync)
        {
            map = EnsureHistoryLoadedUnsafe(profileId);
            var list = map.Values
                .Select(static entry => new ClientModHistoryEntry
                {
                    ModId = entry.ModId,
                    SeenCount = entry.SeenCount,
                    LastSeenUtc = entry.LastSeenUtc
                })
                .OrderByDescending(static x => x.SeenCount)
                .ThenBy(static x => x.ModId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Task.FromResult<IReadOnlyList<ClientModHistoryEntry>>(list);
        }
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
        var rawJson = await _serverConfigService.LoadRawJsonAsync(profile, cancellationToken);
        return JsonNode.Parse(rawJson) as JsonObject
               ?? throw new InvalidOperationException("配置格式错误。");
    }

    private async Task SaveConfigRootAsync(InstanceProfile profile, JsonObject root, CancellationToken cancellationToken)
    {
        await _serverConfigService.SaveRawJsonAsync(
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

    private void OnServerOutputReceived(object? sender, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        var profile = TryResolveRunningProfile();
        if (profile is null) return;

        var profileId = NormalizeProfileId(profile.Id);
        var nowUtc = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            CleanupExpiredCapturesUnsafe(nowUtc);
            EnsureHistoryLoadedUnsafe(profileId);

            if (TryBeginHandshakeCaptureUnsafe(profileId, line, nowUtc))
            {
                RecordModIdsFromLineUnsafe(profileId, line, nowUtc);
                return;
            }

            if (!_captureByProfileId.TryGetValue(profileId, out var capture)) return;
            if (capture.ExpiresAtUtc <= nowUtc)
            {
                _captureByProfileId.Remove(profileId);
                return;
            }

            RecordModIdsFromLineUnsafe(profileId, line, nowUtc);
            TryCloseHandshakeCaptureUnsafe(profileId, line, capture.PlayerName);
        }
    }

    private InstanceProfile? TryResolveRunningProfile()
    {
        if (_serverProcessService is null || _profileService is null)
            return null;

        try
        {
            var status = _serverProcessService.GetCurrentStatus();
            var profileId = status.ProfileId?.Trim();
            if (string.IsNullOrWhiteSpace(profileId))
                return null;

            return _profileService.GetProfileById(profileId) ??
                   _profileService.GetProfiles()
                       .FirstOrDefault(profile => profile.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private bool TryBeginHandshakeCaptureUnsafe(string profileId, string line, DateTimeOffset nowUtc)
    {
        var match = HandshakeStartPattern.Match(line);
        if (!match.Success) return false;

        var playerName = match.Groups["player"].Value.Trim();
        _captureByProfileId[profileId] = new HandshakeCaptureState
        {
            PlayerName = playerName,
            ExpiresAtUtc = nowUtc.AddSeconds(HandshakeCaptureTtlSec)
        };
        return true;
    }

    private void TryCloseHandshakeCaptureUnsafe(string profileId, string line, string expectedPlayer)
    {
        var match = JoinBeginPattern.Match(line);
        if (!match.Success) return;

        var playerName = match.Groups["player"].Value.Trim();
        if (!playerName.Equals(expectedPlayer, StringComparison.OrdinalIgnoreCase))
            return;

        _captureByProfileId.Remove(profileId);
    }

    private void CleanupExpiredCapturesUnsafe(DateTimeOffset nowUtc)
    {
        var expired = _captureByProfileId
            .Where(x => x.Value.ExpiresAtUtc <= nowUtc)
            .Select(x => x.Key)
            .ToList();

        foreach (var key in expired)
            _captureByProfileId.Remove(key);
    }

    private void RecordModIdsFromLineUnsafe(string profileId, string line, DateTimeOffset seenUtc)
    {
        if (!MayContainClientModPayload(line))
            return;

        var modIds = ExtractClientModIdsFromHandshakeLine(line)
            .Where(IsLikelyModId)
            .Where(static id => !BuiltInModIds.Contains(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (modIds.Count == 0)
            return;

        var history = EnsureHistoryLoadedUnsafe(profileId);
        foreach (var modId in modIds)
        {
            if (!history.TryGetValue(modId, out var existing))
            {
                history[modId] = new MutableHistoryEntry
                {
                    ModId = modId,
                    SeenCount = 1,
                    LastSeenUtc = seenUtc
                };
                continue;
            }

            existing.SeenCount = Math.Max(0, existing.SeenCount) + 1;
            if (!existing.LastSeenUtc.HasValue || existing.LastSeenUtc.Value < seenUtc)
                existing.LastSeenUtc = seenUtc;
        }

        SaveHistoryFileUnsafe(profileId, history);
    }

    private static bool MayContainClientModPayload(string line)
    {
        return line.Contains("mod", StringComparison.OrdinalIgnoreCase)
               || line.Contains("clientmods", StringComparison.OrdinalIgnoreCase)
               || line.Contains("modid", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ExtractClientModIdsFromHandshakeLine(string line)
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

        if (candidates.Count == 0 && KeyValueModIdRegex.IsMatch(line))
            candidates.Add(line);

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

    private Dictionary<string, MutableHistoryEntry> EnsureHistoryLoadedUnsafe(string profileId)
    {
        if (_historyCache.TryGetValue(profileId, out var cached))
            return cached;

        var loaded = LoadHistoryFileUnsafe(profileId);
        _historyCache[profileId] = loaded;
        return loaded;
    }

    private static string NormalizeProfileId(string? profileId)
    {
        var value = profileId?.Trim();
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }

    private static string HistoryRootPath => Path.Combine(WorkspacePathHelper.RuntimeRoot, "client-mod-history");

    private static string GetHistoryFilePath(string profileId)
    {
        return Path.Combine(
            HistoryRootPath,
            $"{WorkspacePathHelper.SanitizeFileName(profileId)}.json");
    }

    private static Dictionary<string, MutableHistoryEntry> LoadHistoryFileUnsafe(string profileId)
    {
        try
        {
            WorkspacePathHelper.EnsureWorkspace();
            Directory.CreateDirectory(HistoryRootPath);
            var path = GetHistoryFilePath(profileId);
            if (!File.Exists(path))
                return new Dictionary<string, MutableHistoryEntry>(StringComparer.OrdinalIgnoreCase);

            var json = File.ReadAllText(path);
            var store = JsonSerializer.Deserialize<ClientModHistoryStore>(json, JsonReadOptions);
            if (store?.Entries is null || store.Entries.Count == 0)
                return new Dictionary<string, MutableHistoryEntry>(StringComparer.OrdinalIgnoreCase);

            var map = new Dictionary<string, MutableHistoryEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in store.Entries)
            {
                var modId = NormalizeModId(item.ModId);
                if (!IsLikelyModId(modId)) continue;
                if (BuiltInModIds.Contains(modId)) continue;

                if (!map.TryGetValue(modId, out var existing))
                {
                    map[modId] = new MutableHistoryEntry
                    {
                        ModId = modId,
                        SeenCount = Math.Max(0, item.SeenCount),
                        LastSeenUtc = item.LastSeenUtc
                    };
                    continue;
                }

                existing.SeenCount = Math.Max(existing.SeenCount, Math.Max(0, item.SeenCount));
                if (!existing.LastSeenUtc.HasValue || (item.LastSeenUtc.HasValue && item.LastSeenUtc > existing.LastSeenUtc))
                    existing.LastSeenUtc = item.LastSeenUtc;
            }

            return map;
        }
        catch
        {
            return new Dictionary<string, MutableHistoryEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveHistoryFileUnsafe(string profileId, Dictionary<string, MutableHistoryEntry> map)
    {
        try
        {
            WorkspacePathHelper.EnsureWorkspace();
            Directory.CreateDirectory(HistoryRootPath);
            var path = GetHistoryFilePath(profileId);
            var store = new ClientModHistoryStore
            {
                Entries = map.Values
                    .OrderByDescending(static x => x.SeenCount)
                    .ThenBy(static x => x.ModId, StringComparer.OrdinalIgnoreCase)
                    .Select(static x => new ClientModHistoryStoreEntry
                    {
                        ModId = x.ModId,
                        SeenCount = Math.Max(0, x.SeenCount),
                        LastSeenUtc = x.LastSeenUtc
                    })
                    .ToList()
            };

            var json = JsonSerializer.Serialize(store, JsonWriteOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // 采集失败不影响主流程
        }
    }

    private sealed class HandshakeCaptureState
    {
        public string PlayerName { get; init; } = string.Empty;
        public DateTimeOffset ExpiresAtUtc { get; init; }
    }

    private sealed class MutableHistoryEntry
    {
        public string ModId { get; init; } = string.Empty;
        public int SeenCount { get; set; }
        public DateTimeOffset? LastSeenUtc { get; set; }
    }

    private sealed class ClientModHistoryStore
    {
        public List<ClientModHistoryStoreEntry> Entries { get; init; } = [];
    }

    private sealed class ClientModHistoryStoreEntry
    {
        public string ModId { get; init; } = string.Empty;
        public int SeenCount { get; init; }
        public DateTimeOffset? LastSeenUtc { get; init; }
    }
}
