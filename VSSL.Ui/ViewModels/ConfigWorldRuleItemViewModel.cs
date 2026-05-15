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

    public IReadOnlyList<string> ChoiceNames { get; init; } = [];

    public IReadOnlyList<ConfigChoiceOptionViewModel> ChoiceOptions { get; init; } = [];

    public string? DescriptionZh { get; init; }

    public string? DescriptionEn { get; init; }

    public string? Description => SelectLocalized(DescriptionZh, DescriptionEn);

    public bool IsOnlyDuringWorldCreate { get; init; }

    public bool IsBoolean => Type == WorldRuleType.Boolean;

    public bool IsChoice => Type == WorldRuleType.Choice;

    public bool IsText => Type is WorldRuleType.Text or WorldRuleType.Number;

    [ObservableProperty] private bool _canEdit = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BoolValue))]
    [NotifyPropertyChangedFor(nameof(SelectedChoiceOption))]
    private string _value = string.Empty;

    public bool BoolValue
    {
        get => ParseBool(Value);
        set => Value = value ? "true" : "false";
    }

    public ConfigChoiceOptionViewModel? SelectedChoiceOption
    {
        get
        {
            if (ChoiceOptions.Count == 0) return null;
            return ChoiceOptions.FirstOrDefault(option =>
                option.Value.Equals(Value, StringComparison.OrdinalIgnoreCase));
        }
        set
        {
            if (value is null) return;
            if (value.Value.Equals(Value, StringComparison.OrdinalIgnoreCase)) return;
            Value = value.Value;
        }
    }

    partial void OnValueChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedChoiceOption));
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
