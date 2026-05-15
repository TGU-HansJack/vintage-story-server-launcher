using CommunityToolkit.Mvvm.ComponentModel;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     存档列表项视图模型
/// </summary>
public partial class SaveFileItemViewModel : ViewModelBase
{
    public string FullPath { get; init; } = string.Empty;

    public string DirectoryPath => string.IsNullOrWhiteSpace(FullPath) ? string.Empty : Path.GetDirectoryName(FullPath) ?? string.Empty;

    public string FileName { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public string SizeText => FormatSize(SizeBytes);

    public DateTimeOffset LastWriteTimeUtc { get; init; }

    public string LastWriteText => LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [ObservableProperty] private bool _isSelected;

    [ObservableProperty] private bool _isActive;

    private static string FormatSize(long sizeBytes)
    {
        if (sizeBytes < 0) sizeBytes = 0;

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)sizeBytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{size:0} {units[unitIndex]}"
            : $"{size:0.##} {units[unitIndex]}";
    }
}
