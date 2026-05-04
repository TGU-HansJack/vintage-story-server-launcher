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

    private static string PreferencesPath => Path.Combine(WorkspacePathHelper.DefaultWorkspaceRoot, "launcher-preferences.json");

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
            return normalized;
        }
        catch
        {
            var fallback = new LauncherPreferences();
            WorkspacePathHelper.SetWorkspaceRoot(fallback.WorkspaceRoot);
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
            WorkspaceRoot = workspaceRoot
        };
    }
}
