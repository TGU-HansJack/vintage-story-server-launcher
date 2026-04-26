using Avalonia;
using VSSL.Abstractions.ViewModels;
using VSSL.Ui.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     主窗口 vm
/// </summary>
public partial class MainWindowViewModel : RecipientViewModelBase, IRecipient<ThemeChangedMessage>,
    IRecipient<MainWindowStateChangedMessage>, IRecipient<CurrentPageChangedMessage>, IRecipient<SidebarToggleMessage>,
    IRecipient<SubMenusChangedMessage>
{
    /// <summary>
    ///     当前页面对应的视图模型
    /// </summary>
    [ObservableProperty] private IViewModel? _currentPage;

    /// <summary>
    ///     当前主题是否为暗色主题
    /// </summary>
    [ObservableProperty] private bool _isDarkMode = true;

    /// <summary>
    ///     当前是否全屏
    /// </summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MainWindowPadding))]
    private bool _isMaximized;

    /// <summary>
    ///     侧边栏导航区域展开状态
    /// </summary>
    [ObservableProperty] private bool _isSidebarOpened = true;

    /// <summary>
    ///     当前一级菜单是否有二级菜单
    /// </summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SubMenuPaneLength))]
    private bool _hasSubMenus = true;

    /// <summary>
    ///     主窗口内边距
    /// </summary>
    public Thickness MainWindowPadding => IsMaximized ? new Thickness(8) : new Thickness(0);

    /// <summary>
    ///     二级侧栏宽度，一级菜单无子菜单时收起
    /// </summary>
    public double SubMenuPaneLength => HasSubMenus ? 200 : 0;

    /// <inheritdoc />
    public void Receive(CurrentPageChangedMessage message)
    {
        CurrentPage = message.Value;
    }

    /// <inheritdoc />
    public void Receive(MainWindowStateChangedMessage message)
    {
        IsMaximized = message.Value;
    }

    /// <inheritdoc />
    public void Receive(SidebarToggleMessage message)
    {
        IsSidebarOpened = message.Value;
    }

    /// <inheritdoc />
    public void Receive(SubMenusChangedMessage message)
    {
        HasSubMenus = message.Value.Count > 0;
    }

    /// <inheritdoc />
    public void Receive(ThemeChangedMessage message)
    {
        IsDarkMode = message.Value;
    }
}
