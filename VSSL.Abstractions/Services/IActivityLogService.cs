using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
/// </summary>
public interface IActivityLogService
{
    /// <summary>
    ///     Get activity logs from json file
    /// </summary>
    /// <returns>activity logs</returns>
    List<ActivityLog> GetActivityLogsAsync();
}
