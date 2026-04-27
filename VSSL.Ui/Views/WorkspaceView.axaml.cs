using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
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
        {
            _viewModel.ConsoleLines.CollectionChanged += OnConsoleLinesChanged;
            ScrollToLatest(force: true);
        }
    }

    private void AttachViewModel(WorkspaceViewModel viewModel)
    {
        if (_viewModel is not null)
            _viewModel.ConsoleLines.CollectionChanged -= OnConsoleLinesChanged;

        _viewModel = viewModel;
        _viewModel.ConsoleLines.CollectionChanged += OnConsoleLinesChanged;
        ScrollToLatest(force: true);
    }

    private void OnConsoleLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollToLatest(force: false);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ScrollToLatest(force: true);
    }

    private void ScrollToLatest(bool force)
    {
        if (_viewModel is null) return;
        if (_viewModel.ConsoleLines.Count == 0) return;
        if (!force && !_viewModel.IsConsoleAutoFollow) return;

        var last = _viewModel.ConsoleLines[^1];
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<ListBox>("ConsoleList") is not { } listBox) return;
            listBox.ScrollIntoView(last);
        }, DispatcherPriority.Background);
    }

    private async void ConsoleList_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (sender is not ListBox listBox) return;

        var selectedLines = listBox.SelectedItems?
            .OfType<object>()
            .Select(item => item?.ToString() ?? string.Empty)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList()
            ?? [];
        if (selectedLines.Count == 0)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        await clipboard.SetTextAsync(string.Join(Environment.NewLine, selectedLines));
        e.Handled = true;
    }
}
