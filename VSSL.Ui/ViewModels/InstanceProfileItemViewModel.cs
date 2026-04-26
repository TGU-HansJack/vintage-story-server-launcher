using CommunityToolkit.Mvvm.ComponentModel;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     实例档案列表项视图模型
/// </summary>
public partial class InstanceProfileItemViewModel : ViewModelBase
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string DirectoryPath { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public string CreatedAtText => CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [ObservableProperty] private bool _isSelected;
}
