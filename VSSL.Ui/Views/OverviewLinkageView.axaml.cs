using Avalonia.Controls;
using Avalonia.Threading;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class OverviewLinkageView : UserControl
{
    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    public OverviewLinkageView()
    {
        InitializeComponent();
        InitTimer();
    }

    public OverviewLinkageView(OverviewLinkageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        InitTimer();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is OverviewLinkageViewModel viewModel)
        {
            viewModel.RefreshLinkageCommand.Execute(null);
        }

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
            if (DataContext is OverviewLinkageViewModel viewModel)
            {
                viewModel.RefreshLinkageRuntime();
            }
        };
    }
}
