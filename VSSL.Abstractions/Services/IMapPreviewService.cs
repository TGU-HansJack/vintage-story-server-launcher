using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

public interface IMapPreviewService
{
    Task<MapPreviewData> LoadMapPreviewAsync(
        InstanceProfile profile,
        string? saveFilePath = null,
        CancellationToken cancellationToken = default);
}
