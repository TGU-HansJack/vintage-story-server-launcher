using Avalonia.Controls;
using VSSL.Abstractions.Services.Ui;
using VSSL.Ui.Messages;
using VSSL.Ui.Views;
using CommunityToolkit.Mvvm.Messaging;

namespace VSSL.Ui.Services.Ui;

/// <summary>
///     主窗口管理服务实现
/// </summary>
public class MainWindowService(Lazy<MainWindow> mainWindow, IMessenger messenger) : IMainWindowService
{
    /// <summary>
    ///     主窗口是否处于最大化
    /// </summary>
    private bool IsMaximized { get; set; }

    /// <inheritdoc />
    public void Minimize()
    {
        mainWindow.Value.WindowState = WindowState.Minimized;
    }

    /// <inheritdoc />
    public void ToggleMaximize()
    {
        mainWindow.Value.WindowState = mainWindow.Value.WindowState switch
        {
            WindowState.Normal => WindowState.Maximized,
            WindowState.Maximized => WindowState.Normal,
            _ => mainWindow.Value.WindowState
        };
        IsMaximized = mainWindow
            .Value.WindowState == WindowState.Maximized;

        // 发布窗口最大化状态改变消息
        messenger.Send(new MainWindowStateChangedMessage(IsMaximized));
    }

    /// <inheritdoc />
    public void Close()
    {
        mainWindow.Value.Close();
    }
}