using System.Globalization;
using System.Text.Json;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     启动器偏好设置服务
/// </summary>
public class LauncherPreferencesService : ILauncherPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILauncherStartupService? _launcherStartupService;

    private static string PreferencesPath => Path.Combine(WorkspacePathHelper.DefaultWorkspaceRoot, "launcher-preferences.json");

    public LauncherPreferencesService(ILauncherStartupService? launcherStartupService = null)
    {
        _launcherStartupService = launcherStartupService;
    }

    /// <inheritdoc />
    public LauncherPreferences Load()
    {
        Directory.CreateDirectory(WorkspacePathHelper.DefaultWorkspaceRoot);
        if (!File.Exists(PreferencesPath))
        {
            var empty = new LauncherPreferences();
            WorkspacePathHelper.SetWorkspaceRoot(empty.WorkspaceRoot);
            return empty;
        }

        try
        {
            var json = File.ReadAllText(PreferencesPath);
            var parsed = JsonSerializer.Deserialize<LauncherPreferences>(json, JsonOptions) ?? new LauncherPreferences();
            var normalized = Normalize(parsed);
            WorkspacePathHelper.SetWorkspaceRoot(normalized.WorkspaceRoot);
            _launcherStartupService?.SetEnabled(normalized.StartWithWindows);
            return normalized;
        }
        catch
        {
            var fallback = new LauncherPreferences();
            WorkspacePathHelper.SetWorkspaceRoot(fallback.WorkspaceRoot);
            _launcherStartupService?.SetEnabled(fallback.StartWithWindows);
            return fallback;
        }
    }

    /// <inheritdoc />
    public void Save(LauncherPreferences preferences)
    {
        Directory.CreateDirectory(WorkspacePathHelper.DefaultWorkspaceRoot);
        var normalized = Normalize(preferences);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        File.WriteAllText(PreferencesPath, json);
        WorkspacePathHelper.SetWorkspaceRoot(normalized.WorkspaceRoot);
        _launcherStartupService?.SetEnabled(normalized.StartWithWindows);
    }

    private static LauncherPreferences Normalize(LauncherPreferences preferences)
    {
        var workspaceRoot = string.IsNullOrWhiteSpace(preferences.WorkspaceRoot)
            ? string.Empty
            : Path.GetFullPath(preferences.WorkspaceRoot.Trim());

        return new LauncherPreferences
        {
            IsOnboardingCompleted = preferences.IsOnboardingCompleted,
            IsDarkMode = preferences.IsDarkMode,
            Language = string.IsNullOrWhiteSpace(preferences.Language)
                ? CultureInfo.CurrentCulture.Name
                : preferences.Language.Trim(),
            WorkspaceRoot = workspaceRoot,
            StartWithWindows = preferences.StartWithWindows,
            StartHiddenOnLaunch = preferences.StartHiddenOnLaunch,
            CloseToTrayOnExit = preferences.CloseToTrayOnExit,
            AutoStartServerOnLaunch = preferences.AutoStartServerOnLaunch,
            AutoStartRobotOnLaunch = preferences.AutoStartRobotOnLaunch,
            AutoStartServerProfileId = string.IsNullOrWhiteSpace(preferences.AutoStartServerProfileId)
                ? string.Empty
                : preferences.AutoStartServerProfileId.Trim(),
            QuickCommands = (preferences.QuickCommands ?? [])
                .Select(command => command?.Trim() ?? string.Empty)
                .Where(command => !string.IsNullOrWhiteSpace(command))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(100)
                .ToList()
        };
    }
}
