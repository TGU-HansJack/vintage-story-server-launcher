namespace VSSL.Domains.Models;

/// <summary>
///     总览-联结（OSQ）设置
/// </summary>
public sealed class OverviewLinkageSettings
{
    public bool Enabled { get; init; } = true;

    public string ListenPrefix { get; init; } = "http://127.0.0.1:18089/";

    public bool AllowInsecureHttp { get; init; }

    public int RequestTimeoutSec { get; init; } = 8;

    public bool IncludeServerInfo { get; init; } = true;

    public bool IncludePlayers { get; init; } = true;

    public bool IncludePlayerEvents { get; init; } = true;

    public bool IncludeChats { get; init; } = true;

    public bool IncludeNotifications { get; init; } = true;

    public bool IncludeMapData { get; init; } = true;

    public bool IncludeImages { get; init; } = true;

    public IReadOnlyList<OverviewLinkageEndpointSettings> Endpoints { get; init; } = [];
}

public sealed class OverviewLinkageEndpointSettings
{
    public string ServerHost { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public bool Enabled { get; init; } = true;
}

public sealed class OverviewLinkageRuntimeStatus
{
    public bool IsListening { get; init; }

    public string ListenPrefix { get; init; } = string.Empty;

    public string StartedAtUtc { get; init; } = string.Empty;

    public string LastReceivedUtc { get; init; } = string.Empty;

    public string LastError { get; init; } = string.Empty;

    public long TotalRequests { get; init; }

    public long AcceptedRequests { get; init; }

    public long RejectedRequests { get; init; }

    public IReadOnlyList<OverviewLinkageEndpointRuntime> Endpoints { get; init; } = [];
}

public sealed class OverviewLinkageEndpointRuntime
{
    public string ServerHost { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    public string LastServerName { get; init; } = string.Empty;

    public string LastServerStatus { get; init; } = string.Empty;

    public int LastOnlinePlayers { get; init; }

    public int LastMaxPlayers { get; init; }

    public string LastPayloadTimeUtc { get; init; } = string.Empty;

    public string LastReceivedUtc { get; init; } = string.Empty;

    public string LastError { get; init; } = string.Empty;
}
