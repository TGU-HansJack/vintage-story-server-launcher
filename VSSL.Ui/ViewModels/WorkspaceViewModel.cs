using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using Avalonia.Threading;
using VSSL.Abstractions.Services;
using VSSL.Abstractions.Services.Ui;
using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     服务器控制台页面视图模型
/// </summary>
public partial class WorkspaceViewModel : ViewModelBase
{
    private readonly IInstanceProfileService? _instanceProfileService;
    private readonly IInstanceSaveService? _instanceSaveService;
    private readonly IServerProcessService? _serverProcessService;
    private readonly ILogTailService? _logTailService;
    private readonly ILauncherPreferencesService? _launcherPreferencesService;
    private readonly IQuickCommandsDialogService? _quickCommandsDialogService;
    private string? _runningProfileId;
    private string? _tailingProfileId;
    private int? _runningNoticeProcessId;

    [ObservableProperty] private InstanceProfile? _selectedProfile;
    [ObservableProperty] private string? _selectedInstalledVersion;
    [ObservableProperty] private string _quickCreateProfileName = string.Empty;
    [ObservableProperty] private string _consoleInput = string.Empty;
    [ObservableProperty] private string? _selectedQuickCommand;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isConsoleAutoFollow = true;
    [ObservableProperty] private SaveFileItemViewModel? _selectedSave;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeStateText))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeStateText))]
    private int _onlinePlayers;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeStateText))]
    private int _runtimeProcessId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeStateText))]
    private bool _canSendCommands;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeStateText))]
    private DateTimeOffset? _runtimeStartedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartupProgressText))]
    private double _startupProgress;

    [ObservableProperty] private bool _isStartupProgressVisible;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];
    public ObservableCollection<SaveFileItemViewModel> Saves { get; } = [];
    public ObservableCollection<string> InstalledVersions { get; } = [];
    public ObservableCollection<string> QuickCommands { get; } = [];

    public ObservableCollection<string> ConsoleLines { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public bool HasSaves => Saves.Count > 0;

    public bool HasNoSaves => !HasSaves;

    public bool HasInstalledVersions => InstalledVersions.Count > 0;

    public bool HasNoInstalledVersions => !HasInstalledVersions;

    public bool HasConsoleLines => ConsoleLines.Count > 0;

    public bool HasNoConsoleLines => !HasConsoleLines;

    public bool HasQuickCommands => QuickCommands.Count > 0;

    public bool HasNoQuickCommands => !HasQuickCommands;

    public string RuntimeStateText
    {
        get
        {
            if (!IsRunning)
                return L("CommonStoppedState");

            var runningText = LF("WorkspaceRuntimeRunningFormat", RuntimeProcessId, OnlinePlayers,
                FormatStartedAt(RuntimeStartedAt));
            return CanSendCommands ? runningText : $"{runningText} {L("WorkspaceRuntimeReadOnlySuffix")}";
        }
    }

    public string StartupProgressText => LF("WorkspaceStartupProgressFormat", StartupProgress);

    partial void OnSelectedProfileChanged(InstanceProfile? value)
    {
        _ = LoadSavesForProfileAsync(value);
    }

    partial void OnSelectedSaveChanged(SaveFileItemViewModel? value)
    {
        if (value is null) return;
        _ = SetActiveSaveFromWorkspaceAsync(value);
    }

    partial void OnSelectedQuickCommandChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        ConsoleInput = value;
    }

    [RelayCommand]
    private async Task RefreshProfilesAsync()
    {
        if (_instanceProfileService is null) return;

        try
        {
            IsBusy = true;
            RefreshInstalledVersions();
            var oldSelectedId = SelectedProfile?.Id;
            var profiles = _instanceProfileService.GetProfiles();
            var runningProfileId = _serverProcessService?.GetCurrentStatus().ProfileId ?? _runningProfileId;

            Profiles.Clear();
            foreach (var profile in profiles)
                Profiles.Add(profile);

            OnPropertyChanged(nameof(HasProfiles));
            OnPropertyChanged(nameof(HasNoProfiles));

            if (Profiles.Count == 0)
            {
                SelectedProfile = null;
                Saves.Clear();
                SelectedSave = null;
                OnPropertyChanged(nameof(HasSaves));
                OnPropertyChanged(nameof(HasNoSaves));
                StatusMessage = HasInstalledVersions
                    ? L("WorkspaceQuickNoProfileTip")
                    : L("WorkspaceQuickNoVersionText");
                return;
            }

            SelectedProfile = Profiles.FirstOrDefault(profile =>
                                  !string.IsNullOrWhiteSpace(runningProfileId) &&
                                  profile.Id.Equals(runningProfileId, StringComparison.OrdinalIgnoreCase))
                              ?? Profiles.FirstOrDefault(profile =>
                                  !string.IsNullOrWhiteSpace(oldSelectedId) &&
                                  profile.Id.Equals(oldSelectedId, StringComparison.OrdinalIgnoreCase))
                              ?? Profiles[0];
            StatusMessage = LF("StatusLoadedProfilesFormat", Profiles.Count);
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

    [RelayCommand]
    private async Task CreateAndStartServerAsync()
    {
        if (_instanceProfileService is null || _serverProcessService is null || _logTailService is null)
            return;

        if (!HasInstalledVersions)
        {
            StatusMessage = L("WorkspaceQuickNoVersionText");
            return;
        }

        var version = string.IsNullOrWhiteSpace(SelectedInstalledVersion)
            ? InstalledVersions.FirstOrDefault()
            : SelectedInstalledVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            StatusMessage = L("WorkspaceQuickNoVersionText");
            return;
        }

        var profileName = string.IsNullOrWhiteSpace(QuickCreateProfileName)
            ? string.Format(CultureInfo.CurrentCulture, L("WorkspaceQuickDefaultProfileNameFormat"), DateTime.Now)
            : QuickCreateProfileName.Trim();

        try
        {
            IsBusy = true;
            StatusMessage = L("WorkspaceQuickCreatingText");

            var createdProfile = _instanceProfileService.CreateProfile(profileName, version);
            StatusMessage = LF("WorkspaceQuickCreatedFormat", createdProfile.Name);

            await RefreshProfilesAsync();
            SelectedProfile = Profiles.FirstOrDefault(profile =>
                                  profile.Id.Equals(createdProfile.Id, StringComparison.OrdinalIgnoreCase))
                              ?? createdProfile;
            QuickCreateProfileName = string.Empty;

            await StartServerAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = LF("WorkspaceQuickCreateFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartServerAsync()
    {
        if (_serverProcessService is null || _logTailService is null)
            return;
        if (SelectedProfile is null)
        {
            StatusMessage = L("StatusSelectProfileFirst");
            return;
        }

        try
        {
            IsBusy = true;
            IsStartupProgressVisible = true;
            StartupProgress = 10;
            StatusMessage = L("WorkspaceStatusStarting");

            await _serverProcessService.StartAsync(SelectedProfile);
            StartupProgress = 75;
            if (_tailingProfileId?.Equals(SelectedProfile.Id, StringComparison.OrdinalIgnoreCase) != true)
                await StartLogTailAsync(SelectedProfile);
            StartupProgress = 100;
            StatusMessage = L("WorkspaceStatusStarted");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("WorkspaceStatusStartFailedFormat", ex.Message);
            StartupProgress = 0;
        }
        finally
        {
            IsBusy = false;
            IsStartupProgressVisible = false;
        }
    }

    [RelayCommand]
    private async Task StopServerAsync()
    {
        if (_serverProcessService is null || _logTailService is null)
            return;

        try
        {
            IsBusy = true;
            StatusMessage = L("WorkspaceStatusStopping");
            await _serverProcessService.StopAsync(TimeSpan.FromSeconds(12));
            await _logTailService.StopAsync();
            StatusMessage = L("WorkspaceStatusStopped");
            StartupProgress = 0;
            IsStartupProgressVisible = false;
        }
        catch (Exception ex)
        {
            StatusMessage = LF("WorkspaceStatusStopFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SendConsoleCommandAsync()
    {
        if (_serverProcessService is null) return;
        if (string.IsNullOrWhiteSpace(ConsoleInput))
            return;

        try
        {
            await _serverProcessService.SendCommandAsync(ConsoleInput);
            ConsoleInput = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = LF("WorkspaceStatusSendCommandFailedFormat", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ManageQuickCommandsAsync()
    {
        if (_quickCommandsDialogService is null || _launcherPreferencesService is null)
        {
            return;
        }

        try
        {
            var edited = await _quickCommandsDialogService.ShowEditorAsync(
                L("WorkspaceQuickCommandsDialogTitle"),
                QuickCommands.ToList());
            if (edited is null)
            {
                return;
            }

            var normalized = edited
                .Where(command => !string.IsNullOrWhiteSpace(command))
                .Select(command => command.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(100)
                .ToList();

            ApplyQuickCommands(normalized);
            SaveQuickCommands(normalized);
            StatusMessage = L("ConfigStatusSaved");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("WorkspaceStatusSendCommandFailedFormat", ex.Message);
        }
    }

    [RelayCommand]
    private void ClearConsole()
    {
        ConsoleLines.Clear();
        OnPropertyChanged(nameof(HasConsoleLines));
        OnPropertyChanged(nameof(HasNoConsoleLines));
    }

    [RelayCommand]
    private async Task ExportConsoleAsync()
    {
        try
        {
            if (ConsoleLines.Count == 0)
            {
                StatusMessage = L("WorkspaceStatusNoExportLogs");
                return;
            }

            var workspaceRoot = _instanceProfileService?.GetWorkspaceRoot()
                                ?? Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                    "VSSL",
                                    "workspace");
            var exportDirectory = Path.Combine(workspaceRoot, "exports");
            Directory.CreateDirectory(exportDirectory);

            var filePath = Path.Combine(exportDirectory, $"console-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            await File.WriteAllLinesAsync(filePath, ConsoleLines.ToArray());
            TryOpenExportFolder(filePath);
            StatusMessage = LF("WorkspaceStatusExportedFormat", filePath);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("WorkspaceStatusExportFailedFormat", ex.Message);
        }
    }

    private static void TryOpenExportFolder(string filePath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
                return;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore shell launch failure to keep export successful.
        }
    }

    private void OnProcessOutputReceived(object? sender, string line)
    {
        RunOnUiThread(() => AppendConsoleLine(line));
    }

    private void OnLogTailLineReceived(object? sender, string line)
    {
        RunOnUiThread(() => AppendConsoleLine($"[log] {line}"));
    }

    private void OnProcessStatusChanged(object? sender, ServerRuntimeStatus status)
    {
        RunOnUiThread(() => ApplyProcessStatus(status));
    }

    private void ApplyProcessStatus(ServerRuntimeStatus status)
    {
        IsRunning = status.IsRunning;
        OnlinePlayers = status.OnlinePlayers;
        RuntimeProcessId = status.ProcessId ?? 0;
        RuntimeStartedAt = status.StartedAtUtc;
        CanSendCommands = status.CanSendCommands;
        _runningProfileId = status.ProfileId;

        if (!status.IsRunning)
        {
            _runningNoticeProcessId = null;
            StopLogTailForStoppedServer();
            return;
        }

        if (status.ProcessId.HasValue && _runningNoticeProcessId != status.ProcessId.Value)
        {
            _runningNoticeProcessId = status.ProcessId.Value;
            var controlText = status.CanSendCommands
                ? L("WorkspaceControlReadyText")
                : L("WorkspaceControlReadOnlyText");
            AppendConsoleLine($"[system] 已检测到服务端正在运行，PID={status.ProcessId.Value}。{controlText}");
        }

        if (!string.IsNullOrWhiteSpace(status.ProfileId))
            SelectAndTailRunningProfile(status.ProfileId);
    }

    private void AppendConsoleLine(string line)
    {
        ConsoleLines.Add(line);
        while (ConsoleLines.Count > 3000)
            ConsoleLines.RemoveAt(0);

        OnPropertyChanged(nameof(HasConsoleLines));
        OnPropertyChanged(nameof(HasNoConsoleLines));
    }

    private void SelectAndTailRunningProfile(string profileId)
    {
        var profile = Profiles.FirstOrDefault(item =>
            item.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));

        if (profile is null && _instanceProfileService is not null)
        {
            profile = _instanceProfileService.GetProfileById(profileId);
            if (profile is not null && Profiles.All(item =>
                    !item.Id.Equals(profile.Id, StringComparison.OrdinalIgnoreCase)))
            {
                Profiles.Add(profile);
                OnPropertyChanged(nameof(HasProfiles));
                OnPropertyChanged(nameof(HasNoProfiles));
            }
        }

        if (profile is null)
            return;

        if (SelectedProfile?.Id.Equals(profile.Id, StringComparison.OrdinalIgnoreCase) != true)
            SelectedProfile = profile;

        StartLogTailIfNeeded(profile);
    }

    private void StartLogTailIfNeeded(InstanceProfile profile)
    {
        if (_logTailService is null)
            return;
        if (_tailingProfileId?.Equals(profile.Id, StringComparison.OrdinalIgnoreCase) == true)
            return;

        _ = StartLogTailAsync(profile);
    }

    private async Task StartLogTailAsync(InstanceProfile profile)
    {
        _tailingProfileId = profile.Id;

        try
        {
            await _logTailService!.StartAsync(profile);
        }
        catch (Exception ex)
        {
            if (_tailingProfileId?.Equals(profile.Id, StringComparison.OrdinalIgnoreCase) == true)
                _tailingProfileId = null;
            RunOnUiThread(() => StatusMessage = LF("WorkspaceStatusStartFailedFormat", ex.Message));
        }
    }

    private void StopLogTailForStoppedServer()
    {
        if (_logTailService is null || _tailingProfileId is null)
            return;

        _tailingProfileId = null;
        _ = _logTailService.StopAsync();
    }

    private async Task LoadSavesForProfileAsync(InstanceProfile? profile)
    {
        if (_instanceSaveService is null || profile is null)
        {
            Saves.Clear();
            SelectedSave = null;
            OnPropertyChanged(nameof(HasSaves));
            OnPropertyChanged(nameof(HasNoSaves));
            return;
        }

        try
        {
            var entries = await _instanceSaveService.GetSavesAsync(profile);
            var previousPath = SelectedSave?.FullPath;
            var activePath = profile.ActiveSaveFile;

            Saves.Clear();
            foreach (var entry in entries)
            {
                Saves.Add(new SaveFileItemViewModel
                {
                    FullPath = entry.FullPath,
                    FileName = entry.FileName,
                    SizeBytes = entry.SizeBytes,
                    LastWriteTimeUtc = entry.LastWriteTimeUtc,
                    IsActive = entry.FullPath.Equals(activePath, StringComparison.OrdinalIgnoreCase)
                });
            }

            SelectedSave = Saves.FirstOrDefault(item =>
                               !string.IsNullOrWhiteSpace(activePath) &&
                               item.FullPath.Equals(activePath, StringComparison.OrdinalIgnoreCase))
                           ?? Saves.FirstOrDefault(item =>
                               !string.IsNullOrWhiteSpace(previousPath) &&
                               item.FullPath.Equals(previousPath, StringComparison.OrdinalIgnoreCase))
                           ?? Saves.FirstOrDefault();

            OnPropertyChanged(nameof(HasSaves));
            OnPropertyChanged(nameof(HasNoSaves));
        }
        catch (Exception ex)
        {
            Saves.Clear();
            SelectedSave = null;
            OnPropertyChanged(nameof(HasSaves));
            OnPropertyChanged(nameof(HasNoSaves));
            StatusMessage = LF("WorkspaceStatusLoadSavesFailedFormat", ex.Message);
        }
    }

    private async Task SetActiveSaveFromWorkspaceAsync(SaveFileItemViewModel selected)
    {
        if (_instanceProfileService is null || _instanceSaveService is null) return;

        var profile = SelectedProfile;
        if (profile is null) return;
        if (string.IsNullOrWhiteSpace(selected.FullPath)) return;

        var selectedPath = Path.GetFullPath(selected.FullPath);
        if (selectedPath.Equals(profile.ActiveSaveFile, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            await _instanceSaveService.SetActiveSaveAsync(profile, selectedPath);
            profile.ActiveSaveFile = selectedPath;
            profile.SaveDirectory = Path.GetDirectoryName(selectedPath) ?? profile.SaveDirectory;
            profile.LastUpdatedUtc = DateTimeOffset.UtcNow;
            _instanceProfileService.UpdateProfile(profile);

            await LoadSavesForProfileAsync(profile);
            var selectedName = Path.GetFileName(selectedPath);
            StatusMessage = LF("WorkspaceStatusActiveSaveSwitchedFormat", selectedName);
        }
        catch (Exception ex)
        {
            StatusMessage = LF("WorkspaceStatusSwitchSaveFailedFormat", ex.Message);
            await LoadSavesForProfileAsync(profile);
        }
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    private static string FormatStartedAt(DateTimeOffset? startedAtUtc)
    {
        if (!startedAtUtc.HasValue) return "-";

        return startedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
    }

    private void RefreshInstalledVersions()
    {
        if (_instanceProfileService is null)
        {
            InstalledVersions.Clear();
            OnPropertyChanged(nameof(HasInstalledVersions));
            OnPropertyChanged(nameof(HasNoInstalledVersions));
            return;
        }

        var oldSelectedVersion = SelectedInstalledVersion;
        var versions = _instanceProfileService.GetInstalledVersions();

        InstalledVersions.Clear();
        foreach (var version in versions)
            InstalledVersions.Add(version);

        OnPropertyChanged(nameof(HasInstalledVersions));
        OnPropertyChanged(nameof(HasNoInstalledVersions));

        SelectedInstalledVersion = InstalledVersions.FirstOrDefault(version =>
                                       !string.IsNullOrWhiteSpace(oldSelectedVersion) &&
                                       version.Equals(oldSelectedVersion, StringComparison.OrdinalIgnoreCase))
                                   ?? InstalledVersions.FirstOrDefault();
    }

    private void LoadQuickCommands()
    {
        if (_launcherPreferencesService is null)
        {
            ApplyQuickCommands([]);
            return;
        }

        var preferences = _launcherPreferencesService.Load();
        var commands = (preferences.QuickCommands ?? [])
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Select(command => command.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        ApplyQuickCommands(commands);
    }

    private void ApplyQuickCommands(IReadOnlyList<string> commands)
    {
        var previous = SelectedQuickCommand;
        QuickCommands.Clear();
        foreach (var command in commands)
        {
            QuickCommands.Add(command);
        }

        SelectedQuickCommand = QuickCommands.FirstOrDefault(command =>
            !string.IsNullOrWhiteSpace(previous) &&
            command.Equals(previous, StringComparison.OrdinalIgnoreCase))
            ?? QuickCommands.FirstOrDefault();

        OnPropertyChanged(nameof(HasQuickCommands));
        OnPropertyChanged(nameof(HasNoQuickCommands));
    }

    private void SaveQuickCommands(IReadOnlyList<string> commands)
    {
        if (_launcherPreferencesService is null)
        {
            return;
        }

        var preferences = _launcherPreferencesService.Load();
        preferences.QuickCommands = commands.ToList();
        _launcherPreferencesService.Save(preferences);
    }

    #region Constructors

    public WorkspaceViewModel()
    {
    }

    public WorkspaceViewModel(
        IInstanceProfileService instanceProfileService,
        IInstanceSaveService instanceSaveService,
        IServerProcessService serverProcessService,
        ILogTailService logTailService,
        ILauncherPreferencesService launcherPreferencesService,
        IQuickCommandsDialogService quickCommandsDialogService)
    {
        _instanceProfileService = instanceProfileService;
        _instanceSaveService = instanceSaveService;
        _serverProcessService = serverProcessService;
        _logTailService = logTailService;
        _launcherPreferencesService = launcherPreferencesService;
        _quickCommandsDialogService = quickCommandsDialogService;

        _serverProcessService.OutputReceived += OnProcessOutputReceived;
        _serverProcessService.StatusChanged += OnProcessStatusChanged;
        _logTailService.LogLineReceived += OnLogTailLineReceived;

        OnProcessStatusChanged(this, _serverProcessService.GetCurrentStatus());
        LoadQuickCommands();
        _ = RefreshProfilesAsync();
    }

    #endregion
}
