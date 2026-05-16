using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     客户端模组限制服务默认实现
/// </summary>
public class ClientModRestrictionService : IClientModRestrictionService
{
    private const string RestrictionServiceModId = "vsslrestriction";
    private const string RestrictionServiceModVersion = "1.1.0";
    private const string RestrictionServiceModFolderName = "vsslrestriction";
    private const string RestrictionServiceModZipName = "vsslrestriction.zip";
    private const string RestrictionReportPrefix = "[VSSL-RESTRICTION-REPORT]";
    private const string ManagedDisabledModsConfigKey = "VsslRestrictionManagedDisabledMods";

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> BuiltInModIds =
    [
        "game",
        "creative",
        "survival",
        RestrictionServiceModId
    ];

    private readonly IInstanceServerConfigService _serverConfigService;
    private readonly IServerProcessService? _serverProcessService;
    private readonly IInstanceProfileService? _profileService;
    private readonly object _sync = new();
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
        var list = BuildHistoryFromProfileLogs(profile, cancellationToken);
        return Task.FromResult<IReadOnlyList<ClientModHistoryEntry>>(list);
    }

    private IReadOnlyList<ClientModHistoryEntry> BuildHistoryFromProfileLogs(
        InstanceProfile profile,
        CancellationToken cancellationToken)
    {
        var parsedFromLogs = new Dictionary<string, MutableHistoryEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var logPath in EnumerateProfileLogFiles(profile.DirectoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                foreach (var line in ReadLinesShared(logPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (!line.Contains(RestrictionReportPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!TryParseRestrictionReportLine(line, out var report)) continue;

                    var whenUtc = report.ReportedAtUtc ?? DateTimeOffset.UtcNow;
                    foreach (var modId in report.ModIds
                                 .Select(NormalizeModId)
                                 .Where(static id => !string.IsNullOrWhiteSpace(id))
                                 .Where(id => !BuiltInModIds.Contains(id))
                                 .Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        IncrementHistorySeen(parsedFromLogs, modId, whenUtc);
                    }
                }
            }
            catch
            {
                // 单个日志文件读取失败不阻断整体加载
            }
        }

        var profileId = NormalizeProfileId(profile.Id);
        lock (_sync)
        {
            var history = EnsureHistoryLoadedUnsafe(profileId);
            var changed = false;
            foreach (var entry in parsedFromLogs.Values)
                changed |= MergeHistoryEntry(history, entry.ModId, entry.SeenCount, entry.LastSeenUtc);

            if (changed)
                SaveHistoryFileUnsafe(profileId, history);

            return ToHistoryEntries(history);
        }
    }

    private static IEnumerable<string> EnumerateProfileLogFiles(string profileDirectoryPath)
    {
        var logsRoot = WorkspacePathHelper.GetProfileLogsPath(profileDirectoryPath);
        var mainLog = WorkspacePathHelper.GetServerMainLogPath(profileDirectoryPath);
        if (File.Exists(mainLog))
            yield return mainLog;

        var archiveRoot = Path.Combine(logsRoot, "Archive");
        if (!Directory.Exists(archiveRoot))
            yield break;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(archiveRoot, "server-main.log", SearchOption.AllDirectories);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
            yield return file;
    }

    private static IEnumerable<string> ReadLinesShared(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = reader.ReadLine()) is not null)
            yield return line;
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetBlacklistedModIdsAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        var root = await LoadConfigRootAsync(profile, cancellationToken);
        var blacklist = ReadBlacklist(root);
        if (SyncServerSideDisabledModsFromBlacklist(root, blacklist))
            await SaveConfigRootAsync(profile, root, cancellationToken);

        return blacklist;
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
        var changedModIds = new List<string>();
        foreach (var modId in modIds)
        {
            var normalized = NormalizeModId(modId);
            if (string.IsNullOrWhiteSpace(normalized)) continue;

            if (blacklist.Add(normalized))
            {
                changed++;
                changedModIds.Add(normalized);
            }
        }

        if (changed == 0) return 0;

        root["ModIdBlackList"] = BuildBlacklistArray(blacklist);
        SyncServerSideDisabledModsFromBlacklist(root, blacklist);
        await SaveConfigRootAsync(profile, root, cancellationToken);
        RememberKnownMods(profile, changedModIds);
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
        var changedModIds = new List<string>();
        foreach (var modId in modIds)
        {
            var normalized = NormalizeModId(modId);
            if (string.IsNullOrWhiteSpace(normalized)) continue;

            if (blacklist.Remove(normalized))
            {
                changed++;
                changedModIds.Add(normalized);
            }
        }

        if (changed == 0) return 0;

        root["ModIdBlackList"] = BuildBlacklistArray(blacklist);
        SyncServerSideDisabledModsFromBlacklist(root, blacklist);
        await SaveConfigRootAsync(profile, root, cancellationToken);
        RememberKnownMods(profile, changedModIds);
        return changed;
    }

    /// <inheritdoc />
    public Task<bool> GetRestrictionServiceModEnabledAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var modsPath = WorkspacePathHelper.GetProfileModsPath(profile.DirectoryPath);
        var folderPath = Path.Combine(modsPath, RestrictionServiceModFolderName);
        var zipPath = Path.Combine(modsPath, RestrictionServiceModZipName);
        var enabled = Directory.Exists(folderPath) || File.Exists(zipPath);
        return Task.FromResult(enabled);
    }

    /// <inheritdoc />
    public async Task SetRestrictionServiceModEnabledAsync(
        InstanceProfile profile,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var modsPath = WorkspacePathHelper.GetProfileModsPath(profile.DirectoryPath);
        Directory.CreateDirectory(modsPath);

        var folderPath = Path.Combine(modsPath, RestrictionServiceModFolderName);
        var zipPath = Path.Combine(modsPath, RestrictionServiceModZipName);

        if (enabled)
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            DeployEmbeddedRestrictionMod(folderPath);
            await RemoveRestrictionServiceModFromDisabledListAsync(profile, cancellationToken);
            return;
        }

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        if (Directory.Exists(folderPath))
            Directory.Delete(folderPath, recursive: true);

        await RemoveRestrictionServiceModFromDisabledListAsync(profile, cancellationToken);
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

    private async Task RemoveRestrictionServiceModFromDisabledListAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken)
    {
        try
        {
            var root = await LoadConfigRootAsync(profile, cancellationToken);
            if (root["WorldConfig"] is not JsonObject worldConfig)
                return;

            if (worldConfig["DisabledMods"] is not JsonArray disabledMods)
                return;

            var beforeCount = disabledMods.Count;
            var cleanupSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                RestrictionServiceModId,
                $"{RestrictionServiceModId}@{RestrictionServiceModVersion}"
            };

            var remain = disabledMods
                .Where(static item => item is not null)
                .Select(static item => item!.GetValue<string>())
                .Where(value => !cleanupSet.Contains(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (remain.Count == beforeCount)
                return;

            disabledMods.Clear();
            foreach (var value in remain)
                disabledMods.Add(value);

            await SaveConfigRootAsync(profile, root, cancellationToken);
        }
        catch
        {
            // 该步骤仅用于清理禁用项，不阻断主流程
        }
    }

    private static void DeployEmbeddedRestrictionMod(string destinationFolderPath)
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "EmbeddedMods", RestrictionServiceModFolderName);
        if (!Directory.Exists(sourceRoot))
            throw new InvalidOperationException("未找到内置限制模组文件，请先重新构建启动器。");

        if (Directory.Exists(destinationFolderPath))
            Directory.Delete(destinationFolderPath, recursive: true);

        CopyDirectory(sourceRoot, destinationFolderPath);
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var directory in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, directory);
            var target = Path.Combine(destinationPath, relative);
            Directory.CreateDirectory(target);
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, file);
            var target = Path.Combine(destinationPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
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

    private static bool SyncServerSideDisabledModsFromBlacklist(JsonObject root, HashSet<string> blacklist)
    {
        var changed = false;
        var disabledMods = GetOrCreateWorldDisabledModsArray(root);
        var managedDisabledMods = ReadManagedDisabledMods(root);

        foreach (var modId in blacklist)
        {
            if (!ContainsExactDisabledModEntry(disabledMods, modId))
            {
                disabledMods.Add(modId);
                changed = true;
            }

            if (managedDisabledMods.Add(modId))
                changed = true;
        }

        var staleManaged = managedDisabledMods
            .Where(managedId => !blacklist.Contains(managedId))
            .ToList();
        foreach (var managedId in staleManaged)
        {
            if (RemoveExactDisabledModEntry(disabledMods, managedId))
                changed = true;

            if (managedDisabledMods.Remove(managedId))
                changed = true;
        }

        if (WriteManagedDisabledMods(root, managedDisabledMods))
            changed = true;

        return changed;
    }

    private static JsonArray GetOrCreateWorldDisabledModsArray(JsonObject root)
    {
        if (root["WorldConfig"] is not JsonObject worldConfig)
        {
            worldConfig = new JsonObject();
            root["WorldConfig"] = worldConfig;
        }

        if (worldConfig["DisabledMods"] is JsonArray disabledMods)
            return disabledMods;

        disabledMods = new JsonArray();
        worldConfig["DisabledMods"] = disabledMods;
        return disabledMods;
    }

    private static HashSet<string> ReadManagedDisabledMods(JsonObject root)
    {
        if (root[ManagedDisabledModsConfigKey] is not JsonArray array)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return array
            .Where(static item => item is not null)
            .Select(static item => NormalizeModId(item!.GetValue<string>()))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool WriteManagedDisabledMods(JsonObject root, HashSet<string> managedDisabledMods)
    {
        if (managedDisabledMods.Count == 0)
            return root.Remove(ManagedDisabledModsConfigKey);

        var serialized = managedDisabledMods
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (root[ManagedDisabledModsConfigKey] is JsonArray currentArray)
        {
            var current = currentArray
                .Where(static item => item is not null)
                .Select(static item => NormalizeModId(item!.GetValue<string>()))
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (current.SequenceEqual(serialized, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        var next = new JsonArray();
        foreach (var modId in serialized)
            next.Add(modId);

        root[ManagedDisabledModsConfigKey] = next;
        return true;
    }

    private static bool ContainsExactDisabledModEntry(JsonArray disabledMods, string modId)
    {
        return disabledMods
            .Where(static item => item is not null)
            .Select(static item => item!.GetValue<string>().Trim())
            .Any(value => value.Equals(modId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool RemoveExactDisabledModEntry(JsonArray disabledMods, string modId)
    {
        var removed = false;
        for (var index = disabledMods.Count - 1; index >= 0; index--)
        {
            if (disabledMods[index] is null) continue;
            var value = disabledMods[index]!.GetValue<string>().Trim();
            if (!value.Equals(modId, StringComparison.OrdinalIgnoreCase)) continue;

            disabledMods.RemoveAt(index);
            removed = true;
        }

        return removed;
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
        if (!line.Contains(RestrictionReportPrefix, StringComparison.OrdinalIgnoreCase)) return;

        var profile = TryResolveRunningProfile();
        if (profile is null) return;

        var profileId = NormalizeProfileId(profile.Id);
        var nowUtc = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            var history = EnsureHistoryLoadedUnsafe(profileId);
            if (!TryExtractReportedModIds(line, out var modIds))
                return;

            var normalizedModIds = modIds
                .Select(NormalizeModId)
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .Where(id => !BuiltInModIds.Contains(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedModIds.Count == 0)
                return;

            foreach (var modId in normalizedModIds)
                IncrementHistorySeen(history, modId, nowUtc);

            SaveHistoryFileUnsafe(profileId, history);
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

    private static bool TryExtractReportedModIds(string line, out IReadOnlyList<string> modIds)
    {
        modIds = [];

        var prefixIndex = line.IndexOf(RestrictionReportPrefix, StringComparison.OrdinalIgnoreCase);
        if (prefixIndex < 0) return false;

        var payload = line[(prefixIndex + RestrictionReportPrefix.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(payload)) return false;

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("mods", out var modsElement) ||
                modsElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var list = new List<string>();
            foreach (var item in modsElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        list.Add(value);
                    continue;
                }

                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("id", out var idElement) &&
                    idElement.ValueKind == JsonValueKind.String)
                {
                    var id = idElement.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                        list.Add(id);
                }
            }

            modIds = list;
            return list.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseRestrictionReportLine(string line, out ParsedRestrictionReport report)
    {
        report = ParsedRestrictionReport.Empty;
        if (!TryExtractReportPayload(line, out var payload))
            return false;

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("mods", out var modsElement) || modsElement.ValueKind != JsonValueKind.Array)
                return false;

            DateTimeOffset? reportedAtUtc = null;
            if (root.TryGetProperty("reportedAtUtc", out var reportedAtElement) &&
                reportedAtElement.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(
                    reportedAtElement.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsedTime))
            {
                reportedAtUtc = parsedTime.ToUniversalTime();
            }

            var modIds = new List<string>();
            foreach (var item in modsElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        modIds.Add(value);
                    continue;
                }

                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("id", out var idElement) &&
                    idElement.ValueKind == JsonValueKind.String)
                {
                    var id = idElement.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                        modIds.Add(id);
                }
            }

            if (modIds.Count == 0)
                return false;

            report = new ParsedRestrictionReport
            {
                ModIds = modIds,
                ReportedAtUtc = reportedAtUtc
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryExtractReportPayload(string line, out string payload)
    {
        payload = string.Empty;
        var prefixIndex = line.IndexOf(RestrictionReportPrefix, StringComparison.OrdinalIgnoreCase);
        if (prefixIndex < 0) return false;

        payload = line[(prefixIndex + RestrictionReportPrefix.Length)..].Trim();
        return !string.IsNullOrWhiteSpace(payload);
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

    private void RememberKnownMods(InstanceProfile profile, IEnumerable<string> modIds)
    {
        var profileId = NormalizeProfileId(profile.Id);
        var normalizedModIds = modIds
            .Select(NormalizeModId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Where(id => !BuiltInModIds.Contains(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedModIds.Count == 0)
            return;

        lock (_sync)
        {
            var history = EnsureHistoryLoadedUnsafe(profileId);
            var changed = false;
            foreach (var modId in normalizedModIds)
                changed |= EnsureHistoryEntry(history, modId);

            if (changed)
                SaveHistoryFileUnsafe(profileId, history);
        }
    }

    private static bool EnsureHistoryEntry(Dictionary<string, MutableHistoryEntry> map, string modId)
    {
        return MergeHistoryEntry(map, modId, 0, null);
    }

    private static void IncrementHistorySeen(
        Dictionary<string, MutableHistoryEntry> map,
        string modId,
        DateTimeOffset seenAtUtc)
    {
        MergeHistoryEntry(map, modId, 1, seenAtUtc, incrementSeen: true);
    }

    private static bool MergeHistoryEntry(
        Dictionary<string, MutableHistoryEntry> map,
        string modId,
        int seenCount,
        DateTimeOffset? lastSeenUtc,
        bool incrementSeen = false)
    {
        if (string.IsNullOrWhiteSpace(modId))
            return false;

        var normalized = NormalizeModId(modId);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;
        if (BuiltInModIds.Contains(normalized))
            return false;

        if (!map.TryGetValue(normalized, out var existing))
        {
            map[normalized] = new MutableHistoryEntry
            {
                ModId = normalized,
                SeenCount = Math.Max(0, seenCount),
                LastSeenUtc = lastSeenUtc
            };
            return true;
        }

        var changed = false;

        if (incrementSeen)
        {
            var updated = Math.Max(0, existing.SeenCount) + Math.Max(0, seenCount);
            if (updated != existing.SeenCount)
            {
                existing.SeenCount = updated;
                changed = true;
            }
        }
        else
        {
            var updated = Math.Max(existing.SeenCount, Math.Max(0, seenCount));
            if (updated != existing.SeenCount)
            {
                existing.SeenCount = updated;
                changed = true;
            }
        }

        if (lastSeenUtc.HasValue && (!existing.LastSeenUtc.HasValue || existing.LastSeenUtc.Value < lastSeenUtc.Value))
        {
            existing.LastSeenUtc = lastSeenUtc;
            changed = true;
        }

        return changed;
    }

    private static List<ClientModHistoryEntry> ToHistoryEntries(Dictionary<string, MutableHistoryEntry> map)
    {
        return map.Values
            .Select(static entry => new ClientModHistoryEntry
            {
                ModId = entry.ModId,
                SeenCount = entry.SeenCount,
                LastSeenUtc = entry.LastSeenUtc
            })
            .OrderByDescending(static item => item.SeenCount)
            .ThenBy(static item => item.ModId, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
                if (string.IsNullOrWhiteSpace(modId)) continue;
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

    private sealed class ParsedRestrictionReport
    {
        public static ParsedRestrictionReport Empty { get; } = new();
        public List<string> ModIds { get; init; } = [];
        public DateTimeOffset? ReportedAtUtc { get; init; }
    }
}
