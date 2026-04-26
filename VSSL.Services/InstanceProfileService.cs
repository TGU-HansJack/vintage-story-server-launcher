using System.Text.Json;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;
using System.IO.Compression;

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
    private string WorkspaceRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VSSL",
        "workspace");

    private string ProfilesIndexPath => Path.Combine(WorkspaceRoot, "profiles.json");

    private string ServersRoot => Path.Combine(WorkspaceRoot, "servers", "windows");

    private string PackagesRoot => Path.Combine(WorkspaceRoot, "packages");

    private string TempRoot => Path.Combine(WorkspaceRoot, ".tmp");

    private string DataRoot => Path.Combine(WorkspaceRoot, "data");

    /// <inheritdoc />
    public string GetWorkspaceRoot()
    {
        EnsureWorkspace();
        return WorkspaceRoot;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetInstalledVersions()
    {
        EnsureWorkspace();
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(PackagesRoot)) return [];

        foreach (var zipFilePath in Directory.GetFiles(PackagesRoot, "vs_server_win-x64_*.zip",
                     SearchOption.AllDirectories))
        {
            var version = TryExtractVersionFromPackageName(Path.GetFileName(zipFilePath));
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
        EnsureWorkspace();
        var index = ReadProfileIndex();
        return index.Profiles
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToList();
    }

    /// <inheritdoc />
    public InstanceProfile CreateProfile(string profileName, string version)
    {
        EnsureWorkspace();
        if (string.IsNullOrWhiteSpace(profileName))
            throw new InvalidOperationException("档案名称不能为空。");
        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException("请先选择已安装服务端版本。");

        var selectedVersion = version.Trim();
        var installPath = Path.Combine(ServersRoot, selectedVersion);
        if (!Directory.Exists(installPath))
            installPath = EnsureVersionInstalledFromPackage(selectedVersion);

        var profileId = Guid.NewGuid().ToString("N");
        var profileDirectory = Path.Combine(DataRoot, profileId);
        Directory.CreateDirectory(profileDirectory);
        Directory.CreateDirectory(Path.Combine(profileDirectory, "Saves"));
        Directory.CreateDirectory(Path.Combine(profileDirectory, "Mods"));
        Directory.CreateDirectory(Path.Combine(profileDirectory, "Logs"));

        var profile = new InstanceProfile
        {
            Id = profileId,
            Name = profileName.Trim(),
            Version = selectedVersion,
            DirectoryPath = profileDirectory,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var index = ReadProfileIndex();
        index.Profiles.Add(profile);
        WriteProfileIndex(index);
        return profile;
    }

    /// <inheritdoc />
    public int DeleteProfiles(IReadOnlyCollection<string> profileIds)
    {
        EnsureWorkspace();
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
            TryDeleteProfileDirectory(profile.DirectoryPath, profile.Id);

        return deletingProfiles.Count;
    }

    private void EnsureWorkspace()
    {
        Directory.CreateDirectory(WorkspaceRoot);
        Directory.CreateDirectory(ServersRoot);
        Directory.CreateDirectory(PackagesRoot);
        Directory.CreateDirectory(TempRoot);
        Directory.CreateDirectory(DataRoot);
    }

    private InstanceProfileIndex ReadProfileIndex()
    {
        if (!File.Exists(ProfilesIndexPath)) return new InstanceProfileIndex();

        try
        {
            var jsonText = File.ReadAllText(ProfilesIndexPath);
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
        File.WriteAllText(ProfilesIndexPath, jsonText);
    }

    private void TryDeleteProfileDirectory(string? directoryPath, string profileId)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) return;

        string fullPath;
        string dataRootFullPath;
        try
        {
            fullPath = Path.GetFullPath(directoryPath);
            dataRootFullPath = Path.GetFullPath(DataRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return;
        }

        if (!Directory.Exists(fullPath)) return;

        var normalizedFullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!normalizedFullPath.StartsWith(dataRootFullPath, StringComparison.OrdinalIgnoreCase)) return;

        var segments = normalizedFullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!segments.Any(segment => segment.Equals(profileId, StringComparison.OrdinalIgnoreCase))) return;

        Directory.Delete(normalizedFullPath, recursive: true);
    }

    private string EnsureVersionInstalledFromPackage(string version)
    {
        var installPath = Path.Combine(ServersRoot, version);
        if (Directory.Exists(installPath)) return installPath;

        var packagePath = FindPackagePathByVersion(version);
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new InvalidOperationException($"未找到版本 {version} 的已下载包（packages）。");

        var tempInstallRoot = Path.Combine(TempRoot, $"install-{version}-{Guid.NewGuid():N}");
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
        if (!Directory.Exists(PackagesRoot)) return null;

        return Directory
            .GetFiles(PackagesRoot, "vs_server_win-x64_*.zip", SearchOption.AllDirectories)
            .Select(path => new
            {
                Path = path,
                Version = TryExtractVersionFromPackageName(Path.GetFileName(path)),
                LastWriteTime = File.GetLastWriteTimeUtc(path)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Version) &&
                           item.Version!.Equals(version, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.LastWriteTime)
            .Select(item => item.Path)
            .FirstOrDefault();
    }

    private static string? TryExtractVersionFromPackageName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        const string prefix = "vs_server_win-x64_";
        const string suffix = ".zip";
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return null;

        var version = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - suffix.Length).Trim();
        return string.IsNullOrWhiteSpace(version) ? null : version;
    }
}
