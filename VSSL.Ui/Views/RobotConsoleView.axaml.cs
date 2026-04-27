using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
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
        ScrollToLatest(force: true);
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
            ScrollToLatest(force: false);
        };
    }

    private void ScrollToLatest(bool force)
    {
        if (DataContext is not RobotConsoleViewModel viewModel) return;
        if (viewModel.ConsoleLines.Count == 0) return;
        if (!force && !viewModel.IsConsoleAutoFollow) return;

        var last = viewModel.ConsoleLines[^1];
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<ListBox>("RobotConsoleList") is not { } listBox) return;
            listBox.ScrollIntoView(last);
        }, DispatcherPriority.Background);
    }

    private async void RobotConsoleList_OnKeyDown(object? sender, KeyEventArgs e)
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
