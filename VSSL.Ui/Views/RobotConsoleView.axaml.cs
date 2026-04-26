using Avalonia.Controls;
using Avalonia.Threading;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class RobotConsoleView : UserControl
{
    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    public RobotConsoleView()
    {
        InitializeComponent();
        InitTimer();
    }

    public RobotConsoleView(RobotConsoleViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        InitTimer();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _refreshTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _refreshTimer.Stop();
    }

    private void InitTimer()
    {
        _refreshTimer.Tick += (_, _) =>
        {
            if (DataContext is not RobotConsoleViewModel viewModel) return;
            viewModel.RefreshCommand.Execute(null);

            if (!viewModel.IsConsoleAutoFollow || viewModel.ConsoleLines.Count == 0) return;

            if (this.FindControl<ListBox>("RobotConsoleList") is { } listBox)
                listBox.ScrollIntoView(viewModel.ConsoleLines[^1]);
        };
    }
}
