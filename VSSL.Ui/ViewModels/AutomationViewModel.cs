using System.Collections.ObjectModel;
using Avalonia.Threading;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     自动化页面
/// </summary>
public partial class AutomationViewModel : ViewModelBase
{
    private readonly IAutomationService? _automationService;
    private readonly IAutomationSettingsService? _automationSettingsService;
    private readonly IInstanceProfileService? _instanceProfileService;
    private readonly IServerProcessService? _serverProcessService;

    [ObservableProperty] private InstanceProfile? _selectedProfile;
    [ObservableProperty] private bool _restartSchedulerEnabled;
    [ObservableProperty] private bool _backupEnabled;
    [ObservableProperty] private bool _backupBeforeShutdown = true;
    [ObservableProperty] private bool _broadcastEnabled;
    [ObservableProperty] private bool _exportLogEnabled;
    [ObservableProperty] private bool _exportBeforeShutdown = true;
    [ObservableProperty] private bool _exportIncludeChat = true;
    [ObservableProperty] private bool _exportIncludeServerInfo = true;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];
    public ObservableCollection<AutomationActionWindow> ActionWindows { get; } = [];
    public ObservableCollection<AutomationBackupTimeItemViewModel> BackupTimes { get; } = [];
    public ObservableCollection<ScheduledBroadcastMessage> BroadcastMessages { get; } = [];
    public ObservableCollection<AutomationExportTimeItemViewModel> ExportTimes { get; } = [];
    public ObservableCollection<string> RuntimeLogs { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;
    public bool HasNoProfiles => !HasProfiles;

    partial void OnSelectedProfileChanged(InstanceProfile? value)
    {
        if (_automationSettingsService is null) return;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_automationSettingsService is null || _instanceProfileService is null) return;

        try
        {
            IsBusy = true;
            await LoadProfilesAsync();
            await LoadSettingsAsync();
            StatusMessage = LF("StatusLoadedProfilesFormat", Profiles.Count);
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
    private void AddActionWindow()
    {
        ActionWindows.Add(new AutomationActionWindow());
    }

    [RelayCommand]
    private void RemoveActionWindow(AutomationActionWindow? window)
    {
        if (window is null) return;
        ActionWindows.Remove(window);
    }

    [RelayCommand]
    private void AddBackupTime()
    {
        BackupTimes.Add(new AutomationBackupTimeItemViewModel
        {
            Time = "03:00"
        });
    }

    [RelayCommand]
    private void RemoveBackupTime(AutomationBackupTimeItemViewModel? item)
    {
        if (item is null) return;
        BackupTimes.Remove(item);
    }

    [RelayCommand]
    private void AddBroadcast()
    {
        BroadcastMessages.Add(new ScheduledBroadcastMessage());
    }

    [RelayCommand]
    private void RemoveBroadcast(ScheduledBroadcastMessage? message)
    {
        if (message is null) return;
        BroadcastMessages.Remove(message);
    }

    [RelayCommand]
    private void AddExportTime()
    {
        ExportTimes.Add(new AutomationExportTimeItemViewModel
        {
            Time = "12:00"
        });
    }

    [RelayCommand]
    private void RemoveExportTime(AutomationExportTimeItemViewModel? item)
    {
        if (item is null) return;
        ExportTimes.Remove(item);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_automationSettingsService is null) return;

        try
        {
            IsBusy = true;
            var settings = BuildSettings();
            await _automationSettingsService.SaveAsync(settings);
            if (_automationService is not null)
                await _automationService.ReloadAsync();
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
    private void SyncRuntimeLogs()
    {
        if (_automationService is null) return;
        RuntimeLogs.Clear();
        foreach (var line in _automationService.GetRuntimeLogs())
            RuntimeLogs.Add(line);
    }

    private async Task LoadProfilesAsync()
    {
        if (_instanceProfileService is null)
            return;

        var oldSelectedId = SelectedProfile?.Id;
        var profiles = _instanceProfileService.GetProfiles();

        Profiles.Clear();
        foreach (var profile in profiles)
            Profiles.Add(profile);

        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(HasNoProfiles));

        SelectedProfile = Profiles.FirstOrDefault(profile =>
                              !string.IsNullOrWhiteSpace(oldSelectedId) &&
                              profile.Id.Equals(oldSelectedId, StringComparison.OrdinalIgnoreCase))
                          ?? Profiles.FirstOrDefault();
    }

    private async Task LoadSettingsAsync()
    {
        if (_automationSettingsService is null)
            return;

        var settings = await _automationSettingsService.LoadAsync();
        RestartSchedulerEnabled = settings.RestartSchedulerEnabled;
        BackupEnabled = settings.BackupEnabled;
        BackupBeforeShutdown = settings.BackupBeforeShutdown;
        BroadcastEnabled = settings.BroadcastEnabled;
        ExportLogEnabled = settings.ExportLogEnabled;
        ExportBeforeShutdown = settings.ExportBeforeShutdown;
        ExportIncludeChat = settings.ExportIncludeChat;
        ExportIncludeServerInfo = settings.ExportIncludeServerInfo;

        ActionWindows.Clear();
        foreach (var window in settings.ActionWindows)
            ActionWindows.Add(window);

        BackupTimes.Clear();
        foreach (var time in settings.BackupTimes)
        {
            BackupTimes.Add(new AutomationBackupTimeItemViewModel
            {
                Time = time
            });
        }

        BroadcastMessages.Clear();
        foreach (var message in settings.BroadcastMessages)
            BroadcastMessages.Add(message);

        ExportTimes.Clear();
        foreach (var time in settings.ExportTimes)
        {
            ExportTimes.Add(new AutomationExportTimeItemViewModel
            {
                Time = time
            });
        }

        SelectedProfile = Profiles.FirstOrDefault(profile =>
            profile.Id.Equals(settings.TargetProfileId, StringComparison.OrdinalIgnoreCase))
            ?? Profiles.FirstOrDefault();
    }

    private AutomationSettings BuildSettings()
    {
        return new AutomationSettings
        {
            TargetProfileId = SelectedProfile?.Id ?? string.Empty,
            RestartSchedulerEnabled = RestartSchedulerEnabled,
            BackupEnabled = BackupEnabled,
            BackupTimes = BackupTimes
                .Select(item => item.Time?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            BackupBeforeShutdown = BackupBeforeShutdown,
            BroadcastEnabled = BroadcastEnabled,
            ExportLogEnabled = ExportLogEnabled,
            ExportBeforeShutdown = ExportBeforeShutdown,
            ExportIncludeChat = ExportIncludeChat,
            ExportIncludeServerInfo = ExportIncludeServerInfo,
            ActionWindows = ActionWindows.ToList(),
            BroadcastMessages = BroadcastMessages.ToList(),
            ExportTimes = ExportTimes
                .Select(item => item.Time?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public AutomationViewModel()
    {
    }

    public AutomationViewModel(
        IAutomationService automationService,
        IAutomationSettingsService automationSettingsService,
        IInstanceProfileService instanceProfileService,
        IServerProcessService serverProcessService)
    {
        _automationService = automationService;
        _automationSettingsService = automationSettingsService;
        _instanceProfileService = instanceProfileService;
        _serverProcessService = serverProcessService;

        _automationService.RuntimeLogReceived += (_, line) =>
        {
            RunOnUiThread(() =>
            {
                RuntimeLogs.Add(line);
                while (RuntimeLogs.Count > 2000)
                    RuntimeLogs.RemoveAt(0);
            });
        };

        _ = RefreshAsync();
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
}
