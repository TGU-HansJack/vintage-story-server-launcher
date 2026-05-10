using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class AutomationView : UserControl
{
    private AutomationViewModel? _viewModel;

    public AutomationView()
    {
        InitializeComponent();
    }

    public AutomationView(AutomationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        AttachViewModel(viewModel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
            _viewModel.RuntimeLogs.CollectionChanged -= OnRuntimeLogsChanged;

        _viewModel = DataContext as AutomationViewModel;
        if (_viewModel is not null)
        {
            _viewModel.RuntimeLogs.CollectionChanged += OnRuntimeLogsChanged;
            ScrollToLatest(force: true);
        }
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ScrollToLatest(force: true);
    }

    private void AttachViewModel(AutomationViewModel viewModel)
    {
        if (_viewModel is not null)
            _viewModel.RuntimeLogs.CollectionChanged -= OnRuntimeLogsChanged;

        _viewModel = viewModel;
        _viewModel.RuntimeLogs.CollectionChanged += OnRuntimeLogsChanged;
        ScrollToLatest(force: true);
    }

    private void OnRuntimeLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollToLatest(force: false);
    }

    private void ScrollToLatest(bool force)
    {
        if (_viewModel is null || _viewModel.RuntimeLogs.Count == 0) return;

        var last = _viewModel.RuntimeLogs[^1];
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<ListBox>("AutomationRuntimeLogsList") is not { } listBox) return;
            listBox.ScrollIntoView(last);
        }, DispatcherPriority.Background);
    }
}
