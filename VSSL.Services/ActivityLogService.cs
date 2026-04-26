using System.Text.Json;
using VSSL.Abstractions.Services;
using VSSL.Domains;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
/// </summary>
public class ActivityLogService : IActivityLogService
{
    /// <inheritdoc />
    public List<ActivityLog> GetActivityLogsAsync()
    {
        var jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "activity-logs.json");
        var activityLogsJsonStr = File.ReadAllText(jsonFilePath);
        return JsonSerializer.Deserialize<List<ActivityLog>>(activityLogsJsonStr,
            AppJsonSerializerContext.Default.ListActivityLog) ?? [];
    }
}
