using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     配置页世界规则项
/// </summary>
public partial class ConfigWorldRuleItemViewModel : ViewModelBase
{
    public required string Key { get; init; }

    public required string LabelZh { get; init; }

    public required string LabelEn { get; init; }

    public string Label => SelectLocalized(LabelZh, LabelEn);

    public required WorldRuleType Type { get; init; }

    public string? DescriptionZh { get; init; }

    public string? DescriptionEn { get; init; }

    public string? Description => SelectLocalized(DescriptionZh, DescriptionEn);

    [ObservableProperty] private string _value = string.Empty;

    private static string SelectLocalized(string? zh, string? en)
    {
        var useZh = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        if (useZh)
            return string.IsNullOrWhiteSpace(zh) ? en ?? string.Empty : zh;

        return string.IsNullOrWhiteSpace(en) ? zh ?? string.Empty : en;
    }
}
