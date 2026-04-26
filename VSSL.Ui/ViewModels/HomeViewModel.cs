using System.Collections.ObjectModel;
using VSSL.Abstractions.Services;
using VSSL.Ui.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     总览页面视图模型（实时状态）
/// </summary>
public partial class HomeViewModel : RecipientViewModelBase, IRecipient<ThemeChangedMessage>
{
    private readonly ISystemStatusService? _systemStatusService;
    private readonly ObservableCollection<ObservablePoint> _serverMemoryPoints = [];
    private readonly ObservableCollection<ObservablePoint> _robotMemoryPoints = [];
    private readonly ObservableCollection<ObservablePoint> _playerPoints = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LegendTextPaint))]
    private bool _isDarkMode = true;

    [ObservableProperty] private string _serverStateText = "未运行";
    [ObservableProperty] private string _robotStateText = "未运行";
    [ObservableProperty] private string _serverMemoryText = "0 MB";
    [ObservableProperty] private string _robotMemoryText = "0 MB";
    [ObservableProperty] private string _serverUptimeText = "00:00:00";
    [ObservableProperty] private string _robotUptimeText = "00:00:00";
    [ObservableProperty] private string _onlinePlayersText = "0";

    public ObservableCollection<ISeries> MemorySeries { get; } =
    [
        new LineSeries<ObservablePoint>
        {
            Values = [],
            Name = "服务器内存(MB)",
            Stroke = new SolidColorPaint(new SKColor(91, 155, 213), 2),
            Fill = null,
            GeometrySize = 0
        },
        new LineSeries<ObservablePoint>
        {
            Values = [],
            Name = "机器人内存(MB)",
            Stroke = new SolidColorPaint(new SKColor(237, 125, 49), 2),
            Fill = null,
            GeometrySize = 0
        }
    ];

    public ObservableCollection<ISeries> PlayerSeries { get; } =
    [
        new LineSeries<ObservablePoint>
        {
            Values = [],
            Name = "在线玩家",
            Stroke = new SolidColorPaint(new SKColor(112, 173, 71), 2),
            Fill = null,
            GeometrySize = 0
        }
    ];

    public ObservableCollection<Axis> MemoryXAxes { get; } =
    [
        new()
        {
            Name = "采样点",
            Labeler = value => Math.Round(value).ToString("F0")
        }
    ];

    public ObservableCollection<Axis> MemoryYAxes { get; } =
    [
        new()
        {
            Name = "MB"
        }
    ];

    public ObservableCollection<Axis> PlayerXAxes { get; } =
    [
        new()
        {
            Name = "采样点",
            Labeler = value => Math.Round(value).ToString("F0")
        }
    ];

    public ObservableCollection<Axis> PlayerYAxes { get; } =
    [
        new()
        {
            Name = "人数",
            MinLimit = 0
        }
    ];

    public SolidColorPaint LegendTextPaint => IsDarkMode
        ? new SolidColorPaint(new SKColor(255, 255, 255))
        : new SolidColorPaint(new SKColor(0, 0, 0));

    [RelayCommand]
    public void RefreshMetrics()
    {
        if (_systemStatusService is null) return;

        var samples = _systemStatusService.GetRecentSamples(60);
        _serverMemoryPoints.Clear();
        _robotMemoryPoints.Clear();
        _playerPoints.Clear();

        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            var x = i + 1;
            _serverMemoryPoints.Add(new ObservablePoint(x, BytesToMb(sample.ServerMemoryBytes)));
            _robotMemoryPoints.Add(new ObservablePoint(x, BytesToMb(sample.RobotMemoryBytes)));
            _playerPoints.Add(new ObservablePoint(x, sample.OnlinePlayers));
        }

        if (MemorySeries[0] is LineSeries<ObservablePoint> serverSeries)
            serverSeries.Values = _serverMemoryPoints;
        if (MemorySeries[1] is LineSeries<ObservablePoint> robotSeries)
            robotSeries.Values = _robotMemoryPoints;
        if (PlayerSeries[0] is LineSeries<ObservablePoint> playerSeries)
            playerSeries.Values = _playerPoints;

        var latest = _systemStatusService.GetLatestSample();
        ServerStateText = latest.ServerRunning ? "运行中" : "未运行";
        RobotStateText = latest.RobotRunning ? "运行中" : "未运行";
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

    #endregion
}
