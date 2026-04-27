using VSSL.Abstractions.Services;
using VSSL.Abstractions.Services.Ui;
using VSSL.Ui.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     总览页面视图模型（实时状态）
/// </summary>
public partial class HomeViewModel : RecipientViewModelBase, IRecipient<ThemeChangedMessage>
{
    private readonly ISystemStatusService? _systemStatusService;
    private readonly List<double> _serverMemoryValues = [];
    private readonly List<double> _robotMemoryValues = [];
    private readonly List<double> _playerValues = [];

    [ObservableProperty] private bool _isDarkMode = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ServerStateText))]
    private bool _serverRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RobotStateText))]
    private bool _robotRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ServerMemoryLineText))]
    private string _serverMemoryText = "0 MB";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RobotMemoryLineText))]
    private string _robotMemoryText = "0 MB";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ServerUptimeLineText))]
    private string _serverUptimeText = "00:00:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RobotUptimeLineText))]
    private string _robotUptimeText = "00:00:00";

    [ObservableProperty] private string _onlinePlayersText = "0";
    [ObservableProperty] private IReadOnlyList<double> _serverMemorySeries = Array.Empty<double>();
    [ObservableProperty] private IReadOnlyList<double> _robotMemorySeries = Array.Empty<double>();
    [ObservableProperty] private IReadOnlyList<double> _playerSeries = Array.Empty<double>();

    public string ServerStateText => ServerRunning ? L("CommonRunningState") : L("CommonStoppedState");

    public string RobotStateText => RobotRunning ? L("CommonRunningState") : L("CommonStoppedState");

    public string ServerMemoryLineText => LF("HomeMemoryFormat", ServerMemoryText);

    public string RobotMemoryLineText => LF("HomeMemoryFormat", RobotMemoryText);

    public string ServerUptimeLineText => LF("HomeUptimeFormat", ServerUptimeText);

    public string RobotUptimeLineText => LF("HomeUptimeFormat", RobotUptimeText);

    [RelayCommand]
    public void RefreshMetrics()
    {
        if (_systemStatusService is null) return;

        var samples = _systemStatusService.GetRecentSamples(60);
        _serverMemoryValues.Clear();
        _robotMemoryValues.Clear();
        _playerValues.Clear();

        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            _serverMemoryValues.Add(BytesToMb(sample.ServerMemoryBytes));
            _robotMemoryValues.Add(BytesToMb(sample.RobotMemoryBytes));
            _playerValues.Add(sample.OnlinePlayers);
        }

        ServerMemorySeries = _serverMemoryValues.ToArray();
        RobotMemorySeries = _robotMemoryValues.ToArray();
        PlayerSeries = _playerValues.ToArray();

        var latest = _systemStatusService.GetLatestSample();
        ServerRunning = latest.ServerRunning;
        RobotRunning = latest.RobotRunning;
        ServerMemoryText = $"{BytesToMb(latest.ServerMemoryBytes):0.0} MB";
        RobotMemoryText = $"{BytesToMb(latest.RobotMemoryBytes):0.0} MB";
        ServerUptimeText = FormatDuration(latest.ServerUptime);
        RobotUptimeText = FormatDuration(latest.RobotUptime);
        OnlinePlayersText = latest.OnlinePlayers.ToString();
    }

    public void Receive(ThemeChangedMessage message)
    {
        IsDarkMode = message.Value;
    }

    private static double BytesToMb(long bytes)
    {
        return bytes <= 0 ? 0 : bytes / 1024d / 1024d;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return "00:00:00";
        var totalHours = (int)duration.TotalHours;
        return $"{totalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    #region Constructors

    public HomeViewModel()
    {
    }

    public HomeViewModel(ISystemStatusService systemStatusService)
    {
        _systemStatusService = systemStatusService;
        RefreshMetrics();
    }

    public HomeViewModel(ISystemStatusService systemStatusService, IThemeService themeService)
    {
        _systemStatusService = systemStatusService;
        IsDarkMode = themeService.IsDarkMode;
        RefreshMetrics();
    }

    #endregion
}
