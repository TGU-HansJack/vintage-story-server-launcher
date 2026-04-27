using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using VSSL.Abstractions.Services.Ui;

namespace VSSL.Ui.Services.Ui;

/// <summary>
///     文件选择服务
/// </summary>
public class FilePickerService : IFilePickerService
{
    /// <inheritdoc />
    public async Task<string?> PickSingleFileAsync(
        string title,
        string filterName,
        IReadOnlyList<string> patterns,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return null;

        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;
        if (owner?.StorageProvider is null)
            return null;

        var safePatterns = patterns?.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        if (safePatterns is null || safePatterns.Length == 0)
            safePatterns = ["*"];

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(filterName)
                {
                    Patterns = safePatterns
                }
            ]
        };

        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await owner.StorageProvider.OpenFilePickerAsync(options);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (files.Count == 0)
            return null;

        var selected = files[0];
        var localPath = selected.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(localPath))
            return localPath;

        return selected.Path.IsFile
            ? selected.Path.LocalPath
            : selected.Path.OriginalString;
    }
}
