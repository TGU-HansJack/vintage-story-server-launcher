using VSSL.Abstractions.Services;
using VSSL.Abstractions.Services.Ui;
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

    [ObservableProperty] private string _workspaceRoot = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public string DefaultWorkspaceRoot => _instanceProfileService?.GetDefaultWorkspaceRoot() ?? string.Empty;

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
            _launcherPreferencesService.Save(preferences);
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
    }

    public DirectorySettingsViewModel()
    {
    }

    public DirectorySettingsViewModel(
        ILauncherPreferencesService launcherPreferencesService,
        IInstanceProfileService instanceProfileService,
        IFilePickerService filePickerService,
        IAutomationSettingsService automationSettingsService)
    {
        _launcherPreferencesService = launcherPreferencesService;
        _instanceProfileService = instanceProfileService;
        _filePickerService = filePickerService;
        _automationSettingsService = automationSettingsService;
        LoadCurrent();
    }
}
