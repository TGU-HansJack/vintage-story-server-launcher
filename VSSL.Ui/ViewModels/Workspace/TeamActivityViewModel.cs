using System.Diagnostics;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Ui.ViewModels.Workspace;

/// <summary>
///     ViewModel of <see cref="VSSL.Ui.Controls.Workspace.TeamActivity" />>
/// </summary>
public class TeamActivityViewModel : ViewModelBase
{
    /// <summary>
    ///     Team activities
    /// </summary>
    public List<ActivityLog> ActivityLogs { get; set; } = [];

    #region Constructors

    public TeamActivityViewModel()
    {
        // for preview only
    }

    public TeamActivityViewModel(IActivityLogService activityLogService)
    {
        ActivityLogs = activityLogService.GetActivityLogsAsync();
    }

    #endregion
}
