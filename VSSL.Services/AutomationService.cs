using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     自动化调度服务（定时开关服/播报/日志导出）
/// </summary>
public partial class AutomationService : IAutomationService, IDisposable
{
    private readonly IAutomationSettingsService _settingsService;
    private readonly IInstanceProfileService _profileService;
    private readonly ILogTailService _logTailService;
    private readonly IServerProcessService _serverProcessService;
    private readonly object _logsGate = new();
    private readonly List<string> _runtimeLogs = [];
    private readonly ConcurrentQueue<string> _latestServerLines = new();
    private readonly HashSet<string> _executedMinuteKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loopTask;

    private const int MaxRuntimeLogs = 1500;
    private const int MaxServerLines = 5000;

    private AutomationSettings _settings = new();
    private DateTime _lastTickMinute = DateTime.MinValue;

    public event EventHandler<string>? RuntimeLogReceived;

    public AutomationService(
        IAutomationSettingsService settingsService,
        IInstanceProfileService profileService,
        ILogTailService logTailService,
        IServerProcessService serverProcessService)
    {
        _settingsService = settingsService;
        _profileService = profileService;
        _logTailService = logTailService;
        _serverProcessService = serverProcessService;

        _serverProcessService.OutputReceived += OnServerOutputReceived;
        _logTailService.LogLineReceived += OnLogTailLineReceived;
        _serverProcessService.StatusChanged += OnServerStatusChanged;
        _loopTask = Task.Run(() => LoopAsync(_cts.Token), CancellationToken.None);
    }

    public IReadOnlyList<string> GetRuntimeLogs()
    {
        lock (_logsGate)
        {
            return _runtimeLogs.ToList();
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _settingsService.LoadAsync(cancellationToken);
        WriteRuntimeLog("已重新加载自动化设置。");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _serverProcessService.OutputReceived -= OnServerOutputReceived;
        _logTailService.LogLineReceived -= OnLogTailLineReceived;
        _serverProcessService.StatusChanged -= OnServerStatusChanged;
        try
        {
            _loopTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignored
        }
        _cts.Dispose();
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        await ReloadAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                WriteRuntimeLog($"自动化循环异常：{ex.Message}");
            }

            var delay = TimeSpan.FromSeconds(Math.Max(1, 60 - DateTime.Now.Second));
            await Task.Delay(delay, cancellationToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        if (minute == _lastTickMinute)
            return;

        _lastTickMinute = minute;
        PurgeExecutedKeys(minute);

        if (_settings.RestartSchedulerEnabled)
            await HandleRestartWindowsAsync(minute, cancellationToken);

        if (_settings.BroadcastEnabled)
            await HandleBroadcastAsync(minute, cancellationToken);

        if (_settings.ExportLogEnabled)
            await HandleExportLogsAsync(minute, cancellationToken);
    }

    private async Task HandleRestartWindowsAsync(DateTime minute, CancellationToken cancellationToken)
    {
        var profile = ResolveTargetProfile();
        if (profile is null)
            return;

        foreach (var window in _settings.TimeWindows.Where(w => w.Enabled))
        {
            if (!TryParseHm(window.StartTime, out var start) || !TryParseHm(window.EndTime, out var end))
                continue;

            var date = minute.Date;
            var startTime = date.Add(start);
            var endTime = date.Add(end);
            if (endTime <= startTime)
                endTime = endTime.AddDays(1);

            // warning before shutdown
            foreach (var remainMinute in new[] { 5, 3, 1 })
            {
                if (minute == endTime.AddMinutes(-remainMinute))
                {
                    var key = $"warn|{endTime:yyyyMMddHHmm}|{remainMinute}";
                    if (MarkExecuted(key))
                        await TryBroadcastSystemMessageAsync($"服务器将在 {remainMinute} 分钟后关闭。", cancellationToken);
                }
            }

            // start
            if (minute == startTime)
            {
                var key = $"start|{startTime:yyyyMMddHHmm}";
                if (MarkExecuted(key))
                    await EnsureServerStartedAsync(profile, cancellationToken);
            }

            // stop
            if (minute == endTime)
            {
                var key = $"stop|{endTime:yyyyMMddHHmm}";
                if (MarkExecuted(key))
                    await EnsureServerStoppedAsync(cancellationToken);
            }
        }
    }

    private async Task HandleBroadcastAsync(DateTime minute, CancellationToken cancellationToken)
    {
        foreach (var item in _settings.BroadcastMessages.Where(x => x.Enabled))
        {
            if (!TryParseHm(item.Time, out var at))
                continue;

            var point = minute.Date.Add(at);
            if (point != minute)
                continue;

            var key = $"broadcast|{minute:yyyyMMddHHmm}|{item.Message}";
            if (!MarkExecuted(key))
                continue;

            await TryBroadcastSystemMessageAsync(item.Message, cancellationToken);
        }
    }

    private async Task HandleExportLogsAsync(DateTime minute, CancellationToken cancellationToken)
    {
        foreach (var time in _settings.ExportTimes)
        {
            if (!TryParseHm(time, out var at))
                continue;

            var point = minute.Date.Add(at);
            if (point != minute)
                continue;

            var key = $"export|{minute:yyyyMMddHHmm}|{time}";
            if (!MarkExecuted(key))
                continue;

            await ExportLogsAsync("scheduled", cancellationToken);
        }
    }

    private async Task EnsureServerStartedAsync(InstanceProfile profile, CancellationToken cancellationToken)
    {
        var status = _serverProcessService.GetCurrentStatus();
        if (status.IsRunning)
        {
            WriteRuntimeLog("自动化：服务端已在运行，跳过开服。");
            return;
        }

        await _serverProcessService.StartAsync(profile, cancellationToken);
        WriteRuntimeLog($"自动化：已按计划启动服务端（档案：{profile.Name}）。");
    }

    private async Task EnsureServerStoppedAsync(CancellationToken cancellationToken)
    {
        if (_settings.ExportBeforeShutdown)
            await ExportLogsAsync("before-shutdown", cancellationToken);

        var status = _serverProcessService.GetCurrentStatus();
        if (!status.IsRunning)
        {
            WriteRuntimeLog("自动化：服务端未运行，跳过关服。");
            return;
        }

        await _serverProcessService.StopAsync(TimeSpan.FromSeconds(15), cancellationToken);
        WriteRuntimeLog("自动化：已按计划关闭服务端。");
    }

    private async Task TryBroadcastSystemMessageAsync(string content, CancellationToken cancellationToken)
    {
        var status = _serverProcessService.GetCurrentStatus();
        if (!status.IsRunning)
        {
            WriteRuntimeLog($"自动化播报跳过（服务端未运行）：{content}");
            return;
        }

        var normalized = content.Replace('\r', ' ').Replace('\n', ' ').Trim();
        await _serverProcessService.SendCommandAsync($"/announce {normalized}", cancellationToken);
        WriteRuntimeLog($"自动化播报：{content}");
    }

    private async Task ExportLogsAsync(string reason, CancellationToken cancellationToken)
    {
        var exportRoot = Path.Combine(WorkspacePathHelper.WorkspaceRoot, "exports", "automation");
        Directory.CreateDirectory(exportRoot);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        var sourceLines = _latestServerLines.ToArray();
        if (sourceLines.Length == 0)
        {
            WriteRuntimeLog("自动化日志导出跳过：当前未采集到控制台输出。");
            return;
        }

        var allPath = Path.Combine(exportRoot, $"automation-{reason}-{timestamp}-all.log");
        await File.WriteAllLinesAsync(allPath, sourceLines, cancellationToken);

        if (_settings.ExportIncludeChat)
        {
            var chatLines = sourceLines.Where(IsChatLine).ToList();
            var chatPath = Path.Combine(exportRoot, $"automation-{reason}-{timestamp}-chat.log");
            await File.WriteAllLinesAsync(chatPath, chatLines, cancellationToken);
        }

        if (_settings.ExportIncludeServerInfo)
        {
            var infoLines = sourceLines.Where(IsServerInfoLine).ToList();
            var infoPath = Path.Combine(exportRoot, $"automation-{reason}-{timestamp}-server.log");
            await File.WriteAllLinesAsync(infoPath, infoLines, cancellationToken);
        }

        WriteRuntimeLog($"自动化日志已导出：{allPath}");
    }

    private static bool IsChatLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        return ChatRegex().IsMatch(line);
    }

