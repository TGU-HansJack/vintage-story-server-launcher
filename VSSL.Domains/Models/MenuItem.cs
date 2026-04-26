using VSSL.Domains.Enums;

namespace VSSL.Domains.Models;

/// <summary>
///     菜单项 model
/// </summary>
public class MenuItem
{
    /// <summary>
    ///     menu title resources key
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    ///     menu icon filename
    ///     <remarks>one of VSSL.Ui/Assets/Icons/MaterialSymbols/*.svg file</remarks>
    /// </summary>
    public required string Icon { get; init; }

    /// <summary>
    ///     a key of VSSL.Ui.Services.Ui.DefaultNavigationService.ViewMappings field
    ///     <remarks>Sidebar menu is a dir, so ViewName is nullable</remarks>
    /// </summary>
    public ViewName? ViewName { get; init; }

    /// <summary>
    ///     Sub menus
    /// </summary>
    public List<MenuItem> Children { get; set; } = [];
}
