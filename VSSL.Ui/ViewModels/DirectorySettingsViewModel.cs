using System;
using System.Collections.ObjectModel;
using System.Linq;
using VSSL.Abstractions.Services;
using VSSL.Abstractions.Services.Ui;
using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     目录设置页面
/// </summary>
public partial class DirectorySettingsViewModel : ViewModelBase
{
    private readonly IAutomationSettingsService? _automationSettingsService;
    private readonly IFilePickerService? _filePickerService;
    private readonly IInstanceProfileService? _instanceProfileService;
    private readonly ILauncherPreferencesService? _launcherPreferencesService;
    private readonly ILauncherStartupService? _launcherStartupService;

    [ObservableProperty] private string _workspaceRoot = string.Empty;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _closeToTrayOnExit;
    [ObservableProperty] private bool _startHiddenOnLaunch;
    [ObservableProperty] private bool _autoStartServerOnLaunch;
    [ObservableProperty] private bool _autoStartRobotOnLaunch;
    [ObservableProperty] private InstanceProfile? _selectedStartupProfile;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<InstanceProfile> StartupProfiles { get; } = [];

    public string DefaultWorkspaceRoot => _instanceProfileService?.GetDefaultWorkspaceRoot() ?? string.Empty;

    public bool HasStartupProfiles => StartupProfiles.Count > 0;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (_filePickerService is null) return;

        var selected = await _filePickerService.PickFolderAsync(L("CommonBrowseButtonText"));
        if (string.IsNullOrWhiteSpace(selected))
            return;

        WorkspaceRoot = selected.Trim();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_launcherPreferencesService is null) return;
        try
        {
            IsBusy = true;
            var preferences = _launcherPreferencesService.Load();
            preferences.WorkspaceRoot = string.IsNullOrWhiteSpace(WorkspaceRoot) ? string.Empty : WorkspaceRoot.Trim();
            preferences.StartWithWindows = StartWithWindows;
            preferences.CloseToTrayOnExit = CloseToTrayOnExit;
            preferences.StartHiddenOnLaunch = StartHiddenOnLaunch;
            preferences.AutoStartServerOnLaunch = AutoStartServerOnLaunch;
            preferences.AutoStartRobotOnLaunch = AutoStartRobotOnLaunch;
            preferences.AutoStartServerProfileId = SelectedStartupProfile?.Id ?? string.Empty;
            _launcherPreferencesService.Save(preferences);
            _launcherStartupService?.SetEnabled(StartWithWindows);
            if (_automationSettingsService is not null)
                await _automationSettingsService.LoadAsync();
            StatusMessage = L("ConfigStatusSaved");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        WorkspaceRoot = DefaultWorkspaceRoot;
    }

    private void LoadCurrent()
    {
        if (_launcherPreferencesService is null)
            return;

        var preferences = _launcherPreferencesService.Load();
        WorkspaceRoot = string.IsNullOrWhiteSpace(preferences.WorkspaceRoot)
            ? DefaultWorkspaceRoot
            : preferences.WorkspaceRoot;
        StartWithWindows = _launcherStartupService?.IsEnabled() ?? preferences.StartWithWindows;
        CloseToTrayOnExit = preferences.CloseToTrayOnExit;
        StartHiddenOnLaunch = preferences.StartHiddenOnLaunch;
        AutoStartServerOnLaunch = preferences.AutoStartServerOnLaunch;
        AutoStartRobotOnLaunch = preferences.AutoStartRobotOnLaunch;

        LoadProfiles(preferences.AutoStartServerProfileId);
    }

    private void LoadProfiles(string preferredProfileId)
    {
        StartupProfiles.Clear();

        var profiles = _instanceProfileService?.GetProfiles() ?? [];
        foreach (var profile in profiles)
        {
            StartupProfiles.Add(profile);
        }

        SelectedStartupProfile = StartupProfiles.FirstOrDefault(profile =>
                                   !string.IsNullOrWhiteSpace(preferredProfileId) &&
                                   profile.Id.Equals(preferredProfileId, StringComparison.OrdinalIgnoreCase))
                               ?? StartupProfiles.FirstOrDefault();

        OnPropertyChanged(nameof(HasStartupProfiles));
    }

    public DirectorySettingsViewModel()
    {
    }

    public DirectorySettingsViewModel(
        ILauncherPreferencesService launcherPreferencesService,
        IInstanceProfileService instanceProfileService,
        IFilePickerService filePickerService,
        IAutomationSettingsService automationSettingsService,
        ILauncherStartupService launcherStartupService)
    {
        _launcherPreferencesService = launcherPreferencesService;
        _instanceProfileService = instanceProfileService;
        _filePickerService = filePickerService;
        _automationSettingsService = automationSettingsService;
        _launcherStartupService = launcherStartupService;
        LoadCurrent();
    }
}
