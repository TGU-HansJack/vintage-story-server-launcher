using System.Collections.Concurrent;
using System.Diagnostics;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     系统状态采样服务默认实现
/// </summary>
public class SystemStatusService : ISystemStatusService
{
    private const int MaxSamples = 180;
    private readonly IServerProcessService _serverProcessService;
    private readonly IRobotService _robotService;
    private readonly ConcurrentQueue<LauncherMetricSample> _samples = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _samplingTask;
    private LauncherMetricSample _latest = new()
    {
        TimestampUtc = DateTimeOffset.UtcNow
    };

    public SystemStatusService(IServerProcessService serverProcessService, IRobotService robotService)
    {
        _serverProcessService = serverProcessService;
        _robotService = robotService;
        _samplingTask = Task.Run(() => SamplingLoopAsync(_cts.Token), CancellationToken.None);
    }

    /// <inheritdoc />
    public LauncherMetricSample GetLatestSample()
    {
        return _latest;
    }

    /// <inheritdoc />
    public IReadOnlyList<LauncherMetricSample> GetRecentSamples(int maxCount = 60)
    {
        var safeMax = maxCount <= 0 ? 1 : maxCount;
        return _samples.ToArray()
            .OrderBy(sample => sample.TimestampUtc)
            .TakeLast(safeMax)
            .ToList();
    }

    private async Task SamplingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                SampleOnce();
                await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(1200, cancellationToken);
            }
        }
    }

    private void SampleOnce()
    {
        var serverStatus = _serverProcessService.GetCurrentStatus();
        var robotStatus = _robotService.GetCurrentStatus();
        var now = DateTimeOffset.UtcNow;

        var serverMemory = serverStatus.IsRunning
            ? ResolveProcessMemory(serverStatus.ProcessId) ?? serverStatus.MemoryBytes
            : 0;
        var robotMemory = robotStatus.IsRunning
            ? ResolveProcessMemory(robotStatus.ProcessId) ?? 0
            : 0;

        var sample = new LauncherMetricSample
        {
            TimestampUtc = now,
            ServerMemoryBytes = serverMemory,
            RobotMemoryBytes = robotMemory,
            OnlinePlayers = serverStatus.OnlinePlayers,
            ServerUptime = serverStatus.IsRunning && serverStatus.StartedAtUtc.HasValue
                ? now - serverStatus.StartedAtUtc.Value
                : TimeSpan.Zero,
            RobotUptime = robotStatus.IsRunning && robotStatus.StartedAtUtc.HasValue
                ? now - robotStatus.StartedAtUtc.Value
                : TimeSpan.Zero,
            ServerRunning = serverStatus.IsRunning,
            RobotRunning = robotStatus.IsRunning
        };

        _samples.Enqueue(sample);
        while (_samples.Count > MaxSamples && _samples.TryDequeue(out _))
        {
        }

        _latest = sample;
    }

    private static long? ResolveProcessMemory(int? processId)
    {
        if (!processId.HasValue || processId.Value <= 0) return null;

        try
        {
            using var process = Process.GetProcessById(processId.Value);
            return process.WorkingSet64;
        }
        catch
        {
            return null;
        }
    }
}
