using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     实例下载列表项视图模型
/// </summary>
public partial class InstanceDownloadItemViewModel : ViewModelBase
{
    public InstanceDownloadItemViewModel(ServerDownloadEntry entry)
    {
        Version = entry.Version;
        Platform = entry.Platform;
        FileSize = entry.FileSize;
        FileName = entry.FileName;
        CdnUrl = entry.CdnUrl;
        TargetFilePath = entry.TargetFilePath;
    }

    public string Version { get; }

    public string Platform { get; }

    public string FileSize { get; }

    public string FileName { get; }

    public string CdnUrl { get; }

    public string TargetFilePath { get; }

    [ObservableProperty] private bool _isDownloaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(DownloadProgressText))]
    private bool _isDownloading;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DownloadProgressText))]
    private double _downloadProgress;

    public bool CanDownload => !IsDownloading;

    public string DownloadProgressText => IsDownloading ? $"{DownloadProgress * 100:0}%" : string.Empty;
}
