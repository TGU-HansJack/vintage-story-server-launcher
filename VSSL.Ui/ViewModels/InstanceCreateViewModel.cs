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
                StatusMessage = L("InstanceCreateStatusEnterProfileName");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedVersion))
            {
                StatusMessage = L("InstanceCreateStatusSelectVersionFirst");
                return;
            }

            var profile = _instanceProfileService.CreateProfile(ProfileName, SelectedVersion);
            ProfileName = string.Empty;
            LoadProfiles();
            StatusMessage = LF("InstanceCreateStatusProfileCreatedFormat", profile.Name);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("InstanceCreateStatusCreateFailedFormat", ex.Message);
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
            StatusMessage = L("InstanceCreateStatusSelectProfilesToDelete");
            return;
        }

        try
        {
            var deletedCount = _instanceProfileService.DeleteProfiles(selectedIds);
            LoadProfiles();
            StatusMessage = deletedCount > 0
                ? LF("InstanceCreateStatusDeletedProfilesFormat", deletedCount)
                : L("InstanceCreateStatusNoProfilesDeleted");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("InstanceCreateStatusDeleteFailedFormat", ex.Message);
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
            StatusMessage = L("InstanceCreateStatusNoVersionsFound");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedVersion) || !InstalledVersions.Contains(SelectedVersion))
            SelectedVersion = InstalledVersions[0];

        StatusMessage = LF("InstanceCreateStatusDetectedVersionsFormat", InstalledVersions.Count);
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
            StatusMessage = LF("InstanceCreateStatusDefaultWorkspaceFormat", WorkspaceRoot);
    }

    #endregion
}
