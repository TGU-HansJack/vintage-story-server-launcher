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

    private static string PreferencesPath => Path.Combine(WorkspacePathHelper.WorkspaceRoot, "launcher-preferences.json");

    /// <inheritdoc />
    public LauncherPreferences Load()
    {
        WorkspacePathHelper.EnsureWorkspace();
        if (!File.Exists(PreferencesPath))
            return new LauncherPreferences();

        try
        {
            var json = File.ReadAllText(PreferencesPath);
            var parsed = JsonSerializer.Deserialize<LauncherPreferences>(json, JsonOptions) ?? new LauncherPreferences();
            return Normalize(parsed);
        }
        catch
        {
            return new LauncherPreferences();
        }
    }

    /// <inheritdoc />
    public void Save(LauncherPreferences preferences)
    {
        WorkspacePathHelper.EnsureWorkspace();
        var normalized = Normalize(preferences);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        File.WriteAllText(PreferencesPath, json);
    }

    private static LauncherPreferences Normalize(LauncherPreferences preferences)
    {
        return new LauncherPreferences
        {
            IsOnboardingCompleted = preferences.IsOnboardingCompleted,
            IsDarkMode = preferences.IsDarkMode,
            Language = string.IsNullOrWhiteSpace(preferences.Language)
                ? CultureInfo.CurrentCulture.Name
                : preferences.Language.Trim()
        };
    }
}
