using VSSL.Domains.Enums;

namespace VSSL.Abstractions.Services.Ui;

/// <summary>
///     导航服务
/// </summary>
public interface INavigationService
{
    /// <summary>
    ///     Navigate to specified view~
    /// </summary>
    /// <param name="viewName">view name, is a key of ViewMappings</param>
    void NavigateTo(ViewName? viewName);
}
