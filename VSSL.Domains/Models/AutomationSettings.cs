namespace VSSL.Domains.Models;

/// <summary>
///     自动化设置
/// </summary>
public class AutomationSettings
{
    public string TargetProfileId { get; set; } = string.Empty;

    public bool RestartSchedulerEnabled { get; set; }

    public List<DailyTimeWindow> TimeWindows { get; set; } = [];

    public bool BroadcastEnabled { get; set; }

    public List<ScheduledBroadcastMessage> BroadcastMessages { get; set; } = [];

    public bool ExportLogEnabled { get; set; }

    public List<string> ExportTimes { get; set; } = [];

    public bool ExportBeforeShutdown { get; set; } = true;

    public bool ExportIncludeChat { get; set; } = true;

    public bool ExportIncludeServerInfo { get; set; } = true;
}
