using System.Collections.ObjectModel;
using System.ComponentModel;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     View model of <see cref="Views.InstanceCreateView" />
/// </summary>
public partial class InstanceCreateViewModel : ViewModelBase
{
    private readonly IInstanceProfileService? _instanceProfileService;
    private bool _syncingSelectAll;

    [ObservableProperty] private string? _selectedVersion;
    [ObservableProperty] private string _profileName = string.Empty;
    [ObservableProperty] private bool _selectAll;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _workspaceRoot = string.Empty;

    public ObservableCollection<string> InstalledVersions { get; } = [];

    public ObservableCollection<InstanceProfileItemViewModel> Profiles { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public bool HasSelectedProfiles => Profiles.Any(profile => profile.IsSelected);

    partial void OnSelectAllChanged(bool value)
    {
        if (_syncingSelectAll) return;

        _syncingSelectAll = true;
        foreach (var profile in Profiles)
            profile.IsSelected = value;
        _syncingSelectAll = false;

        OnPropertyChanged(nameof(HasSelectedProfiles));
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadData();
    }

    [RelayCommand]
    private void Create()
    {
        if (_instanceProfileService is null) return;

        try
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
            {
                StatusMessage = "请输入档案名称。";
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedVersion))
            {
                StatusMessage = "请先选择已安装服务端版本。";
                return;
            }

            var profile = _instanceProfileService.CreateProfile(ProfileName, SelectedVersion);
            ProfileName = string.Empty;
            LoadProfiles();
            StatusMessage = $"档案已创建：{profile.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"创建失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (_instanceProfileService is null) return;

        var selectedIds = Profiles
            .Where(profile => profile.IsSelected)
            .Select(profile => profile.Id)
            .ToList();
        if (selectedIds.Count == 0)
        {
            StatusMessage = "请先勾选要删除的档案。";
            return;
        }

        try
        {
            var deletedCount = _instanceProfileService.DeleteProfiles(selectedIds);
            LoadProfiles();
            StatusMessage = deletedCount > 0
                ? $"已删除 {deletedCount} 个档案。"
                : "没有可删除的档案。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除失败：{ex.Message}";
        }
    }

    private void LoadData()
    {
        LoadInstalledVersions();
        LoadProfiles();
    }

    private void LoadInstalledVersions()
    {
        InstalledVersions.Clear();
        if (_instanceProfileService is null)
        {
            SelectedVersion = null;
            return;
        }

        var versions = _instanceProfileService.GetInstalledVersions();
        foreach (var version in versions) InstalledVersions.Add(version);

        if (InstalledVersions.Count == 0)
        {
            SelectedVersion = null;
            StatusMessage = "未发现可用版本（来源：packages 的 vs_server_win-x64_*.zip）。";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedVersion) || !InstalledVersions.Contains(SelectedVersion))
            SelectedVersion = InstalledVersions[0];

        StatusMessage = $"已检测到 {InstalledVersions.Count} 个版本（来源：packages）。";
    }

    private void LoadProfiles()
    {
        foreach (var item in Profiles)
            item.PropertyChanged -= OnProfileItemPropertyChanged;

        Profiles.Clear();
        if (_instanceProfileService is null)
        {
            OnPropertyChanged(nameof(HasProfiles));
            OnPropertyChanged(nameof(HasNoProfiles));
            OnPropertyChanged(nameof(HasSelectedProfiles));
            return;
        }

        var profiles = _instanceProfileService.GetProfiles();
        foreach (var profile in profiles)
        {
            var item = new InstanceProfileItemViewModel
            {
                Id = profile.Id,
                Name = profile.Name,
                Version = profile.Version,
                DirectoryPath = profile.DirectoryPath,
                CreatedAtUtc = profile.CreatedAtUtc
            };
            item.PropertyChanged += OnProfileItemPropertyChanged;
            Profiles.Add(item);
        }

        SyncSelectAllByRows();
        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(HasNoProfiles));
        OnPropertyChanged(nameof(HasSelectedProfiles));
    }

    private void OnProfileItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(InstanceProfileItemViewModel.IsSelected)) return;

        SyncSelectAllByRows();
        OnPropertyChanged(nameof(HasSelectedProfiles));
    }

    private void SyncSelectAllByRows()
    {
        _syncingSelectAll = true;
        SelectAll = Profiles.Count > 0 && Profiles.All(item => item.IsSelected);
        _syncingSelectAll = false;
    }

    #region Constructors

    public InstanceCreateViewModel()
    {
    }

    public InstanceCreateViewModel(IInstanceProfileService instanceProfileService)
    {
        _instanceProfileService = instanceProfileService;
        WorkspaceRoot = instanceProfileService.GetWorkspaceRoot();
        LoadData();

        if (string.IsNullOrWhiteSpace(StatusMessage))
            StatusMessage = $"默认工作区：{WorkspaceRoot}";
    }

    #endregion
}
