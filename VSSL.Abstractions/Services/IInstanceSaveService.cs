using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
///     实例存档服务
/// </summary>
public interface IInstanceSaveService
{
    Task<IReadOnlyList<SaveFileEntry>> GetSavesAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default);

    Task<string> CreateSaveAsync(
        InstanceProfile profile,
        string saveName,
        CancellationToken cancellationToken = default);

    Task<string> BackupActiveSaveAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default);

    Task SetActiveSaveAsync(
        InstanceProfile profile,
        string saveFilePath,
        CancellationToken cancellationToken = default);

    Task<int> DeleteSavesAsync(
        InstanceProfile profile,
        IReadOnlyCollection<string> saveFilePaths,
        CancellationToken cancellationToken = default);
}
