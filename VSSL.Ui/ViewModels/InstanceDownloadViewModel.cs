using System.Collections.ObjectModel;
using VSSL.Abstractions.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     View model of <see cref="Views.InstanceDownloadView" />
/// </summary>
public partial class InstanceDownloadViewModel : ViewModelBase
{
    private readonly IInstanceDownloadService? _instanceDownloadService;
    private readonly List<InstanceDownloadItemViewModel> _allEntries = [];

    [ObservableProperty] private bool _showDownloadedOnly;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasNoData))] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _downloadRootDirectory = string.Empty;

    public ObservableCollection<InstanceDownloadItemViewModel> Entries { get; } = [];

    public bool HasNoData => !IsLoading && Entries.Count == 0;

    public bool HasData => Entries.Count > 0;

    partial void OnShowDownloadedOnlyChanged(bool value)
    {
        ApplyFilters();
    }

    partial void OnSearchKeywordChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            ApplyFilters();
    }

    [RelayCommand]
    private void Search()
    {
        ApplyFilters();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadEntriesAsync();
    }

    [RelayCommand]
    private async Task DownloadAsync(InstanceDownloadItemViewModel? entry)
    {
        if (_instanceDownloadService is null || entry is null || entry.IsDownloading) return;

        entry.IsDownloading = true;
        entry.DownloadProgress = 0;

        try
        {
            StatusMessage = $"正在下载：{entry.FileName}";
            var progress = new Progress<double>(value => entry.DownloadProgress = value);
            await _instanceDownloadService.DownloadByCdnAsync(entry.CdnUrl, entry.TargetFilePath, progress);
            entry.IsDownloaded = _instanceDownloadService.IsDownloaded(entry.TargetFilePath);
            StatusMessage = entry.IsDownloaded
                ? $"下载完成：{entry.FileName}"
                : $"下载失败：{entry.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"下载失败：{entry.FileName}，{ex.Message}";
        }
        finally
        {
            entry.IsDownloading = false;
            entry.DownloadProgress = 0;
            ApplyFilters();
        }
    }

    private async Task LoadEntriesAsync()
    {
        if (_instanceDownloadService is null) return;

        IsLoading = true;
        StatusMessage = "正在读取服务端版本列表...";
        try
        {
            var serverEntries = await _instanceDownloadService.GetServerDownloadEntriesAsync();

            _allEntries.Clear();
            foreach (var serverEntry in serverEntries)
            {
                var viewModel = new InstanceDownloadItemViewModel(serverEntry)
                {
                    IsDownloaded = _instanceDownloadService.IsDownloaded(serverEntry.TargetFilePath)
                };
                _allEntries.Add(viewModel);
            }

            ApplyFilters();
            StatusMessage = _allEntries.Count == 0
                ? "未读取到可下载服务端版本。"
                : $"已加载 {_allEntries.Count} 条服务端版本，默认下载目录：{DownloadRootDirectory}";
        }
        catch (Exception ex)
        {
            _allEntries.Clear();
            Entries.Clear();
            StatusMessage = $"读取服务端版本失败：{ex.Message}";
            OnPropertyChanged(nameof(HasNoData));
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoData));
        }
    }

    private void ApplyFilters()
    {
        if (_instanceDownloadService is null)
        {
            Entries.Clear();
            OnPropertyChanged(nameof(HasNoData));
            return;
        }

        foreach (var item in _allEntries)
            item.IsDownloaded = _instanceDownloadService.IsDownloaded(item.TargetFilePath);

        IEnumerable<InstanceDownloadItemViewModel> query = _allEntries;

        if (ShowDownloadedOnly)
            query = query.Where(item => item.IsDownloaded);

        var keyword = SearchKeyword.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(item =>
                ContainsIgnoreCase(item.Version, keyword) ||
                ContainsIgnoreCase(item.Platform, keyword) ||
                ContainsIgnoreCase(item.FileName, keyword) ||
                ContainsIgnoreCase(item.TargetFilePath, keyword));

        Entries.Clear();
        foreach (var item in query) Entries.Add(item);

        OnPropertyChanged(nameof(HasNoData));
        OnPropertyChanged(nameof(HasData));
    }

    private static bool ContainsIgnoreCase(string value, string keyword)
    {
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    #region Constructors

    public InstanceDownloadViewModel()
    {
    }

    public InstanceDownloadViewModel(IInstanceDownloadService instanceDownloadService)
    {
        _instanceDownloadService = instanceDownloadService;
        DownloadRootDirectory = instanceDownloadService.GetDefaultDownloadDirectory();

        Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoData));
        Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasData));
        _ = LoadEntriesAsync();
    }

    #endregion
}
