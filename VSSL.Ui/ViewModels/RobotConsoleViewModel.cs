using System.Collections.ObjectModel;
using VSSL.Abstractions.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     机器人控制台页面视图模型
/// </summary>
public partial class RobotConsoleViewModel : ViewModelBase
{
    private readonly IRobotService? _robotService;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _runtimeStateText = "未运行";

    public ObservableCollection<string> ConsoleLines { get; } = [];

    public bool HasConsoleLines => ConsoleLines.Count > 0;

    public bool HasNoConsoleLines => !HasConsoleLines;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_robotService is null) return;

        try
        {
            var status = _robotService.GetCurrentStatus();
            IsRunning = status.IsRunning;
            RuntimeStateText = status.IsRunning
                ? $"运行中（WS: {status.OneBotWsUrl}，启动时间: {status.StartedAtUtc?.ToLocalTime():yyyy-MM-dd HH:mm:ss}）"
                : "未运行";

            var lines = _robotService.GetConsoleLines();
            ConsoleLines.Clear();
            foreach (var line in lines) ConsoleLines.Add(line);
            OnPropertyChanged(nameof(HasConsoleLines));
            OnPropertyChanged(nameof(HasNoConsoleLines));

            StatusMessage = $"控制台已刷新，共 {ConsoleLines.Count} 行。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"刷新控制台失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (_robotService is null) return;

        try
        {
            IsBusy = true;
            var settings = await _robotService.LoadSettingsAsync();
            await _robotService.StartAsync(settings);
            await RefreshAsync();
            StatusMessage = "机器人已启动。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"启动机器人失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (_robotService is null) return;

        try
        {
            IsBusy = true;
            await _robotService.StopAsync(TimeSpan.FromSeconds(5));
            await RefreshAsync();
            StatusMessage = "机器人已停止。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"停止机器人失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (_robotService is null) return;
        _robotService.ClearConsole();
        await RefreshAsync();
        StatusMessage = "控制台日志已清空。";
    }

    #region Constructors

    public RobotConsoleViewModel()
    {
    }

    public RobotConsoleViewModel(IRobotService robotService)
    {
        _robotService = robotService;
        ConsoleLines.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasConsoleLines));
            OnPropertyChanged(nameof(HasNoConsoleLines));
        };
        _ = RefreshAsync();
    }

    #endregion
}
