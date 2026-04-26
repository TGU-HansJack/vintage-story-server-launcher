using System.Globalization;
using System.Text.Json;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     VS2QQ 机器人服务默认实现
/// </summary>
public class RobotService : IRobotService
{
    private const int MaxConsoleLines = 2000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _runtimeGate = new(1, 1);
    private readonly object _consoleGate = new();
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private readonly List<string> _consoleLines = [];

    private RobotRuntimeStatus _status = new();
    private RobotSettings _lastLoadedSettings = new();

    /// <inheritdoc />
    public async Task<RobotSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        WorkspacePathHelper.EnsureWorkspace();
        Directory.CreateDirectory(WorkspacePathHelper.RobotRoot);

        if (!File.Exists(WorkspacePathHelper.RobotSettingsPath))
        {
            var defaults = BuildDefaultSettings();
            await SaveSettingsAsync(defaults, cancellationToken);
            _lastLoadedSettings = defaults;
            return defaults;
        }

        try
        {
            var json = await File.ReadAllTextAsync(WorkspacePathHelper.RobotSettingsPath, cancellationToken);
            var settings = JsonSerializer.Deserialize<RobotSettings>(json) ?? BuildDefaultSettings();
            var normalized = NormalizeSettings(settings);
            _lastLoadedSettings = normalized;
            return normalized;
        }
        catch
        {
            var fallback = BuildDefaultSettings();
            _lastLoadedSettings = fallback;
            return fallback;
        }
    }

    /// <inheritdoc />
    public async Task SaveSettingsAsync(RobotSettings settings, CancellationToken cancellationToken = default)
    {
        WorkspacePathHelper.EnsureWorkspace();
        Directory.CreateDirectory(WorkspacePathHelper.RobotRoot);

        var normalized = NormalizeSettings(settings);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        await File.WriteAllTextAsync(WorkspacePathHelper.RobotSettingsPath, json, cancellationToken);
        _lastLoadedSettings = normalized;
    }

    /// <inheritdoc />
    public RobotRuntimeStatus GetCurrentStatus()
    {
        return _status;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetConsoleLines()
    {
        lock (_consoleGate)
        {
            return _consoleLines.ToList();
        }
    }

    /// <inheritdoc />
    public void ClearConsole()
    {
        lock (_consoleGate)
        {
            _consoleLines.Clear();
        }
    }

    /// <inheritdoc />
    public async Task StartAsync(RobotSettings settings, CancellationToken cancellationToken = default)
    {
        await _runtimeGate.WaitAsync(cancellationToken);
        try
        {
            if (_status.IsRunning)
                throw new InvalidOperationException("机器人已在运行中。");

            var normalized = NormalizeSettings(settings);
            await SaveSettingsAsync(normalized, cancellationToken);

            _runCts = new CancellationTokenSource();
            var loopToken = _runCts.Token;
            _runTask = Task.Run(() => RunLoopAsync(normalized, loopToken), CancellationToken.None);

            _status = new RobotRuntimeStatus
            {
                IsRunning = true,
                ProcessId = Environment.ProcessId,
                StartedAtUtc = DateTimeOffset.UtcNow,
                OneBotWsUrl = normalized.OneBotWsUrl
            };

            AppendConsoleLine($"[system] VS2QQ 已启动，OneBot={normalized.OneBotWsUrl}");
            AppendConsoleLine($"[system] 数据库路径={normalized.DatabasePath}");
        }
        finally
        {
            _runtimeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(TimeSpan gracefulTimeout, CancellationToken cancellationToken = default)
    {
        Task? currentRunTask;
        CancellationTokenSource? currentCts;

        await _runtimeGate.WaitAsync(cancellationToken);
        try
        {
            if (!_status.IsRunning)
                return;

            currentRunTask = _runTask;
            currentCts = _runCts;
            currentCts?.Cancel();
        }
        finally
        {
            _runtimeGate.Release();
        }

        if (currentRunTask is not null)
        {
            var timeoutTask = Task.Delay(gracefulTimeout, cancellationToken);
            var completed = await Task.WhenAny(currentRunTask, timeoutTask);
            cancellationToken.ThrowIfCancellationRequested();

            if (!ReferenceEquals(completed, currentRunTask))
                throw new InvalidOperationException("停止机器人超时。");

            await currentRunTask;
        }

        await _runtimeGate.WaitAsync(cancellationToken);
        try
        {
            _runCts?.Dispose();
            _runCts = null;
            _runTask = null;
            _status = new RobotRuntimeStatus
            {
                IsRunning = false,
                ProcessId = null,
                StartedAtUtc = null,
                OneBotWsUrl = _lastLoadedSettings.OneBotWsUrl
            };
        }
        finally
        {
            _runtimeGate.Release();
        }

        AppendConsoleLine("[system] VS2QQ 已停止。");
    }

    private async Task RunLoopAsync(RobotSettings settings, CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, settings.PollIntervalSec));
        while (!cancellationToken.IsCancellationRequested)
        {
            AppendConsoleLine(
                $"[heartbeat] {DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} VS2QQ 运行中");

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static RobotSettings BuildDefaultSettings()
    {
        return new RobotSettings
        {
            OneBotWsUrl = "ws://127.0.0.1:3001/",
            AccessToken = string.Empty,
            ReconnectIntervalSec = 5,
            DatabasePath = Path.Combine(WorkspacePathHelper.RobotRoot, "vs2qq.db"),
            PollIntervalSec = 1.0,
            DefaultEncoding = "utf-8",
            FallbackEncoding = "gbk",
            SuperUsers = []
        };
    }

    private static RobotSettings NormalizeSettings(RobotSettings settings)
    {
        var wsUrl = string.IsNullOrWhiteSpace(settings.OneBotWsUrl)
            ? "ws://127.0.0.1:3001/"
            : settings.OneBotWsUrl.Trim();
        if (!wsUrl.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) &&
            !wsUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            wsUrl = "ws://127.0.0.1:3001/";
        }

        var dbPath = string.IsNullOrWhiteSpace(settings.DatabasePath)
            ? Path.Combine(WorkspacePathHelper.RobotRoot, "vs2qq.db")
            : Path.GetFullPath(settings.DatabasePath.Trim());
        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dbDirectory))
            Directory.CreateDirectory(dbDirectory);

        var reconnect = settings.ReconnectIntervalSec <= 0 ? 5 : settings.ReconnectIntervalSec;
        var poll = settings.PollIntervalSec <= 0 ? 1.0 : settings.PollIntervalSec;

        var defaultEncoding = string.IsNullOrWhiteSpace(settings.DefaultEncoding)
            ? "utf-8"
            : settings.DefaultEncoding.Trim();
        var fallbackEncoding = string.IsNullOrWhiteSpace(settings.FallbackEncoding)
            ? "gbk"
            : settings.FallbackEncoding.Trim();

        var superUsers = (settings.SuperUsers ?? [])
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        return new RobotSettings
        {
            OneBotWsUrl = wsUrl,
            AccessToken = settings.AccessToken?.Trim() ?? string.Empty,
            ReconnectIntervalSec = reconnect,
            DatabasePath = dbPath,
            PollIntervalSec = poll,
            DefaultEncoding = defaultEncoding,
            FallbackEncoding = fallbackEncoding,
            SuperUsers = superUsers
        };
    }

    private void AppendConsoleLine(string line)
    {
        lock (_consoleGate)
        {
            _consoleLines.Add(line);
            while (_consoleLines.Count > MaxConsoleLines)
                _consoleLines.RemoveAt(0);
        }
    }
}
