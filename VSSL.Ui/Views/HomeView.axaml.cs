using Avalonia.Controls;
using Avalonia.Threading;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class HomeView : UserControl
{
    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    public HomeView()
    {
        InitializeComponent();
        InitTimer();
    }

    public HomeView(HomeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        InitTimer();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is HomeViewModel viewModel)
            viewModel.RefreshMetricsCommand.Execute(null);

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
            if (DataContext is HomeViewModel viewModel)
                viewModel.RefreshMetricsCommand.Execute(null);
        };
    }
}
