using System.Globalization;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     配置下拉项（保留原始值 + 本地化显示）
/// </summary>
public class ConfigChoiceOptionViewModel : ViewModelBase
{
    public required string Value { get; init; }

    public required string LabelZh { get; init; }

    public required string LabelEn { get; init; }

    public string Label => SelectLocalized(LabelZh, LabelEn);

    private static string SelectLocalized(string? zh, string? en)
    {
        var useZh = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        if (useZh)
            return string.IsNullOrWhiteSpace(zh) ? en ?? string.Empty : zh;

        return string.IsNullOrWhiteSpace(en) ? zh ?? string.Empty : en;
    }
}
