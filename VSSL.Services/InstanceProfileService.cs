using System.IO.Compression;
using System.Text.Json;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     实例档案服务默认实现
/// </summary>
public class InstanceProfileService : IInstanceProfileService
{
    private static readonly object IndexFileLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <inheritdoc />
    public string GetWorkspaceRoot()
    {
        WorkspacePathHelper.EnsureWorkspace();
        return WorkspacePathHelper.WorkspaceRoot;
    }

    /// <inheritdoc />
    public string GetDefaultWorkspaceRoot()
    {
        return WorkspacePathHelper.DefaultWorkspaceRoot;
    }

    /// <inheritdoc />
    public string GetDefaultSaveFilePath(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new InvalidOperationException("profileId 不能为空。");

        return WorkspacePathHelper.GetProfileDefaultSaveFile(profileId.Trim());
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetInstalledVersions()
    {
        WorkspacePathHelper.EnsureWorkspace();
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(WorkspacePathHelper.PackagesRoot)) return [];

        foreach (var zipFilePath in Directory.GetFiles(WorkspacePathHelper.PackagesRoot, "vs_server_win-x64_*.zip",
                     SearchOption.AllDirectories))
        {
            var version = WorkspacePathHelper.TryExtractVersionFromPackageName(Path.GetFileName(zipFilePath));
            if (!string.IsNullOrWhiteSpace(version))
                versions.Add(version);
        }

        return versions
            .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<InstanceProfile> GetProfiles()
    {
        WorkspacePathHelper.EnsureWorkspace();
        lock (IndexFileLock)
        {
            var index = ReadProfileIndex();
            var normalized = DiscoverProfilesFromWorkspace(index);
            normalized |= DeduplicateProfiles(index);
            foreach (var profile in index.Profiles)
                normalized |= NormalizeProfile(profile);
            if (normalized) WriteProfileIndex(index);

            return index.Profiles
                .OrderByDescending(p => p.LastUpdatedUtc)
                .ToList();
        }
    }

    /// <inheritdoc />
    public InstanceProfile? GetProfileById(string profileId)
    {
        WorkspacePathHelper.EnsureWorkspace();
        if (string.IsNullOrWhiteSpace(profileId)) return null;

        lock (IndexFileLock)
        {
            var index = ReadProfileIndex();
            var normalized = DeduplicateProfiles(index);
            var profile = index.Profiles.FirstOrDefault(x => x.Id.Equals(profileId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (profile is null)
            {
                if (normalized) WriteProfileIndex(index);
                return null;
            }

            normalized |= NormalizeProfile(profile);
            if (normalized) WriteProfileIndex(index);
            return profile;
        }
    }

    /// <inheritdoc />
    public InstanceProfile CreateProfile(string profileName, string version)
    {
        WorkspacePathHelper.EnsureWorkspace();
        if (string.IsNullOrWhiteSpace(profileName))
            throw new InvalidOperationException("档案名称不能为空。");
        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException("请先选择已安装服务端版本。");

        var selectedVersion = version.Trim();
        var installPath = Path.Combine(WorkspacePathHelper.ServersRoot, selectedVersion);
        if (!Directory.Exists(installPath))
            installPath = EnsureVersionInstalledFromPackage(selectedVersion);

        lock (IndexFileLock)
        {
            var profileId = Guid.NewGuid().ToString("N");
            var profileDirectory = WorkspacePathHelper.GetProfileDataPath(profileId);
            var saveDirectory = WorkspacePathHelper.GetProfileSavesPath(profileId);
            var defaultSaveFile = WorkspacePathHelper.GetProfileDefaultSaveFile(profileId);

            Directory.CreateDirectory(profileDirectory);
            Directory.CreateDirectory(saveDirectory);
            Directory.CreateDirectory(Path.Combine(profileDirectory, "Mods"));
            Directory.CreateDirectory(Path.Combine(profileDirectory, "Logs"));

            var profile = new InstanceProfile
            {
                Id = profileId,
                Name = profileName.Trim(),
                Version = selectedVersion,
                DirectoryPath = profileDirectory,
                ActiveSaveFile = defaultSaveFile,
                SaveDirectory = saveDirectory,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };

            EnsureServerConfig(profile, installPath);
            NormalizeProfile(profile);

            var index = ReadProfileIndex();
            DeduplicateProfiles(index);
            if (index.Profiles.All(x => !x.Id.Equals(profile.Id, StringComparison.OrdinalIgnoreCase)))
                index.Profiles.Add(profile);
            WriteProfileIndex(index);

            return profile;
        }
    }

    /// <inheritdoc />
    public void UpdateProfile(InstanceProfile profile)
    {
        WorkspacePathHelper.EnsureWorkspace();
        if (string.IsNullOrWhiteSpace(profile.Id))
            throw new InvalidOperationException("档案 Id 不能为空。");

        lock (IndexFileLock)
        {
            var index = ReadProfileIndex();
            DeduplicateProfiles(index);
            var current = index.Profiles.FirstOrDefault(x => x.Id.Equals(profile.Id, StringComparison.OrdinalIgnoreCase));
            if (current is null)
                throw new InvalidOperationException("档案不存在。");

            current.Name = profile.Name.Trim();
            current.Version = profile.Version.Trim();
            current.DirectoryPath = profile.DirectoryPath;
            current.ActiveSaveFile = profile.ActiveSaveFile;
            current.SaveDirectory = profile.SaveDirectory;
            current.LastUpdatedUtc = DateTimeOffset.UtcNow;

            NormalizeProfile(current);
            WriteProfileIndex(index);
        }
    }

    /// <inheritdoc />
    public int DeleteProfiles(IReadOnlyCollection<string> profileIds)
    {
        WorkspacePathHelper.EnsureWorkspace();
        if (profileIds.Count == 0) return 0;

        var profileIdSet = new HashSet<string>(
            profileIds.Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.OrdinalIgnoreCase);
        if (profileIdSet.Count == 0) return 0;

        List<InstanceProfile> deletingProfiles;
        lock (IndexFileLock)
        {
            var index = ReadProfileIndex();
            DeduplicateProfiles(index);
            deletingProfiles = index.Profiles
                .Where(profile => profileIdSet.Contains(profile.Id))
                .ToList();
            if (deletingProfiles.Count == 0) return 0;

            index.Profiles = index.Profiles
                .Where(profile => !profileIdSet.Contains(profile.Id))
                .ToList();
            WriteProfileIndex(index);
        }

        foreach (var profile in deletingProfiles)
        {
            TryDeleteProfileDirectory(profile.DirectoryPath, profile.Id);
            TryDeleteProfileDirectory(profile.SaveDirectory, profile.Id);

            var activeSaveDirectory = Path.GetDirectoryName(profile.ActiveSaveFile);
            if (!string.IsNullOrWhiteSpace(activeSaveDirectory))
                TryDeleteProfileDirectory(activeSaveDirectory, profile.Id);
        }

        return deletingProfiles.Count;
    }

    private InstanceProfileIndex ReadProfileIndex()
    {
        if (!File.Exists(WorkspacePathHelper.ProfilesIndexPath)) return new InstanceProfileIndex();

        try
        {
            var jsonText = File.ReadAllText(WorkspacePathHelper.ProfilesIndexPath);
            return JsonSerializer.Deserialize<InstanceProfileIndex>(jsonText, JsonOptions) ?? new InstanceProfileIndex();
        }
        catch
        {
            return new InstanceProfileIndex();
        }
    }

    private void WriteProfileIndex(InstanceProfileIndex index)
    {
        var jsonText = JsonSerializer.Serialize(index, JsonOptions);
        File.WriteAllText(WorkspacePathHelper.ProfilesIndexPath, jsonText);
    }

    private bool DiscoverProfilesFromWorkspace(InstanceProfileIndex index)
    {
        if (!Directory.Exists(WorkspacePathHelper.DataRoot))
            return false;

        var changed = false;
        var existingIds = new HashSet<string>(
            index.Profiles
                .Where(profile => !string.IsNullOrWhiteSpace(profile.Id))
                .Select(profile => profile.Id),
            StringComparer.OrdinalIgnoreCase);

        var fallbackVersion = index.Profiles
            .Select(profile => profile.Version)
            .FirstOrDefault(version => !string.IsNullOrWhiteSpace(version))
            ?? ResolveDefaultVersion();

        foreach (var profileDirectory in Directory.GetDirectories(WorkspacePathHelper.DataRoot))
        {
            var profileId = Path.GetFileName(profileDirectory)?.Trim();
            if (string.IsNullOrWhiteSpace(profileId))
                continue;

            if (!existingIds.Add(profileId))
                continue;

            index.Profiles.Add(BuildRecoveredProfile(profileId, profileDirectory, fallbackVersion));
            changed = true;
        }

        return changed;
    }

    private static bool DeduplicateProfiles(InstanceProfileIndex index)
    {
        if (index.Profiles.Count <= 1)
            return false;

        var grouped = index.Profiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Id))
            .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!grouped.Any(group => group.Count() > 1))
            return false;

        var deduplicated = new List<InstanceProfile>(index.Profiles.Count);
        var handledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in index.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id))
            {
                deduplicated.Add(profile);
                continue;
            }

            if (!handledIds.Add(profile.Id))
                continue;

            var candidates = grouped
                .First(group => group.Key.Equals(profile.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var preferred = SelectPreferredProfile(candidates);
            deduplicated.Add(preferred);
        }

        index.Profiles = deduplicated;
        return true;
    }

    private static InstanceProfile SelectPreferredProfile(IReadOnlyList<InstanceProfile> candidates)
    {
        if (candidates.Count == 1)
            return candidates[0];

        var nonRecovered = candidates
            .Where(profile => !profile.Name.StartsWith("Recovered-", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var pool = nonRecovered.Count > 0 ? nonRecovered : candidates.ToList();

        return pool
            .OrderByDescending(profile => profile.LastUpdatedUtc)
            .ThenByDescending(profile => profile.CreatedAtUtc)
            .First();
    }

    private static InstanceProfile BuildRecoveredProfile(string profileId, string profileDirectory, string fallbackVersion)
    {
        var (configuredSavePath, configuredServerName) = ReadServerConfigSnapshot(profileDirectory);
        var saveDirectoryFromConfig = string.IsNullOrWhiteSpace(configuredSavePath)
            ? string.Empty
            : Path.GetDirectoryName(configuredSavePath) ?? string.Empty;

        var legacySaveDirectory = Path.Combine(profileDirectory, "Saves");
        var newSaveDirectory = WorkspacePathHelper.GetProfileSavesPath(profileId);
        var saveDirectory = !string.IsNullOrWhiteSpace(saveDirectoryFromConfig)
            ? saveDirectoryFromConfig
            : Directory.Exists(legacySaveDirectory)
                ? legacySaveDirectory
                : Directory.Exists(newSaveDirectory)
                    ? newSaveDirectory
                    : legacySaveDirectory;

        var activeSaveFile = ResolveActiveSaveFile(profileId, saveDirectory, configuredSavePath);
        var profileName = string.IsNullOrWhiteSpace(configuredServerName) ||
                          configuredServerName.Equals("Vintage Story Server", StringComparison.OrdinalIgnoreCase)
            ? $"Recovered-{profileId[..Math.Min(6, profileId.Length)]}"
            : configuredServerName.Trim();

        var createdAt = SafeGetUtc(() => Directory.GetCreationTimeUtc(profileDirectory));
        var updatedAt = SafeGetUtc(() => Directory.GetLastWriteTimeUtc(profileDirectory));

        return new InstanceProfile
        {
            Id = profileId,
            Name = profileName,
            Version = fallbackVersion,
            DirectoryPath = profileDirectory,
            SaveDirectory = saveDirectory,
            ActiveSaveFile = activeSaveFile,
            CreatedAtUtc = createdAt,
            LastUpdatedUtc = updatedAt
        };
    }

    private void TryDeleteProfileDirectory(string? directoryPath, string profileId)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) return;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(directoryPath);
        }
        catch
        {
            return;
        }

        if (!Directory.Exists(fullPath)) return;

        var normalizedFullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var dataRootFullPath = Path.GetFullPath(WorkspacePathHelper.DataRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var savesRootFullPath = Path.GetFullPath(WorkspacePathHelper.SavesRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var inDataRoot = normalizedFullPath.StartsWith(dataRootFullPath, StringComparison.OrdinalIgnoreCase);
        var inSavesRoot = normalizedFullPath.StartsWith(savesRootFullPath, StringComparison.OrdinalIgnoreCase);
        if (!inDataRoot && !inSavesRoot) return;

        var profileRoot = Path.GetFullPath(Path.Combine(WorkspacePathHelper.DataRoot, profileId))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var profileSavesRoot = Path.GetFullPath(Path.Combine(WorkspacePathHelper.SavesRoot, profileId))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (normalizedFullPath.Equals(profileRoot, StringComparison.OrdinalIgnoreCase) ||
            normalizedFullPath.Equals(profileSavesRoot, StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(normalizedFullPath, recursive: true);
            return;
        }

        var segments = normalizedFullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!segments.Any(segment => segment.Equals(profileId, StringComparison.OrdinalIgnoreCase))) return;

        Directory.Delete(normalizedFullPath, recursive: true);
    }

    private string EnsureVersionInstalledFromPackage(string version)
    {
        var installPath = Path.Combine(WorkspacePathHelper.ServersRoot, version);
        if (Directory.Exists(installPath)) return installPath;

        var packagePath = FindPackagePathByVersion(version);
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new InvalidOperationException($"未找到版本 {version} 的已下载包（packages）。");

        var tempInstallRoot = Path.Combine(WorkspacePathHelper.TempRoot, $"install-{version}-{Guid.NewGuid():N}");
        var tempExtractRoot = Path.Combine(tempInstallRoot, "extract");
        Directory.CreateDirectory(tempExtractRoot);

        try
        {
            ZipFile.ExtractToDirectory(packagePath, tempExtractRoot, overwriteFiles: true);
            Directory.Move(tempExtractRoot, installPath);
            return installPath;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"从包安装版本 {version} 失败：{ex.Message}", ex);
        }
        finally
        {
            if (Directory.Exists(tempInstallRoot))
                Directory.Delete(tempInstallRoot, recursive: true);
        }
    }

    private string? FindPackagePathByVersion(string version)
    {
        if (!Directory.Exists(WorkspacePathHelper.PackagesRoot)) return null;

        return Directory
            .GetFiles(WorkspacePathHelper.PackagesRoot, "vs_server_win-x64_*.zip", SearchOption.AllDirectories)
            .Select(path => new
            {
                Path = path,
                Version = WorkspacePathHelper.TryExtractVersionFromPackageName(Path.GetFileName(path)),
                LastWriteTime = File.GetLastWriteTimeUtc(path)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Version) &&
                           item.Version!.Equals(version, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.LastWriteTime)
            .Select(item => item.Path)
            .FirstOrDefault();
    }

    private static bool NormalizeProfile(InstanceProfile profile)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            profile.Id = Guid.NewGuid().ToString("N");
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(profile.DirectoryPath))
        {
            profile.DirectoryPath = WorkspacePathHelper.GetProfileDataPath(profile.Id);
            changed = true;
        }
        else
        {
            var fullProfilePath = SafeGetFullPath(profile.DirectoryPath);
            if (!string.IsNullOrWhiteSpace(fullProfilePath) &&
                !fullProfilePath.Equals(profile.DirectoryPath, StringComparison.OrdinalIgnoreCase))
            {
                profile.DirectoryPath = fullProfilePath;
                changed = true;
            }
        }

        var (configuredSavePath, configuredServerName) = ReadServerConfigSnapshot(profile.DirectoryPath);
        var legacySaveDirectory = Path.Combine(profile.DirectoryPath, "Saves");
        var newSaveDirectory = WorkspacePathHelper.GetProfileSavesPath(profile.Id);
        var configuredSaveDirectory = string.IsNullOrWhiteSpace(configuredSavePath)
            ? string.Empty
            : Path.GetDirectoryName(configuredSavePath) ?? string.Empty;

        var saveDirectory = !string.IsNullOrWhiteSpace(configuredSaveDirectory)
            ? configuredSaveDirectory
            : profile.SaveDirectory;
        if (string.IsNullOrWhiteSpace(saveDirectory))
        {
            saveDirectory = Directory.Exists(legacySaveDirectory)
                    ? legacySaveDirectory
                    : Directory.Exists(newSaveDirectory)
                        ? newSaveDirectory
                        : legacySaveDirectory;
        }

        var fullSaveDirectory = SafeGetFullPath(saveDirectory);
        if (!string.IsNullOrWhiteSpace(fullSaveDirectory) &&
            !fullSaveDirectory.Equals(profile.SaveDirectory, StringComparison.OrdinalIgnoreCase))
        {
            profile.SaveDirectory = fullSaveDirectory;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(profile.SaveDirectory))
        {
            profile.SaveDirectory = WorkspacePathHelper.GetProfileSavesPath(profile.Id);
            changed = true;
        }

        var activeSaveFile = profile.ActiveSaveFile;
        if (!string.IsNullOrWhiteSpace(configuredSavePath))
            activeSaveFile = configuredSavePath;

        if (string.IsNullOrWhiteSpace(activeSaveFile))
            activeSaveFile = ResolveActiveSaveFile(profile.Id, profile.SaveDirectory, configuredSavePath);

        var fullActiveSaveFile = SafeGetFullPath(activeSaveFile);
        if (!string.IsNullOrWhiteSpace(fullActiveSaveFile) &&
            !fullActiveSaveFile.Equals(profile.ActiveSaveFile, StringComparison.OrdinalIgnoreCase))
        {
            profile.ActiveSaveFile = fullActiveSaveFile;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = string.IsNullOrWhiteSpace(configuredServerName)
                ? $"Profile-{profile.Id[..Math.Min(6, profile.Id.Length)]}"
                : configuredServerName.Trim();
            changed = true;
        }

        if (profile.LastUpdatedUtc == default)
        {
            profile.LastUpdatedUtc = profile.CreatedAtUtc == default ? DateTimeOffset.UtcNow : profile.CreatedAtUtc;
            changed = true;
        }

        return changed;
    }

    private static string ResolveActiveSaveFile(string profileId, string saveDirectory, string configuredSavePath)
    {
        if (!string.IsNullOrWhiteSpace(configuredSavePath))
            return configuredSavePath;

        if (!string.IsNullOrWhiteSpace(saveDirectory) && Directory.Exists(saveDirectory))
        {
            var currentFile = Directory
                .EnumerateFiles(saveDirectory, "*.vcdbs", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(currentFile))
                return currentFile;
        }

        return WorkspacePathHelper.GetProfileDefaultSaveFile(profileId);
    }

    private static (string SaveFileLocation, string ServerName) ReadServerConfigSnapshot(string profileDirectory)
    {
        var configPath = WorkspacePathHelper.GetProfileConfigPath(profileDirectory);
        if (!File.Exists(configPath))
            return (string.Empty, string.Empty);

        try
        {
            using var stream = File.OpenRead(configPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            var serverName = root.TryGetProperty("ServerName", out var serverNameElement) &&
                             serverNameElement.ValueKind == JsonValueKind.String
                ? serverNameElement.GetString() ?? string.Empty
                : string.Empty;

            if (root.TryGetProperty("WorldConfig", out var worldConfigElement) &&
                worldConfigElement.ValueKind == JsonValueKind.Object &&
                worldConfigElement.TryGetProperty("SaveFileLocation", out var saveFileElement) &&
                saveFileElement.ValueKind == JsonValueKind.String)
            {
                return (saveFileElement.GetString() ?? string.Empty, serverName);
            }

            return (string.Empty, serverName);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private static string ResolveDefaultVersion()
    {
        if (!Directory.Exists(WorkspacePathHelper.ServersRoot))
            return string.Empty;

        return Directory.GetDirectories(WorkspacePathHelper.ServersRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? string.Empty;
    }

    private static string SafeGetFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static DateTimeOffset SafeGetUtc(Func<DateTime> readDateTimeUtc)
    {
        try
        {
            var value = readDateTimeUtc();
            return value == default
                ? DateTimeOffset.UtcNow
                : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
        }
        catch
        {
            return DateTimeOffset.UtcNow;
        }
    }

    private static void EnsureServerConfig(InstanceProfile profile, string installPath)
    {
        ServerConfigBootstrapper.EnsureGenerated(installPath, profile.DirectoryPath);
    }
}
