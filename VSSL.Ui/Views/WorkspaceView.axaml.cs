using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class WorkspaceView : UserControl
{
    private WorkspaceViewModel? _viewModel;

    public WorkspaceView()
    {
        InitializeComponent();
    }

    public WorkspaceView(WorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        AttachViewModel(viewModel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
            _viewModel.ConsoleLines.CollectionChanged -= OnConsoleLinesChanged;

        _viewModel = DataContext as WorkspaceViewModel;
        if (_viewModel is not null)
            _viewModel.ConsoleLines.CollectionChanged += OnConsoleLinesChanged;
    }

    private void AttachViewModel(WorkspaceViewModel viewModel)
    {
        if (_viewModel is not null)
            _viewModel.ConsoleLines.CollectionChanged -= OnConsoleLinesChanged;

        _viewModel = viewModel;
        _viewModel.ConsoleLines.CollectionChanged += OnConsoleLinesChanged;
    }

    private void OnConsoleLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel is null || !_viewModel.IsConsoleAutoFollow) return;
        if (_viewModel.ConsoleLines.Count == 0) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<ListBox>("ConsoleList") is not { } listBox) return;
            var last = _viewModel.ConsoleLines[^1];
            listBox.ScrollIntoView(last);
        });
    }
}
