using System.Collections.ObjectModel;
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
    [ObservableProperty] private string _consoleInput = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isConsoleAutoFollow = true;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private int _onlinePlayers;
    [ObservableProperty] private string _runtimeStateText = "未运行";
    [ObservableProperty] private double _startupProgress;
    [ObservableProperty] private bool _isStartupProgressVisible;

    public ObservableCollection<InstanceProfile> Profiles { get; } = [];

    public ObservableCollection<string> ConsoleLines { get; } = [];

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public bool HasConsoleLines => ConsoleLines.Count > 0;

    public bool HasNoConsoleLines => !HasConsoleLines;

    [RelayCommand]
    private async Task RefreshProfilesAsync()
    {
        if (_instanceProfileService is null) return;

        try
        {
            IsBusy = true;
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
                StatusMessage = "暂无档案，请先到实例/创建页面创建档案。";
                return;
            }

            SelectedProfile = Profiles.FirstOrDefault(profile =>
                                  !string.IsNullOrWhiteSpace(oldSelectedId) &&
                                  profile.Id.Equals(oldSelectedId, StringComparison.OrdinalIgnoreCase))
                              ?? Profiles[0];
            StatusMessage = $"已加载 {Profiles.Count} 个档案。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"刷新档案失败：{ex.Message}";
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
            StatusMessage = "请先选择档案。";
            return;
        }

        try
        {
            IsBusy = true;
            IsStartupProgressVisible = true;
            StartupProgress = 10;
            StatusMessage = "正在启动服务器...";

            await _serverProcessService.StartAsync(SelectedProfile);
            StartupProgress = 75;
            await _logTailService.StartAsync(SelectedProfile);
            StartupProgress = 100;
            StatusMessage = "服务器已启动。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"启动服务器失败：{ex.Message}";
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
            StatusMessage = "正在停止服务器...";
            await _serverProcessService.StopAsync(TimeSpan.FromSeconds(12));
            await _logTailService.StopAsync();
            StatusMessage = "服务器已停止。";
            StartupProgress = 0;
            IsStartupProgressVisible = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"停止服务器失败：{ex.Message}";
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
            StatusMessage = $"发送命令失败：{ex.Message}";
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
                StatusMessage = "当前没有可导出的日志。";
                return;
            }

            var exportDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VSSL",
                "workspace",
                "exports");
            Directory.CreateDirectory(exportDirectory);

            var filePath = Path.Combine(exportDirectory, $"console-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            await File.WriteAllLinesAsync(filePath, ConsoleLines.ToArray());
            StatusMessage = $"日志已导出：{filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出日志失败：{ex.Message}";
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
        RuntimeStateText = status.IsRunning
            ? $"运行中 (PID: {status.ProcessId}, 在线: {status.OnlinePlayers}, 启动: {status.StartedAtUtc?.ToLocalTime():yyyy-MM-dd HH:mm:ss})"
            : "未运行";
    }

    private void AppendConsoleLine(string line)
    {
        ConsoleLines.Add(line);
        while (ConsoleLines.Count > 3000)
            ConsoleLines.RemoveAt(0);

        OnPropertyChanged(nameof(HasConsoleLines));
        OnPropertyChanged(nameof(HasNoConsoleLines));
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
