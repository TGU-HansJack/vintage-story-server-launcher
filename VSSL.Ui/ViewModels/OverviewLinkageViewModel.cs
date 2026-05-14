using VSSL.Abstractions.Services;
using VSSL.Abstractions.Services.Ui;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     总览-联结独立页面（复用 HomeViewModel 的联结能力）
/// </summary>
public class OverviewLinkageViewModel : HomeViewModel
{
    #region Constructors

    public OverviewLinkageViewModel()
    {
    }

    public OverviewLinkageViewModel(ISystemStatusService systemStatusService, IOverviewLinkageService overviewLinkageService)
        : base(systemStatusService, overviewLinkageService)
    {
    }

    public OverviewLinkageViewModel(ISystemStatusService systemStatusService, IThemeService themeService,
        IOverviewLinkageService overviewLinkageService)
        : base(systemStatusService, themeService, overviewLinkageService)
    {
    }

    #endregion
}
