namespace VSSL.Domains.Models;

/// <summary>
///     启动器状态采样
/// </summary>
public class LauncherMetricSample
{
    public DateTimeOffset TimestampUtc { get; init; }

    public long ServerMemoryBytes { get; init; }

    public long RobotMemoryBytes { get; init; }

    public int OnlinePlayers { get; init; }

    public TimeSpan ServerUptime { get; init; }

    public TimeSpan RobotUptime { get; init; }

    public bool ServerRunning { get; init; }

    public bool RobotRunning { get; init; }
}
