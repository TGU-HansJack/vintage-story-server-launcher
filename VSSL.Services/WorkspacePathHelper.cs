using System.Text.RegularExpressions;

namespace VSSL.Services;

internal static partial class WorkspacePathHelper
{
    private static string? _workspaceRoot;

    public static string DefaultWorkspaceRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VSSL",
        "workspace");

    public static string WorkspaceRoot => EnsureWorkspaceRoot(_workspaceRoot);

    public static void SetWorkspaceRoot(string? workspaceRoot)
    {
        _workspaceRoot = NormalizeRoot(workspaceRoot);
        EnsureWorkspace();
    }

    public static string GetWorkspaceRootOrDefault(string? workspaceRoot)
    {
        return EnsureWorkspaceRoot(NormalizeRoot(workspaceRoot));
    }

    public static string DataRoot => Path.Combine(WorkspaceRoot, "data");

    public static string SavesRoot => Path.Combine(WorkspaceRoot, "saves");

    public static string ServersRoot => Path.Combine(WorkspaceRoot, "servers", "windows");

    public static string PackagesRoot => Path.Combine(WorkspaceRoot, "packages");

    public static string TempRoot => Path.Combine(WorkspaceRoot, ".tmp");

    public static string ProfilesIndexPath => Path.Combine(WorkspaceRoot, "profiles.json");

    public static string RobotRoot => Path.Combine(WorkspaceRoot, "robot");

    public static string RobotSettingsPath => Path.Combine(RobotRoot, "vs2qq-settings.json");

    public static string GetProfileDataPath(string profileId) => Path.Combine(DataRoot, profileId);

    public static string GetServerInstallPath(string version) => Path.Combine(ServersRoot, version);

    public static string GetProfileSavesPath(string profileId) => Path.Combine(SavesRoot, profileId);

    public static string GetProfileDefaultSaveFile(string profileId) =>
        Path.Combine(GetProfileSavesPath(profileId), "default.vcdbs");

    public static string GetProfileConfigPath(string profileDataPath) =>
        Path.Combine(profileDataPath, "serverconfig.json");

    public static string GetProfileModsPath(string profileDataPath) =>
        Path.Combine(profileDataPath, "Mods");

    public static string GetProfileLogsPath(string profileDataPath) =>
        Path.Combine(profileDataPath, "Logs");

    public static string GetServerMainLogPath(string profileDataPath) =>
        Path.Combine(GetProfileLogsPath(profileDataPath), "server-main.log");

    public static void EnsureWorkspace()
    {
        Directory.CreateDirectory(WorkspaceRoot);
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(SavesRoot);
        Directory.CreateDirectory(ServersRoot);
        Directory.CreateDirectory(PackagesRoot);
        Directory.CreateDirectory(TempRoot);
    }

    public static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join('_', name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized;
    }

    public static string? TryExtractVersionFromPackageName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var match = VersionPattern().Match(fileName);
        return match.Success ? match.Groups["version"].Value.Trim() : null;
    }

    [GeneratedRegex(@"^vs_server_win-x64_(?<version>.+)\.zip$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();

    private static string EnsureWorkspaceRoot(string? workspaceRoot)
    {
        var normalizedRoot = NormalizeRoot(workspaceRoot);
        Directory.CreateDirectory(normalizedRoot);
        return normalizedRoot;
    }

    private static string NormalizeRoot(string? workspaceRoot)
    {
        if (!string.IsNullOrWhiteSpace(workspaceRoot))
        {
            try
            {
                return Path.GetFullPath(workspaceRoot.Trim());
            }
            catch
            {
                // fall back to default root
            }
        }

        return DefaultWorkspaceRoot;
    }
}
