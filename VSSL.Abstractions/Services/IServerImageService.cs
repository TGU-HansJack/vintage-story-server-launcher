using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

public interface IServerImageService
{
    string GetImageRootPath(InstanceProfile profile);

    Task<IReadOnlyList<ServerImageFileInfo>> LoadServerImagesAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default);

    Task<ServerImageFileInfo> ImportImageAsync(
        InstanceProfile profile,
        string sourcePath,
        ServerImageKind kind,
        CancellationToken cancellationToken = default);

    Task DeleteImageAsync(
        InstanceProfile profile,
        ServerImageFileInfo image,
        CancellationToken cancellationToken = default);
}
