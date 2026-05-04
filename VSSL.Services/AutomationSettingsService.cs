using System.Text.Json;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     自动化设置持久化服务
/// </summary>
public class AutomationSettingsService : IAutomationSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string SettingsPath => Path.Combine(WorkspacePathHelper.WorkspaceRoot, "automation-settings.json");

    public async Task<AutomationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        WorkspacePathHelper.EnsureWorkspace();
        if (!File.Exists(SettingsPath))
            return BuildDefaults();

        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath, cancellationToken);
            var parsed = JsonSerializer.Deserialize<AutomationSettings>(json, JsonOptions) ?? BuildDefaults();
            return Normalize(parsed);
        }
        catch
        {
            return BuildDefaults();
        }
    }

    public async Task SaveAsync(AutomationSettings settings, CancellationToken cancellationToken = default)
    {
        WorkspacePathHelper.EnsureWorkspace();
        var normalized = Normalize(settings);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json, cancellationToken);
    }

    private static AutomationSettings BuildDefaults()
    {
        return new AutomationSettings
        {
            RestartSchedulerEnabled = false,
            BroadcastEnabled = false,
            ExportLogEnabled = false,
            ExportBeforeShutdown = true,
            ExportIncludeChat = true,
            ExportIncludeServerInfo = true,
            TimeWindows =
            [
                new DailyTimeWindow
                {
                    StartTime = "08:00",
                    EndTime = "23:00",
                    Enabled = true
                }
            ],
            BroadcastMessages =
            [
                new ScheduledBroadcastMessage
                {
                    Time = "12:00",
                    Message = "服务器例行播报",
                    Enabled = false
                }
            ],
            ExportTimes = ["05:00"]
        };
    }

    private static AutomationSettings Normalize(AutomationSettings settings)
    {
        var normalizedWindows = (settings.TimeWindows ?? [])
            .Where(window => window is not null)
            .Select(window => new DailyTimeWindow
            {
                StartTime = NormalizeTime(window.StartTime, "08:00"),
                EndTime = NormalizeTime(window.EndTime, "23:00"),
                Enabled = window.Enabled
            })
            .ToList();
        if (normalizedWindows.Count == 0)
        {
            normalizedWindows.Add(new DailyTimeWindow
            {
                StartTime = "08:00",
                EndTime = "23:00",
                Enabled = true
            });
        }

        var normalizedMessages = (settings.BroadcastMessages ?? [])
            .Where(item => item is not null)
            .Select(item => new ScheduledBroadcastMessage
            {
                Time = NormalizeTime(item.Time, "12:00"),
                Message = item.Message?.Trim() ?? string.Empty,
                Enabled = item.Enabled
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Message))
            .ToList();

        var normalizedExportTimes = (settings.ExportTimes ?? [])
            .Select(time => NormalizeTime(time, string.Empty))
            .Where(time => !string.IsNullOrWhiteSpace(time))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AutomationSettings
        {
            TargetProfileId = settings.TargetProfileId?.Trim() ?? string.Empty,
            RestartSchedulerEnabled = settings.RestartSchedulerEnabled,
            TimeWindows = normalizedWindows,
            BroadcastEnabled = settings.BroadcastEnabled,
            BroadcastMessages = normalizedMessages,
            ExportLogEnabled = settings.ExportLogEnabled,
            ExportTimes = normalizedExportTimes,
            ExportBeforeShutdown = settings.ExportBeforeShutdown,
            ExportIncludeChat = settings.ExportIncludeChat,
            ExportIncludeServerInfo = settings.ExportIncludeServerInfo
        };
    }

    private static string NormalizeTime(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return TimeSpan.TryParse(value.Trim(), out var parsed)
            ? $"{parsed.Hours:00}:{parsed.Minutes:00}"
            : fallback;
    }
}
