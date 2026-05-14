using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
///     总览页联结（OSQ）服务
/// </summary>
public interface IOverviewLinkageService
{
    event EventHandler<string>? OutputReceived;

    Task<OverviewLinkageSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(OverviewLinkageSettings settings, CancellationToken cancellationToken = default);

    OverviewLinkageRuntimeStatus GetRuntimeStatus();

    Task StartAsync(OverviewLinkageSettings settings, CancellationToken cancellationToken = default);

    Task StopAsync(TimeSpan gracefulTimeout, CancellationToken cancellationToken = default);
}
