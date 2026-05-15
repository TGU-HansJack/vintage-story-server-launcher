namespace VSSL.Domains.Models;

/// <summary>
///     历史客户端模组记录
/// </summary>
public class ClientModHistoryEntry
{
    public required string ModId { get; init; }

    public int SeenCount { get; init; }

    public DateTimeOffset? LastSeenUtc { get; init; }
}
