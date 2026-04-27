using System.Collections.ObjectModel;
using System.ComponentModel;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     存档页面视图模型
/// </summary>
public partial class SaveViewModel : ViewModelBase
{
    private readonly IInstanceProfileService? _instanceProfileService;
    private readonly IInstanceSaveService? _instanceSaveService;
    private bool _syncingSelectAll;

    [ObservableProperty] private InstanceProfile? _selectedProfile;
    [ObservableProperty] private string _newSaveName = "default";
    [ObservableProperty] private bool _selectAll;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];

    public ObservableCollection<SaveFileItemViewModel> Saves { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public bool HasSaves => Saves.Count > 0;

    public bool HasNoSaves => !HasSaves;

    public bool HasSelectedSaves => Saves.Any(save => save.IsSelected);

    public string CurrentProfileVersion
    {
        get
        {
            var profile = SelectedProfile;
            return profile is null ? "-" : profile.Version;
        }
    }

    public string CurrentProfileVersionText => LF("SaveCurrentProfileVersionFormat", CurrentProfileVersion);

    partial void OnSelectedProfileChanged(InstanceProfile? value)
    {
        OnPropertyChanged(nameof(CurrentProfileVersion));
        OnPropertyChanged(nameof(CurrentProfileVersionText));
        _ = LoadSavesAsync(value);
    }

    partial void OnSelectAllChanged(bool value)
    {
        if (_syncingSelectAll) return;

        _syncingSelectAll = true;
        foreach (var save in Saves)
            save.IsSelected = value;
        _syncingSelectAll = false;

        OnPropertyChanged(nameof(HasSelectedSaves));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await RefreshProfilesAsync();
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (_instanceProfileService is null || _instanceSaveService is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        try
        {
            IsBusy = true;
            var savePath = await _instanceSaveService.CreateSaveAsync(profile, NewSaveName);
            profile.ActiveSaveFile = savePath;
            profile.SaveDirectory = Path.GetDirectoryName(savePath) ?? profile.SaveDirectory;
            profile.LastUpdatedUtc = DateTimeOffset.UtcNow;
            _instanceProfileService.UpdateProfile(profile);

            NewSaveName = "default";
            await LoadSavesAsync(profile);
            StatusMessage = LF("SaveStatusCreatedFormat", Path.GetFileName(savePath));
        }
        catch (Exception ex)
        {
            StatusMessage = LF("SaveStatusCreateFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (_instanceProfileService is null || _instanceSaveService is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        var selectedPaths = Saves
            .Where(save => save.IsSelected)
            .Select(save => save.FullPath)
            .ToList();
        if (selectedPaths.Count == 0)
        {
            StatusMessage = L("SaveStatusSelectSavesToDelete");
            return;
        }

        try
        {
            IsBusy = true;
            var deleted = await _instanceSaveService.DeleteSavesAsync(profile, selectedPaths);
            await LoadSavesAsync(profile);

            if (selectedPaths.Any(path => path.Equals(profile.ActiveSaveFile, StringComparison.OrdinalIgnoreCase)))
            {
                var nextActive = Saves.FirstOrDefault()?.FullPath;
                if (!string.IsNullOrWhiteSpace(nextActive))
                {
                    await _instanceSaveService.SetActiveSaveAsync(profile, nextActive);
                    profile.ActiveSaveFile = nextActive;
                    profile.SaveDirectory = Path.GetDirectoryName(nextActive) ?? profile.SaveDirectory;
                }
                else
                {
                    var defaultSave = GetDefaultSaveFile(profile.Id);
                    await _instanceSaveService.SetActiveSaveAsync(profile, defaultSave);
                    profile.ActiveSaveFile = defaultSave;
                    profile.SaveDirectory = Path.GetDirectoryName(defaultSave) ?? profile.SaveDirectory;
                }

                profile.LastUpdatedUtc = DateTimeOffset.UtcNow;
                _instanceProfileService.UpdateProfile(profile);
                await LoadSavesAsync(profile);
            }

            StatusMessage = deleted > 0 ? LF("SaveStatusDeletedFormat", deleted) : L("SaveStatusNoDeletes");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("SaveStatusDeleteFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SetActiveAsync(SaveFileItemViewModel? saveItem)
    {
        if (_instanceProfileService is null || _instanceSaveService is null || saveItem is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        try
        {
            IsBusy = true;
            await _instanceSaveService.SetActiveSaveAsync(profile, saveItem.FullPath);
            profile.ActiveSaveFile = saveItem.FullPath;
            profile.SaveDirectory = Path.GetDirectoryName(saveItem.FullPath) ?? profile.SaveDirectory;
            profile.LastUpdatedUtc = DateTimeOffset.UtcNow;
            _instanceProfileService.UpdateProfile(profile);

            await LoadSavesAsync(profile);
            StatusMessage = LF("SaveStatusSetActiveFormat", saveItem.FileName);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("SaveStatusSetActiveFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshProfilesAsync()
    {
        if (_instanceProfileService is null) return;

        try
        {
            IsBusy = true;
            var currentSelectedId = SelectedProfile?.Id;
            var profiles = _instanceProfileService.GetProfiles();

            Profiles.Clear();
            foreach (var profile in profiles) Profiles.Add(profile);

            OnPropertyChanged(nameof(HasProfiles));
            OnPropertyChanged(nameof(HasNoProfiles));

            if (Profiles.Count == 0)
            {
                SelectedProfile = null;
                Saves.Clear();
                OnPropertyChanged(nameof(HasSaves));
                OnPropertyChanged(nameof(HasNoSaves));
                StatusMessage = L("StatusNoProfileCreateFirst");
                return;
            }

            var target = Profiles.FirstOrDefault(profile =>
                !string.IsNullOrWhiteSpace(currentSelectedId) &&
                profile.Id.Equals(currentSelectedId, StringComparison.OrdinalIgnoreCase));
            target ??= Profiles[0];

            if (!ReferenceEquals(SelectedProfile, target))
            {
                SelectedProfile = target;
            }
            else
            {
                await LoadSavesAsync(target);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = LF("StatusRefreshProfilesFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSavesAsync(InstanceProfile? profile)
    {
        if (_instanceSaveService is null || profile is null)
        {
            Saves.Clear();
            OnPropertyChanged(nameof(HasSaves));
            OnPropertyChanged(nameof(HasNoSaves));
            OnPropertyChanged(nameof(HasSelectedSaves));
            return;
        }

        try
        {
            var saveEntries = await _instanceSaveService.GetSavesAsync(profile);

            foreach (var save in Saves) save.PropertyChanged -= OnSaveItemPropertyChanged;
            Saves.Clear();

            foreach (var saveEntry in saveEntries)
            {
                var item = new SaveFileItemViewModel
                {
                    FullPath = saveEntry.FullPath,
                    FileName = saveEntry.FileName,
                    LastWriteTimeUtc = saveEntry.LastWriteTimeUtc,
                    IsActive = saveEntry.FullPath.Equals(profile.ActiveSaveFile, StringComparison.OrdinalIgnoreCase)
                };
                item.PropertyChanged += OnSaveItemPropertyChanged;
                Saves.Add(item);
            }

            SyncSelectAllByRows();
            OnPropertyChanged(nameof(HasSaves));
            OnPropertyChanged(nameof(HasNoSaves));
            OnPropertyChanged(nameof(HasSelectedSaves));

            StatusMessage = LF("SaveStatusLoadedFormat", Saves.Count, profile.Version);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("SaveStatusLoadFailedFormat", ex.Message);
        }
    }

    private void OnSaveItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SaveFileItemViewModel.IsSelected)) return;
        SyncSelectAllByRows();
        OnPropertyChanged(nameof(HasSelectedSaves));
    }

    private void SyncSelectAllByRows()
    {
        _syncingSelectAll = true;
        SelectAll = Saves.Count > 0 && Saves.All(item => item.IsSelected);
        _syncingSelectAll = false;
    }

    private static string GetDefaultSaveFile(string profileId)
    {
        var workspaceRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VSSL",
            "workspace");
        return Path.Combine(workspaceRoot, "data", profileId, "Saves", "default.vcdbs");
    }

    #region Constructors

    public SaveViewModel()
    {
    }

    public SaveViewModel(
        IInstanceProfileService instanceProfileService,
        IInstanceSaveService instanceSaveService)
    {
        _instanceProfileService = instanceProfileService;
        _instanceSaveService = instanceSaveService;
        _ = RefreshProfilesAsync();
    }

    #endregion
}
