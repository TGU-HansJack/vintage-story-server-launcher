using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     实例档案服务默认实现
/// </summary>
public class InstanceProfileService : IInstanceProfileService
{
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
        var index = ReadProfileIndex();
        var normalized = false;
        foreach (var profile in index.Profiles)
            normalized |= NormalizeProfile(profile);
        if (normalized) WriteProfileIndex(index);

        return index.Profiles
            .OrderByDescending(p => p.LastUpdatedUtc)
            .ToList();
    }

    /// <inheritdoc />
    public InstanceProfile? GetProfileById(string profileId)
    {
        WorkspacePathHelper.EnsureWorkspace();
        if (string.IsNullOrWhiteSpace(profileId)) return null;

        var index = ReadProfileIndex();
        var profile = index.Profiles.FirstOrDefault(x => x.Id.Equals(profileId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (profile is null) return null;
        if (NormalizeProfile(profile)) WriteProfileIndex(index);
        return profile;
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

        EnsureServerConfig(profile);

        var index = ReadProfileIndex();
        index.Profiles.Add(profile);
        WriteProfileIndex(index);
        return profile;
    }

    /// <inheritdoc />
    public void UpdateProfile(InstanceProfile profile)
    {
        WorkspacePathHelper.EnsureWorkspace();
        if (string.IsNullOrWhiteSpace(profile.Id))
            throw new InvalidOperationException("档案 Id 不能为空。");

        var index = ReadProfileIndex();
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

    /// <inheritdoc />
    public int DeleteProfiles(IReadOnlyCollection<string> profileIds)
    {
        WorkspacePathHelper.EnsureWorkspace();
        if (profileIds.Count == 0) return 0;

        var profileIdSet = new HashSet<string>(
            profileIds.Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.OrdinalIgnoreCase);
        if (profileIdSet.Count == 0) return 0;

        var index = ReadProfileIndex();
        var deletingProfiles = index.Profiles
            .Where(profile => profileIdSet.Contains(profile.Id))
            .ToList();
        if (deletingProfiles.Count == 0) return 0;

        index.Profiles = index.Profiles
            .Where(profile => !profileIdSet.Contains(profile.Id))
            .ToList();
        WriteProfileIndex(index);

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
        if (string.IsNullOrWhiteSpace(profile.DirectoryPath))
        {
            profile.DirectoryPath = WorkspacePathHelper.GetProfileDataPath(profile.Id);
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(profile.SaveDirectory))
        {
            profile.SaveDirectory = WorkspacePathHelper.GetProfileSavesPath(profile.Id);
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(profile.ActiveSaveFile))
        {
            profile.ActiveSaveFile = WorkspacePathHelper.GetProfileDefaultSaveFile(profile.Id);
            changed = true;
        }

        if (profile.LastUpdatedUtc == default)
        {
            profile.LastUpdatedUtc = profile.CreatedAtUtc == default ? DateTimeOffset.UtcNow : profile.CreatedAtUtc;
            changed = true;
        }

        return changed;
    }

    private static void EnsureServerConfig(InstanceProfile profile)
    {
        var configPath = WorkspacePathHelper.GetProfileConfigPath(profile.DirectoryPath);
        var configDirectory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(configDirectory))
            Directory.CreateDirectory(configDirectory);

        if (File.Exists(configPath)) return;

        var root = new JsonObject
        {
            ["ServerName"] = "Vintage Story Server",
            ["Ip"] = null,
            ["Port"] = 42420,
            ["MaxClients"] = 16,
            ["Password"] = null,
            ["AdvertiseServer"] = false,
            ["WhitelistMode"] = 0,
            ["AllowPvP"] = true,
            ["AllowFireSpread"] = true,
            ["AllowFallingBlocks"] = true
        };

        var worldConfig = new JsonObject
        {
            ["Seed"] = "123456789",
            ["WorldName"] = "A new world",
            ["SaveFileLocation"] = profile.ActiveSaveFile,
            ["PlayStyle"] = "surviveandbuild",
            ["WorldType"] = "standard",
            ["MapSizeY"] = 256
        };
        worldConfig["WorldConfiguration"] = new JsonObject
        {
            ["gameMode"] = "survival",
            ["allowMap"] = true,
            ["allowCoordinateHud"] = true,
            ["allowLandClaiming"] = true,
            ["worldWidth"] = 1024000,
            ["worldLength"] = 1024000,
            ["worldEdge"] = "blocked",
            ["snowAccum"] = true
        };
        root["WorldConfig"] = worldConfig;

        File.WriteAllText(configPath, root.ToJsonString(JsonOptions));
    }
}
