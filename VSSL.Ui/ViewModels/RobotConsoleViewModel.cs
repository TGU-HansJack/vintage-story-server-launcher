using System.Collections.ObjectModel;
using System.Globalization;
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
    [ObservableProperty] private bool _isConsoleAutoFollow = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeStateText))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeStateText))]
    private string _oneBotWsUrl = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeStateText))]
    private DateTimeOffset? _startedAtUtc;

    public ObservableCollection<string> ConsoleLines { get; } = [];

    public bool HasConsoleLines => ConsoleLines.Count > 0;

    public bool HasNoConsoleLines => !HasConsoleLines;

    public string RuntimeStateText => IsRunning
        ? LF("RobotConsoleRuntimeRunningFormat", string.IsNullOrWhiteSpace(OneBotWsUrl) ? "-" : OneBotWsUrl,
            FormatStartedAt(StartedAtUtc))
        : L("CommonStoppedState");

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_robotService is null) return;

        try
        {
            var status = _robotService.GetCurrentStatus();
            IsRunning = status.IsRunning;
            OneBotWsUrl = status.OneBotWsUrl ?? string.Empty;
            StartedAtUtc = status.StartedAtUtc;

            var lines = _robotService.GetConsoleLines();
            ConsoleLines.Clear();
            foreach (var line in lines) ConsoleLines.Add(line);
            OnPropertyChanged(nameof(HasConsoleLines));
            OnPropertyChanged(nameof(HasNoConsoleLines));
        }
        catch (Exception ex)
        {
            StatusMessage = LF("RobotConsoleStatusRefreshFailedFormat", ex.Message);
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
            StatusMessage = L("RobotConsoleStatusStarted");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("RobotConsoleStatusStartFailedFormat", ex.Message);
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
            StatusMessage = L("RobotConsoleStatusStopped");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("RobotConsoleStatusStopFailedFormat", ex.Message);
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
        StatusMessage = L("RobotConsoleStatusCleared");
    }

    private static string FormatStartedAt(DateTimeOffset? startedAtUtc)
    {
        if (!startedAtUtc.HasValue) return "-";
        return startedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
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
