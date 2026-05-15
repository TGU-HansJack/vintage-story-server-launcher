using System.Collections.ObjectModel;
using System.ComponentModel;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     总览-限制 页面视图模型
/// </summary>
public partial class OverviewRestrictionsViewModel : ViewModelBase
{
    private readonly IInstanceProfileService? _instanceProfileService;
    private readonly IClientModRestrictionService? _clientModRestrictionService;
    private bool _syncingSelectAll;

    [ObservableProperty] private InstanceProfile? _selectedProfile;
    [ObservableProperty] private bool _selectAll;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];

    public ObservableCollection<OverviewRestrictionModItemViewModel> Mods { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public bool HasMods => Mods.Count > 0;

    public bool HasNoMods => !HasMods;

    public bool HasSelectedMods => Mods.Any(item => item.IsSelected);

    partial void OnSelectedProfileChanged(InstanceProfile? value)
    {
        _ = LoadRestrictionsAsync(value);
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
    private async Task BlockSelectedAsync()
    {
        if (_clientModRestrictionService is null) return;

        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        var selectedModIds = Mods
            .Where(item => item.IsSelected)
            .Select(item => item.ModId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedModIds.Count == 0)
        {
            StatusMessage = L("RestrictionsStatusSelectModsToBlock");
            return;
        }

        try
        {
            IsBusy = true;
            var changed = await _clientModRestrictionService.AddModIdsToBlacklistAsync(profile, selectedModIds);
            await LoadRestrictionsAsync(profile);
            StatusMessage = changed > 0
                ? LF("RestrictionsStatusBlockedFormat", changed)
                : L("RestrictionsStatusNoChanges");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("RestrictionsStatusBlockFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UnblockSelectedAsync()
    {
        if (_clientModRestrictionService is null) return;

        var profile = SelectedProfile;
        if (profile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        var selectedModIds = Mods
            .Where(item => item.IsSelected)
            .Select(item => item.ModId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedModIds.Count == 0)
        {
            StatusMessage = L("RestrictionsStatusSelectModsToUnblock");
            return;
        }

        try
        {
            IsBusy = true;
            var changed = await _clientModRestrictionService.RemoveModIdsFromBlacklistAsync(profile, selectedModIds);
            await LoadRestrictionsAsync(profile);
            StatusMessage = changed > 0
                ? LF("RestrictionsStatusUnblockedFormat", changed)
                : L("RestrictionsStatusNoChanges");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("RestrictionsStatusUnblockFailedFormat", ex.Message);
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

            var oldSelectedId = SelectedProfile?.Id;
            var profiles = _instanceProfileService.GetProfiles();

            Profiles.Clear();
            foreach (var profile in profiles) Profiles.Add(profile);

            OnPropertyChanged(nameof(HasProfiles));
            OnPropertyChanged(nameof(HasNoProfiles));

            if (Profiles.Count == 0)
            {
                SelectedProfile = null;
                ClearMods();
                StatusMessage = L("StatusNoProfileCreateFirst");
                return;
            }

            var target = Profiles.FirstOrDefault(profile =>
                !string.IsNullOrWhiteSpace(oldSelectedId)
                && profile.Id.Equals(oldSelectedId, StringComparison.OrdinalIgnoreCase));
            target ??= Profiles[0];

            if (!ReferenceEquals(SelectedProfile, target))
            {
                SelectedProfile = target;
            }
            else
            {
                await LoadRestrictionsAsync(target);
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

    private async Task LoadRestrictionsAsync(InstanceProfile? profile)
    {
        if (_clientModRestrictionService is null || profile is null)
        {
            ClearMods();
            return;
        }

        try
        {
            var history = await _clientModRestrictionService.GetHistoricalClientModsAsync(profile);
            var blacklist = await _clientModRestrictionService.GetBlacklistedModIdsAsync(profile);

            var rows = new Dictionary<string, OverviewRestrictionModItemViewModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in history)
            {
                rows[item.ModId] = new OverviewRestrictionModItemViewModel
                {
                    ModId = item.ModId,
                    SeenCount = item.SeenCount,
                    LastSeenUtc = item.LastSeenUtc,
                    IsBlacklisted = blacklist.Contains(item.ModId)
                };
            }

            foreach (var blacklistedModId in blacklist)
            {
                if (rows.ContainsKey(blacklistedModId)) continue;

                rows[blacklistedModId] = new OverviewRestrictionModItemViewModel
                {
                    ModId = blacklistedModId,
                    SeenCount = 0,
                    LastSeenUtc = null,
                    IsBlacklisted = true
                };
            }

            foreach (var oldItem in Mods) oldItem.PropertyChanged -= OnModItemPropertyChanged;
            Mods.Clear();

            foreach (var item in rows.Values
                         .OrderByDescending(static x => x.IsBlacklisted)
                         .ThenByDescending(static x => x.SeenCount)
                         .ThenBy(static x => x.ModId, StringComparer.OrdinalIgnoreCase))
            {
                item.PropertyChanged += OnModItemPropertyChanged;
                Mods.Add(item);
            }

            SyncSelectAllByRows();
            OnPropertyChanged(nameof(HasMods));
            OnPropertyChanged(nameof(HasNoMods));
            OnPropertyChanged(nameof(HasSelectedMods));

            StatusMessage = Mods.Count == 0
                ? L("RestrictionsStatusNoHistory")
                : LF("RestrictionsStatusLoadedFormat", Mods.Count, Mods.Count(item => item.IsBlacklisted));
        }
        catch (Exception ex)
        {
            StatusMessage = LF("RestrictionsStatusLoadFailedFormat", ex.Message);
        }
    }

    private void OnModItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OverviewRestrictionModItemViewModel.IsSelected)) return;

        SyncSelectAllByRows();
        OnPropertyChanged(nameof(HasSelectedMods));
    }

    private void SyncSelectAllByRows()
    {
        _syncingSelectAll = true;
        SelectAll = Mods.Count > 0 && Mods.All(item => item.IsSelected);
        _syncingSelectAll = false;
    }

    private void ClearMods()
    {
        foreach (var oldItem in Mods) oldItem.PropertyChanged -= OnModItemPropertyChanged;

        Mods.Clear();
        SyncSelectAllByRows();

        OnPropertyChanged(nameof(HasMods));
        OnPropertyChanged(nameof(HasNoMods));
        OnPropertyChanged(nameof(HasSelectedMods));
    }

    #region Constructors

    public OverviewRestrictionsViewModel()
    {
    }

    public OverviewRestrictionsViewModel(
        IInstanceProfileService instanceProfileService,
        IClientModRestrictionService clientModRestrictionService)
    {
        _instanceProfileService = instanceProfileService;
        _clientModRestrictionService = clientModRestrictionService;
        _ = RefreshProfilesAsync();
    }

    #endregion
}
