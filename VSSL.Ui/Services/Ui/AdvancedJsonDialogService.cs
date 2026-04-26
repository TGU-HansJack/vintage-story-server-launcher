using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using VSSL.Abstractions.Services.Ui;
using VSSL.Ui.Views.Dialogs;

namespace VSSL.Ui.Services.Ui;

/// <summary>
///     高级 JSON 编辑弹窗服务
/// </summary>
public class AdvancedJsonDialogService : IAdvancedJsonDialogService
{
    /// <inheritdoc />
    public async Task<string?> ShowEditorAsync(
        string title,
        string jsonText,
        CancellationToken cancellationToken = default)
    {
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;
        if (owner is null) return null;

        var dialog = new AdvancedJsonEditorWindow(title, jsonText);
        using var registration = cancellationToken.Register(() => dialog.Close(null));
        return await dialog.ShowDialog<string?>(owner);
    }
}
