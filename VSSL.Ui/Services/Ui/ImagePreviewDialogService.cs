using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using VSSL.Abstractions.Services.Ui;
using VSSL.Ui.Views.Dialogs;

namespace VSSL.Ui.Services.Ui;

/// <summary>
///     图片预览弹窗服务
/// </summary>
public class ImagePreviewDialogService : IImagePreviewDialogService
{
    /// <inheritdoc />
    public async Task ShowAsync(
        string title,
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;
        if (owner is null)
        {
            return;
        }

        var dialog = new ImagePreviewWindow(title, imagePath);
        using var registration = cancellationToken.Register(() => dialog.Close(null));
        await dialog.ShowDialog<object?>(owner);
    }
}
