using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
///     客户端模组限制服务
/// </summary>
public interface IClientModRestrictionService
{
    Task<IReadOnlyList<ClientModHistoryEntry>> GetHistoricalClientModsAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> GetBlacklistedModIdsAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default);

    Task<int> AddModIdsToBlacklistAsync(
        InstanceProfile profile,
        IReadOnlyCollection<string> modIds,
        CancellationToken cancellationToken = default);

    Task<int> RemoveModIdsFromBlacklistAsync(
        InstanceProfile profile,
        IReadOnlyCollection<string> modIds,
        CancellationToken cancellationToken = default);
}
