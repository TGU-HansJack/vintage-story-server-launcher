using System.Collections.ObjectModel;
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

    [ObservableProperty] private InstanceProfile? _selectedProfile;
    [ObservableProperty] private string _modZipPath = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];

    public ObservableCollection<ModItemViewModel> Mods { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public bool HasMods => Mods.Count > 0;

    public bool HasNoMods => !HasMods;

    partial void OnSelectedProfileChanged(InstanceProfile? value)
    {
        _ = LoadModsAsync(value);
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
                Mods.Clear();
                OnPropertyChanged(nameof(HasMods));
                OnPropertyChanged(nameof(HasNoMods));
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
            Mods.Clear();
            OnPropertyChanged(nameof(HasMods));
            OnPropertyChanged(nameof(HasNoMods));
            return;
        }

        try
        {
            var mods = await _instanceModService.GetModsAsync(profile);
            Mods.Clear();
            foreach (var mod in mods)
                Mods.Add(ModItemViewModel.FromModel(mod));

            OnPropertyChanged(nameof(HasMods));
            OnPropertyChanged(nameof(HasNoMods));
            StatusMessage = LF("ModStatusLoadedFormat", Mods.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("ModStatusLoadFailedFormat", ex.Message);
        }
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
