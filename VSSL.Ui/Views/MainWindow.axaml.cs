using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VSSL.Ui.ViewModels;
using Microsoft.Extensions.Logging;

namespace VSSL.Ui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
    }

    public MainWindow(MainWindowViewModel viewModel, ILogger<MainWindow> logger)
    {
        InitializeComponent();
        DataContext = viewModel;
        AddHandler(PointerPressedEvent, OnGlobalPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        logger.LogInformation("MainWindow created");
    }

    private void OnGlobalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 点击输入框内区域时，不改动焦点，保持正常编辑体验。
        if (e.Source is TextBox) return;

        if (e.Source is Control control)
        {
            if (control.GetSelfAndVisualAncestors().OfType<TextBox>().Any()) return;

            // 点击可聚焦控件（按钮、下拉框等）或其模板子元素时，让控件自己处理焦点。
            if (control.GetSelfAndVisualAncestors().OfType<Control>().Any(ancestor => ancestor.Focusable)) return;
        }
        else if (e.Source is IInputElement inputElement && inputElement.Focusable)
        {
            return;
        }

        // 点击空白或非可聚焦内容时，将焦点移出输入框，清除选中状态。
        RootLayout.Focus(NavigationMethod.Pointer);
    }
}
