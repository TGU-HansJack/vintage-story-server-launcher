using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using VSSL.Abstractions.Services;
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
    private readonly IServerProcessService? _serverProcessService;
    private readonly ILogTailService? _logTailService;

    [ObservableProperty] private InstanceProfile? _selectedProfile;
    [ObservableProperty] private string? _selectedInstalledVersion;
    [ObservableProperty] private string _quickCreateProfileName = string.Empty;
    [ObservableProperty] private string _consoleInput = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isConsoleAutoFollow = true;

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
    private DateTimeOffset? _runtimeStartedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartupProgressText))]
    private double _startupProgress;

    [ObservableProperty] private bool _isStartupProgressVisible;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];
    public ObservableCollection<string> InstalledVersions { get; } = [];

    public ObservableCollection<string> ConsoleLines { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public bool HasInstalledVersions => InstalledVersions.Count > 0;

    public bool HasNoInstalledVersions => !HasInstalledVersions;

    public bool HasConsoleLines => ConsoleLines.Count > 0;

    public bool HasNoConsoleLines => !HasConsoleLines;

    public string RuntimeStateText => IsRunning
        ? LF("WorkspaceRuntimeRunningFormat", RuntimeProcessId, OnlinePlayers, FormatStartedAt(RuntimeStartedAt))
        : L("CommonStoppedState");

    public string StartupProgressText => LF("WorkspaceStartupProgressFormat", StartupProgress);

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

            Profiles.Clear();
            foreach (var profile in profiles)
                Profiles.Add(profile);

            OnPropertyChanged(nameof(HasProfiles));
            OnPropertyChanged(nameof(HasNoProfiles));

            if (Profiles.Count == 0)
            {
                SelectedProfile = null;
                StatusMessage = HasInstalledVersions
                    ? L("WorkspaceQuickNoProfileTip")
                    : L("WorkspaceQuickNoVersionText");
                return;
            }

            SelectedProfile = Profiles.FirstOrDefault(profile =>
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
            await _logTailService.StartAsync(SelectedProfile);
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
        AppendConsoleLine(line);
    }

    private void OnLogTailLineReceived(object? sender, string line)
    {
        AppendConsoleLine($"[log] {line}");
    }

    private void OnProcessStatusChanged(object? sender, ServerRuntimeStatus status)
    {
        IsRunning = status.IsRunning;
        OnlinePlayers = status.OnlinePlayers;
        RuntimeProcessId = status.ProcessId ?? 0;
        RuntimeStartedAt = status.StartedAtUtc;
    }

    private void AppendConsoleLine(string line)
    {
        ConsoleLines.Add(line);
        while (ConsoleLines.Count > 3000)
            ConsoleLines.RemoveAt(0);

        OnPropertyChanged(nameof(HasConsoleLines));
        OnPropertyChanged(nameof(HasNoConsoleLines));
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

    #region Constructors

    public WorkspaceViewModel()
    {
    }

    public WorkspaceViewModel(
        IInstanceProfileService instanceProfileService,
        IServerProcessService serverProcessService,
        ILogTailService logTailService)
    {
        _instanceProfileService = instanceProfileService;
        _serverProcessService = serverProcessService;
        _logTailService = logTailService;

        _serverProcessService.OutputReceived += OnProcessOutputReceived;
        _serverProcessService.StatusChanged += OnProcessStatusChanged;
        _logTailService.LogLineReceived += OnLogTailLineReceived;

        OnProcessStatusChanged(this, _serverProcessService.GetCurrentStatus());
        _ = RefreshProfilesAsync();
    }

    #endregion
}
