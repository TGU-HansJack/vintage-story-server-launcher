using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using VsslRestriction.Network;

namespace VsslRestriction.Server;

public sealed class VsslRestrictionServerReceiverSystem : ModSystem
{
    private const int MinRepeatWindowSeconds = 6;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly object _sync = new();
    private readonly Dictionary<string, DateTimeOffset> _lastReportedAtByPlayerUid = new(StringComparer.OrdinalIgnoreCase);

    private ICoreServerAPI? _serverApi;
    private IServerNetworkChannel? _channel;

    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide != EnumAppSide.Client;
    }

    public override double ExecuteOrder()
    {
        return 0.12;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _serverApi = api;
        _channel = api.Network.GetChannel(VsslRestrictionModSystem.ChannelName)
            .SetMessageHandler<ClientModReportPacket>(OnClientModReport);
    }

    private void OnClientModReport(IPlayer fromPlayer, ClientModReportPacket packet)
    {
        if (_serverApi is null) return;
        if (packet is null) return;

        var playerUid = SelectFirstNonEmpty(packet.PlayerUid, fromPlayer?.PlayerUID);
        if (string.IsNullOrWhiteSpace(playerUid)) return;

        var nowUtc = DateTimeOffset.UtcNow;
        lock (_sync)
        {
            if (_lastReportedAtByPlayerUid.TryGetValue(playerUid, out var lastAt) &&
                nowUtc - lastAt < TimeSpan.FromSeconds(MinRepeatWindowSeconds))
            {
                return;
            }

            _lastReportedAtByPlayerUid[playerUid] = nowUtc;
        }

        var playerName = SelectFirstNonEmpty(packet.PlayerName, fromPlayer?.PlayerName);
        var mods = (packet.Mods ?? [])
            .Where(static mod => mod is not null)
            .Select(static mod => new ReportModItem
            {
                Id = (mod.ModId ?? string.Empty).Trim().ToLowerInvariant(),
                Version = (mod.Version ?? string.Empty).Trim(),
                NetworkVersion = (mod.NetworkVersion ?? string.Empty).Trim(),
                Side = (mod.Side ?? string.Empty).Trim()
            })
            .Where(static mod => !string.IsNullOrWhiteSpace(mod.Id))
            .GroupBy(static mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var payload = new ReportLogPayload
        {
            PlayerUid = playerUid,
            PlayerName = playerName,
            ReportedAtUtc = nowUtc,
            ClientTimestampUnix = packet.CreatedAtUnixSeconds,
            Mods = mods
        };

        TryLogBlacklistHit(fromPlayer, mods);

        var line = VsslRestrictionModSystem.ReportLogPrefix + " " +
                   JsonSerializer.Serialize(payload, JsonOptions);
        _serverApi.Logger.Notification("{0}", line);
    }

    private void TryLogBlacklistHit(IPlayer? fromPlayer, List<ReportModItem> reportedMods)
    {
        if (_serverApi is null) return;
        if (fromPlayer is null) return;
        if (reportedMods.Count == 0) return;

        var blacklist = LoadBlacklistFromServerConfig();
        if (blacklist.Count == 0) return;

        var hitList = reportedMods
            .Select(static mod => NormalizeModId(mod.Id))
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Where(id => blacklist.Contains(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (hitList.Count == 0) return;
        _serverApi.Logger.Notification(
            "{0} Player {1} ({2}) reported blocked client mods (not kicked): {3}",
            VsslRestrictionModSystem.ReportLogPrefix,
            fromPlayer.PlayerName,
            fromPlayer.PlayerUID,
            string.Join(",", hitList));
    }

    private static HashSet<string> LoadBlacklistFromServerConfig()
    {
        var path = Path.Combine(GamePaths.Config, "serverconfig.json");
        if (!File.Exists(path))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("ModIdBlackList", out var blacklistElement) ||
                blacklistElement.ValueKind != JsonValueKind.Array)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in blacklistElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                var id = NormalizeModId(item.GetString());
                if (!string.IsNullOrWhiteSpace(id))
                    result.Add(id);
            }

            return result;
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
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

    private static string SelectFirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }

    private sealed class ReportLogPayload
    {
        public string PlayerUid { get; init; } = string.Empty;
        public string PlayerName { get; init; } = string.Empty;
        public DateTimeOffset ReportedAtUtc { get; init; }
        public long ClientTimestampUnix { get; init; }
        public List<ReportModItem> Mods { get; init; } = [];
    }

    private sealed class ReportModItem
    {
        public string Id { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public string NetworkVersion { get; init; } = string.Empty;
        public string Side { get; init; } = string.Empty;
    }
}
