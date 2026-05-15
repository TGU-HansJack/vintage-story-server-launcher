using CommunityToolkit.Mvvm.ComponentModel;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     总览-限制 模组项
/// </summary>
public partial class OverviewRestrictionModItemViewModel : ViewModelBase
{
    public required string ModId { get; init; }

    public int SeenCount { get; init; }

    public DateTimeOffset? LastSeenUtc { get; init; }

    public string LastSeenText => LastSeenUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    [ObservableProperty] private bool _isSelected;

    [ObservableProperty] private bool _isBlacklisted;
}
