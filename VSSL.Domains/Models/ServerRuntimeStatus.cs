namespace VSSL.Domains.Models;

/// <summary>
///     服务器运行状态
/// </summary>
public class ServerRuntimeStatus
{
    public bool IsRunning { get; init; }

    public int? ProcessId { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public string? ProfileId { get; init; }

    public long MemoryBytes { get; init; }

    public int OnlinePlayers { get; init; }
}
