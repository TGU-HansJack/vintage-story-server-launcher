using System.Collections.ObjectModel;
using System.ComponentModel;
using VSSL.Abstractions.Services;
using VSSL.Abstractions.Services.Ui;
using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     模组页面视图模型
/// </summary>
public partial class ModViewModel : ViewModelBase
{
    private readonly IInstanceProfileService? _instanceProfileService;
    private readonly IInstanceModService? _instanceModService;
    private readonly IFilePickerService? _filePickerService;
    private bool _syncingSelectAll;

    [ObservableProperty] private InstanceProfile? _selectedProfile;
    [ObservableProperty] private string _modZipPath = string.Empty;
    [ObservableProperty] private bool _selectAll;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];

    public ObservableCollection<ModItemViewModel> Mods { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public bool HasMods => Mods.Count > 0;

    public bool HasNoMods => !HasMods;

    public bool HasSelectedMods => Mods.Any(mod => mod.IsSelected);

    partial void OnSelectedProfileChanged(InstanceProfile? value)
    {
        _ = LoadModsAsync(value);
    }

    partial void OnSelectAllChanged(bool value)
    {
        if (_syncingSelectAll) return;

        _syncingSelectAll = true;
        foreach (var mod in Mods)
            mod.IsSelected = value;
        _syncingSelectAll = false;

        OnPropertyChanged(nameof(HasSelectedMods));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await RefreshProfilesAsync();
    }

    [RelayCommand]
    private async Task BrowseModZipAsync()
    {
        if (_filePickerService is null) return;

        var selectedPath = await _filePickerService.PickSingleFileAsync(
            L("ModBrowseDialogTitle"),
            L("ModBrowseFilterName"),
            ["*.zip"]);

        if (string.IsNullOrWhiteSpace(selectedPath))
            return;

        ModZipPath = selectedPath.Trim();
    }

    [RelayCommand]
    private async Task ImportByPathAsync()
    {
        if (_instanceModService is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        if (string.IsNullOrWhiteSpace(ModZipPath))
        {
            StatusMessage = L("ModStatusEnterZipPath");
            return;
        }

        try
        {
            IsBusy = true;
            var imported = await _instanceModService.ImportModZipAsync(profile, ModZipPath.Trim());
            await LoadModsAsync(profile);
            StatusMessage = LF("ModStatusImportedFormat", imported.ModId, imported.Version);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ModStatusImportFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleEnabledAsync(ModItemViewModel? mod)
    {
        if (_instanceModService is null || mod is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        try
        {
            IsBusy = true;
            var targetEnabled = mod.IsDisabled;
            await _instanceModService.SetModEnabledAsync(profile, mod.ModId, mod.Version, targetEnabled);
            await LoadModsAsync(profile);
            StatusMessage = LF("ModStatusToggledFormat", mod.ModId, targetEnabled ? L("ModEnabledLabel") : L("ModDisabledLabel"));
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ModStatusToggleFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (_instanceModService is null) return;
        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        var selectedMods = Mods
            .Where(mod => mod.IsSelected)
            .Select(mod => new ModEntry
            {
                ModId = mod.ModId,
                Version = mod.Version,
                FilePath = mod.FilePath,
                Status = mod.Status,
                IsDisabled = mod.IsDisabled,
                Dependencies = [],
                DependencyIssues = []
            })
            .ToList();

        if (selectedMods.Count == 0)
        {
            StatusMessage = L("ModStatusSelectModsToDelete");
            return;
        }

        try
        {
            IsBusy = true;
            var deleted = await _instanceModService.DeleteModsAsync(profile, selectedMods);
            await LoadModsAsync(profile);
            StatusMessage = deleted > 0
                ? LF("ModStatusDeletedFormat", deleted)
                : L("ModStatusNoDeletes");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ModStatusDeleteFailedFormat", ex.Message);
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
            var oldSelected = SelectedProfile?.Id;
            var profiles = _instanceProfileService.GetProfiles();

            Profiles.Clear();
            foreach (var profile in profiles) Profiles.Add(profile);
            OnPropertyChanged(nameof(HasProfiles));
            OnPropertyChanged(nameof(HasNoProfiles));

            if (Profiles.Count == 0)
            {
                SelectedProfile = null;
                foreach (var mod in Mods) mod.PropertyChanged -= OnModItemPropertyChanged;
                Mods.Clear();
                SyncSelectAllByRows();
                OnPropertyChanged(nameof(HasMods));
                OnPropertyChanged(nameof(HasNoMods));
                OnPropertyChanged(nameof(HasSelectedMods));
                StatusMessage = L("StatusNoProfileCreateFirst");
                return;
            }

            var target = Profiles.FirstOrDefault(profile =>
                !string.IsNullOrWhiteSpace(oldSelected) &&
                profile.Id.Equals(oldSelected, StringComparison.OrdinalIgnoreCase));
            target ??= Profiles[0];

            if (!ReferenceEquals(SelectedProfile, target))
            {
                SelectedProfile = target;
            }
            else
            {
                await LoadModsAsync(target);
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

    private async Task LoadModsAsync(InstanceProfile? profile)
    {
        if (_instanceModService is null || profile is null)
        {
            foreach (var mod in Mods) mod.PropertyChanged -= OnModItemPropertyChanged;
            Mods.Clear();
            SyncSelectAllByRows();
            OnPropertyChanged(nameof(HasMods));
            OnPropertyChanged(nameof(HasNoMods));
            OnPropertyChanged(nameof(HasSelectedMods));
            return;
        }

        try
        {
            var mods = await _instanceModService.GetModsAsync(profile);
            foreach (var mod in Mods) mod.PropertyChanged -= OnModItemPropertyChanged;
            Mods.Clear();
            foreach (var mod in mods)
            {
                var item = ModItemViewModel.FromModel(mod);
                item.PropertyChanged += OnModItemPropertyChanged;
                Mods.Add(item);
            }

            SyncSelectAllByRows();
            OnPropertyChanged(nameof(HasMods));
            OnPropertyChanged(nameof(HasNoMods));
            OnPropertyChanged(nameof(HasSelectedMods));
            StatusMessage = LF("ModStatusLoadedFormat", Mods.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ModStatusLoadFailedFormat", ex.Message);
        }
    }

    private void OnModItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ModItemViewModel.IsSelected)) return;
        SyncSelectAllByRows();
        OnPropertyChanged(nameof(HasSelectedMods));
    }

    private void SyncSelectAllByRows()
    {
        _syncingSelectAll = true;
        SelectAll = Mods.Count > 0 && Mods.All(item => item.IsSelected);
        _syncingSelectAll = false;
    }

    #region Constructors

    public ModViewModel()
    {
    }

    public ModViewModel(
        IInstanceProfileService instanceProfileService,
        IInstanceModService instanceModService,
        IFilePickerService filePickerService)
    {
        _instanceProfileService = instanceProfileService;
        _instanceModService = instanceModService;
        _filePickerService = filePickerService;
        _ = RefreshProfilesAsync();
    }

    #endregion
}
