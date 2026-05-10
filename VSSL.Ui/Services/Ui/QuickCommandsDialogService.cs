using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using VSSL.Abstractions.Services.Ui;
using VSSL.Ui.Views.Dialogs;

namespace VSSL.Ui.Services.Ui;

/// <summary>
///     快捷指令编辑弹窗服务
/// </summary>
public class QuickCommandsDialogService : IQuickCommandsDialogService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> ShowEditorAsync(
        string title,
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken = default)
    {
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;
        if (owner is null)
        {
            return null;
        }

        var dialog = new QuickCommandsEditorWindow(title, commands);
        using var registration = cancellationToken.Register(() => dialog.Close(null));
        return await dialog.ShowDialog<IReadOnlyList<string>?>(owner);
    }
}
