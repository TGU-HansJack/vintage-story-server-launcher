using CommunityToolkit.Mvvm.ComponentModel;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     存档列表项视图模型
/// </summary>
public partial class SaveFileItemViewModel : ViewModelBase
{
    public string FullPath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public DateTimeOffset LastWriteTimeUtc { get; init; }

    public string LastWriteText => LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [ObservableProperty] private bool _isSelected;

    [ObservableProperty] private bool _isActive;
}