    private static bool IsServerInfoLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        return !IsChatLine(line);
    }

    private void OnServerOutputReceived(object? sender, string line)
    {
        _latestServerLines.Enqueue(line);
        while (_latestServerLines.Count > MaxServerLines)
            _latestServerLines.TryDequeue(out _);
    }

    private void OnLogTailLineReceived(object? sender, string line)
    {
        _latestServerLines.Enqueue($"[log] {line}");
        while (_latestServerLines.Count > MaxServerLines)
            _latestServerLines.TryDequeue(out _);
    }

    private void OnServerStatusChanged(object? sender, ServerRuntimeStatus status)
    {
        var state = status.IsRunning ? "运行中" : "未运行";
        WriteRuntimeLog($"服务端状态更新：{state}，在线 {status.OnlinePlayers}。");
    }

    private InstanceProfile? ResolveTargetProfile()
    {
        var profiles = _profileService.GetProfiles();
        if (profiles.Count == 0)
        {
            WriteRuntimeLog("自动化未找到档案，无法执行计划。");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_settings.TargetProfileId))
        {
            var matched = profiles.FirstOrDefault(profile =>
                profile.Id.Equals(_settings.TargetProfileId, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
                return matched;
        }

        return profiles[0];
    }

    private void WriteRuntimeLog(string line)
    {
        var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}";
        lock (_logsGate)
        {
            _runtimeLogs.Add(text);
            while (_runtimeLogs.Count > MaxRuntimeLogs)
                _runtimeLogs.RemoveAt(0);
        }

        RuntimeLogReceived?.Invoke(this, text);
    }

    private bool MarkExecuted(string key)
    {
        lock (_executedMinuteKeys)
        {
            return _executedMinuteKeys.Add(key);
        }
    }

    private void PurgeExecutedKeys(DateTime minute)
    {
        var dayKey = minute.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        lock (_executedMinuteKeys)
        {
            _executedMinuteKeys.RemoveWhere(key => !key.Contains(dayKey, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static bool TryParseHm(string? value, out TimeSpan result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        if (TimeSpan.TryParseExact(value.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out result))
            return true;

        return TimeSpan.TryParse(value.Trim(), out result);
    }

    [GeneratedRegex(@"\[(Talk|Chat|Event|Audit)\]|<[^>]+>\s*.+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChatRegex();
}
