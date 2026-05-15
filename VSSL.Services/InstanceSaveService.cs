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
                    SizeBytes = fileInfo.Length,
                    LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
                };
            })
            .OrderByDescending(static item => item.LastWriteTimeUtc)
            .ToList();

        if (!string.IsNullOrWhiteSpace(profile.ActiveSaveFile) &&
            profile.ActiveSaveFile.EndsWith(".vcdbs", StringComparison.OrdinalIgnoreCase) &&
            result.All(item => !item.FullPath.Equals(profile.ActiveSaveFile, StringComparison.OrdinalIgnoreCase)))
        {
            var activePath = Path.GetFullPath(profile.ActiveSaveFile);
            var activeDirectory = Path.GetDirectoryName(activePath);
            if (!string.IsNullOrWhiteSpace(activeDirectory) &&
                activeDirectory.Equals(savesPath, StringComparison.OrdinalIgnoreCase))
            {
                var activeEntry = new SaveFileEntry
                {
                    FullPath = activePath,
                    FileName = Path.GetFileName(activePath),
                    SizeBytes = File.Exists(activePath) ? new FileInfo(activePath).Length : 0,
                    LastWriteTimeUtc = File.Exists(activePath)
                        ? new FileInfo(activePath).LastWriteTimeUtc
                        : profile.LastUpdatedUtc
                };
                result = result
                    .Concat([activeEntry])
                    .OrderByDescending(static item => item.LastWriteTimeUtc)
                    .ToList();
            }
        }

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

        profile.SaveDirectory = savesPath;
        await SetActiveSaveAsync(profile, fullPath, cancellationToken);
        return fullPath;
    }

    /// <inheritdoc />
    public Task<string> BackupActiveSaveAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sourcePath = ResolveActiveSavePath(profile);
        if (!File.Exists(sourcePath))
            throw new InvalidOperationException("当前存档文件不存在，无法备份。");

        var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        var backupRoot = ResolveBackupRoot(profile);

        Directory.CreateDirectory(backupRoot);

        var backupName = $"{WorkspacePathHelper.SanitizeFileName(sourceName)}-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.vcdbs";
        var backupPath = Path.Combine(backupRoot, backupName);

        File.Copy(sourcePath, backupPath, overwrite: false);
        return Task.FromResult(backupPath);
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

        // Avoid leaving a zero-byte placeholder save file.
        // Vintage Story can create a proper database on first startup if the file does not exist.
        if (File.Exists(fullPath))
        {
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length == 0)
                File.Delete(fullPath);
        }

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
        var activeSaveDirectory = Path.GetDirectoryName(profile.ActiveSaveFile);
        if (!string.IsNullOrWhiteSpace(activeSaveDirectory))
        {
            if (string.IsNullOrWhiteSpace(profile.SaveDirectory))
                return activeSaveDirectory;

            try
            {
                var configuredDirectory = profile.SaveDirectory;
                var activeHasSaves = Directory.Exists(activeSaveDirectory) &&
                                     Directory.EnumerateFiles(activeSaveDirectory, "*.vcdbs", SearchOption.TopDirectoryOnly).Any();
                var configuredHasSaves = Directory.Exists(configuredDirectory) &&
                                         Directory.EnumerateFiles(configuredDirectory, "*.vcdbs", SearchOption.TopDirectoryOnly).Any();

                if (activeHasSaves && !configuredHasSaves)
                    return activeSaveDirectory;
            }
            catch
            {
                // 回退到 profile.SaveDirectory / 默认目录。
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.SaveDirectory))
            return profile.SaveDirectory;

        if (!string.IsNullOrWhiteSpace(activeSaveDirectory))
            return activeSaveDirectory;

        return WorkspacePathHelper.GetProfileSavesPath(profile.Id);
    }

    private static string ResolveActiveSavePath(InstanceProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.ActiveSaveFile))
        {
            try
            {
                return Path.GetFullPath(profile.ActiveSaveFile);
            }
            catch
            {
                // fall through
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.SaveDirectory))
        {
            try
            {
                var defaultSave = Path.Combine(Path.GetFullPath(profile.SaveDirectory), "default.vcdbs");
                return defaultSave;
            }
            catch
            {
                // fall through
            }
        }

        return WorkspacePathHelper.GetProfileDefaultSaveFile(profile.Id);
    }

    private static string ResolveBackupRoot(InstanceProfile profile)
    {
        var profileDirectory = profile.DirectoryPath;
        if (!string.IsNullOrWhiteSpace(profileDirectory))
        {
            try
            {
                return Path.Combine(Path.GetFullPath(profileDirectory), "Backups");
            }
            catch
            {
                // fall through
            }
        }

        return Path.Combine(WorkspacePathHelper.GetProfileDataPath(profile.Id), "Backups");
    }
}
