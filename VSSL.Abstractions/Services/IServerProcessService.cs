using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
///     服务端进程服务
/// </summary>
public interface IServerProcessService
{
    event EventHandler<string>? OutputReceived;

    event EventHandler<ServerRuntimeStatus>? StatusChanged;

    ServerRuntimeStatus GetCurrentStatus();

    Task StartAsync(InstanceProfile profile, CancellationToken cancellationToken = default);

    Task StopAsync(TimeSpan gracefulTimeout, CancellationToken cancellationToken = default);

    Task SendCommandAsync(string command, CancellationToken cancellationToken = default);
}
