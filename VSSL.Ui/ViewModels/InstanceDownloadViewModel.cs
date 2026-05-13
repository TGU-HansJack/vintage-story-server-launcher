using System.Collections.ObjectModel;
using VSSL.Abstractions.Services;
using VSSL.Abstractions.Services.Ui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     View model of <see cref="Views.InstanceDownloadView" />
/// </summary>
public partial class InstanceDownloadViewModel : ViewModelBase
{
    private readonly IInstanceDownloadService? _instanceDownloadService;
    private readonly IFilePickerService? _filePickerService;
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
            StatusMessage = LF("InstanceDownloadStatusDownloadingFormat", entry.FileName);
            var progress = new Progress<double>(value => entry.DownloadProgress = value);
            await _instanceDownloadService.DownloadByCdnAsync(entry.CdnUrl, entry.TargetFilePath, progress);
            entry.IsDownloaded = _instanceDownloadService.IsDownloaded(entry.TargetFilePath);
            StatusMessage = entry.IsDownloaded
                ? LF("InstanceDownloadStatusCompletedFormat", entry.FileName)
                : LF("InstanceDownloadStatusFailedSimpleFormat", entry.FileName);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("InstanceDownloadStatusFailedFormat", entry.FileName, ex.Message);
        }
        finally
        {
            entry.IsDownloading = false;
            entry.DownloadProgress = 0;
            ApplyFilters();
        }
    }

    [RelayCommand]
    private async Task ImportPackageAsync()
    {
        if (_instanceDownloadService is null || _filePickerService is null)
            return;

        var selectedPath = await _filePickerService.PickSingleFileAsync(
            L("InstanceDownloadImportDialogTitle"),
            L("InstanceDownloadImportFilterName"),
            ["vs_server_win-x64_*.zip", "*.zip"]);
        if (string.IsNullOrWhiteSpace(selectedPath))
            return;

        var selectedFileName = Path.GetFileName(selectedPath);
        try
        {
            StatusMessage = LF("InstanceDownloadStatusImportingFormat", selectedFileName);
            var importedPath = await _instanceDownloadService.ImportServerPackageAsync(selectedPath);
            ApplyFilters();
            StatusMessage = LF("InstanceDownloadStatusImportedFormat", Path.GetFileName(importedPath));
        }
        catch (Exception ex)
        {
            StatusMessage = LF("InstanceDownloadStatusImportFailedFormat", ex.Message);
        }
    }

    private async Task LoadEntriesAsync()
    {
        if (_instanceDownloadService is null) return;

        IsLoading = true;
        StatusMessage = L("InstanceDownloadStatusLoadingList");
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
                ? L("InstanceDownloadStatusNoEntries")
                : LF("InstanceDownloadStatusLoadedEntriesFormat", _allEntries.Count, DownloadRootDirectory);
        }
        catch (Exception ex)
        {
            _allEntries.Clear();
            Entries.Clear();
            StatusMessage = LF("InstanceDownloadStatusLoadFailedFormat", ex.Message);
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

    public InstanceDownloadViewModel(
        IInstanceDownloadService instanceDownloadService,
        IFilePickerService filePickerService)
    {
        _instanceDownloadService = instanceDownloadService;
        _filePickerService = filePickerService;
        DownloadRootDirectory = instanceDownloadService.GetDefaultDownloadDirectory();

        Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoData));
        Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasData));
        _ = LoadEntriesAsync();
    }

    #endregion
}
