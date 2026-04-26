using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     配置页世界规则项
/// </summary>
public partial class ConfigWorldRuleItemViewModel : ViewModelBase
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required WorldRuleType Type { get; init; }

    public string? Description { get; init; }

    [ObservableProperty] private string _value = string.Empty;
}
