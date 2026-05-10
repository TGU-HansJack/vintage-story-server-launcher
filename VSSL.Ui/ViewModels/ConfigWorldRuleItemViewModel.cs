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

    public IReadOnlyList<string> Choices { get; init; } = [];

    public string? DescriptionZh { get; init; }

    public string? DescriptionEn { get; init; }

    public string? Description => SelectLocalized(DescriptionZh, DescriptionEn);

    public bool IsBoolean => Type == WorldRuleType.Boolean;

    public bool IsChoice => Type == WorldRuleType.Choice;

    public bool IsText => Type is WorldRuleType.Text or WorldRuleType.Number;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BoolValue))]
    private string _value = string.Empty;

    public bool BoolValue
    {
        get => ParseBool(Value);
        set => Value = value ? "true" : "false";
    }

    private static string SelectLocalized(string? zh, string? en)
    {
        var useZh = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        if (useZh)
            return string.IsNullOrWhiteSpace(zh) ? en ?? string.Empty : zh;

        return string.IsNullOrWhiteSpace(en) ? zh ?? string.Empty : en;
    }

    private static bool ParseBool(string? value)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return string.Equals(value?.Trim(), "1", StringComparison.OrdinalIgnoreCase);
    }
}
