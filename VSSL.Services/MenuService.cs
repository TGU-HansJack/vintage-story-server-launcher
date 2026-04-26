using System.Text.Json;
using VSSL.Abstractions.Services;
using VSSL.Domains;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     Menu service impl
/// </summary>
public class MenuService : IMenuService
{
    /// <inheritdoc />
    public List<MenuItem> GetMenuItems()
    {
        var menuJsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "menus.json");
        var menusJsonStr = File.ReadAllText(menuJsonFilePath);
        return JsonSerializer.Deserialize<List<MenuItem>>(menusJsonStr,
            AppJsonSerializerContext.Default.ListMenuItem) ?? [];
    }
}
