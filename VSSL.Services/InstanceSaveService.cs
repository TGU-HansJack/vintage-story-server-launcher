using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     实例存档服务默认实现
/// </summary>
public class InstanceSaveService(IInstanceServerConfigService serverConfigService) : IInstanceSaveService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SaveFileEntry>> GetSavesAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var savesPath = ResolveSavesPath(profile);
        Directory.CreateDirectory(savesPath);

        IReadOnlyList<SaveFileEntry> result = Directory
            .EnumerateFiles(savesPath, "*.vcdbs", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                var fileInfo = new FileInfo(path);
                return new SaveFileEntry
                {
                    FullPath = fileInfo.FullName,
                    FileName = fileInfo.Name,
                    LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
                };
            })
            .OrderByDescending(static item => item.LastWriteTimeUtc)
            .ToList();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public async Task<string> CreateSaveAsync(
        InstanceProfile profile,
        string saveName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(saveName))
            throw new InvalidOperationException("存档名称不能为空。");

        var savesPath = ResolveSavesPath(profile);
        Directory.CreateDirectory(savesPath);

        var fileName = WorkspacePathHelper.SanitizeFileName(saveName.Trim()) + ".vcdbs";
        var fullPath = Path.Combine(savesPath, fileName);

        if (!File.Exists(fullPath))
        {
            await using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            await stream.FlushAsync(cancellationToken);
        }

        profile.SaveDirectory = savesPath;
        await SetActiveSaveAsync(profile, fullPath, cancellationToken);
        return fullPath;
    }

    /// <inheritdoc />
    public async Task SetActiveSaveAsync(
        InstanceProfile profile,
        string saveFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(saveFilePath))
            throw new InvalidOperationException("存档路径不能为空。");

        var fullPath = Path.GetFullPath(saveFilePath);
        var saveDirectory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(saveDirectory))
            throw new InvalidOperationException("无效存档路径。");

        Directory.CreateDirectory(saveDirectory);

        var serverSettings = await serverConfigService.LoadServerSettingsAsync(profile, cancellationToken);
        var worldSettings = await serverConfigService.LoadWorldSettingsAsync(profile, cancellationToken);
        var worldRules = await serverConfigService.LoadWorldRulesAsync(profile, cancellationToken);

        worldSettings.SaveFileLocation = fullPath;
        await serverConfigService.SaveSettingsAsync(profile, serverSettings, worldSettings, worldRules, cancellationToken);

        profile.ActiveSaveFile = fullPath;
        profile.SaveDirectory = saveDirectory;
        profile.LastUpdatedUtc = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc />
    public Task<int> DeleteSavesAsync(
        InstanceProfile profile,
        IReadOnlyCollection<string> saveFilePaths,
        CancellationToken cancellationToken = default)
    {
        if (saveFilePaths.Count == 0) return Task.FromResult(0);

        var saveRoot = Path.GetFullPath(ResolveSavesPath(profile))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var deleted = 0;
        foreach (var filePath in saveFilePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(filePath);
            }
            catch
            {
                continue;
            }

            if (!fullPath.StartsWith(saveRoot, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!fullPath.EndsWith(".vcdbs", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!File.Exists(fullPath))
                continue;

            File.Delete(fullPath);
            deleted++;
        }

        return Task.FromResult(deleted);
    }

    private static string ResolveSavesPath(InstanceProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.SaveDirectory))
            return profile.SaveDirectory;

        var activeSaveDirectory = Path.GetDirectoryName(profile.ActiveSaveFile);
        if (!string.IsNullOrWhiteSpace(activeSaveDirectory))
            return activeSaveDirectory;

        return WorkspacePathHelper.GetProfileSavesPath(profile.Id);
    }
}
