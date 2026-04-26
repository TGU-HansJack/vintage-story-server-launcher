using Avalonia.Controls;
using VSSL.Abstractions.Services.Ui;
using Avalonia.Input;
using Avalonia.VisualTree;
using AppHeaderViewModel = VSSL.Ui.ViewModels.AppHeaderViewModel;

namespace VSSL.Ui.Controls;

public partial class AppHeader : UserControl
{
    private readonly IMainWindowService _mainWindowService;

    public AppHeader()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<AppHeaderViewModel>();
        _mainWindowService = ServiceLocator.GetRequiredService<IMainWindowService>();

        PointerPressed += OnHeaderPointerPressed;
    }

    /// <summary>
    ///     Handle title bar pointer pressed event:
    ///     double click to maximize/restore and single click to drag window.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type is not PointerType.Mouse) return;

        var source = e.Source as Control;
        if (source is Button) return;

        if (e.ClickCount == 2)
        {
            _mainWindowService.ToggleMaximize();
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var window = this.GetVisualRoot() as Window;
        window?.BeginMoveDrag(e);
    }
}
