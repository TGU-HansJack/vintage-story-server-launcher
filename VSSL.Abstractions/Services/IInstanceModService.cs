using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
///     实例模组服务
/// </summary>
public interface IInstanceModService
{
    Task<IReadOnlyList<ModEntry>> GetModsAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default);

    Task<ModEntry> ImportModZipAsync(
        InstanceProfile profile,
        string zipPath,
        CancellationToken cancellationToken = default);

    Task SetModEnabledAsync(
        InstanceProfile profile,
        string modId,
        string version,
        bool enabled,
        CancellationToken cancellationToken = default);
}
