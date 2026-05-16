using Vintagestory.API.Client;
using Vintagestory.API.Common;
using VsslRestriction.Network;

namespace VsslRestriction.Client;

public sealed class VsslRestrictionClientReporterSystem : ModSystem
{
    private const int MaxSendAttempts = 12;
    private const int RetryDelayMs = 1200;

    private ICoreClientAPI? _clientApi;
    private IClientNetworkChannel? _channel;
    private bool _reportSentThisWorld;
    private int _attemptCount;

    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Client;
    }

    public override double ExecuteOrder()
    {
        return 0.12;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;
        _channel = api.Network.GetChannel(VsslRestrictionModSystem.ChannelName);

        api.Event.LevelFinalize += OnLevelFinalize;
        api.Event.LeaveWorld += OnLeaveWorld;
    }

    private void OnLevelFinalize()
    {
        _reportSentThisWorld = false;
        _attemptCount = 0;
        ScheduleRetry(1000);
    }

    private void OnLeaveWorld()
    {
        _reportSentThisWorld = false;
        _attemptCount = 0;
    }

    private void ScheduleRetry(int delayMs)
    {
        if (_clientApi is null) return;
        _clientApi.Event.RegisterCallback(_ => TrySendReport(), delayMs, permittedWhilePaused: true);
    }

    private void TrySendReport()
    {
        if (_reportSentThisWorld) return;
        if (_clientApi is null || _channel is null) return;

        _attemptCount++;
        var player = _clientApi.World?.Player;
        if (!_channel.Connected || player is null || string.IsNullOrWhiteSpace(player.PlayerUID))
        {
            if (_attemptCount < MaxSendAttempts)
                ScheduleRetry(RetryDelayMs);
            return;
        }

        try
        {
            var packet = BuildReportPacket(player);
            _channel.SendPacket(packet);
            _reportSentThisWorld = true;
        }
        catch
        {
            if (_attemptCount < MaxSendAttempts)
                ScheduleRetry(RetryDelayMs);
        }
    }

    private ClientModReportPacket BuildReportPacket(IClientPlayer player)
    {
        var mods = _clientApi?.ModLoader.Mods ?? [];
        var entries = mods
            .Select(mod => mod?.Info)
            .Where(static info => info is not null)
            .Select(info => new ClientModReportEntry
            {
                ModId = info!.ModID ?? string.Empty,
                Version = info.Version ?? string.Empty,
                NetworkVersion = info.NetworkVersion ?? string.Empty,
                Side = info.Side.ToString()
            })
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.ModId))
            .OrderBy(static entry => entry.ModId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ClientModReportPacket
        {
            PlayerUid = player.PlayerUID ?? string.Empty,
            PlayerName = player.PlayerName ?? string.Empty,
            CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Mods = entries
        };
    }
}
