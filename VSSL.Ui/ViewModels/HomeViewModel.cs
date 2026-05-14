using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using VSSL.Abstractions.Services;
using VSSL.Abstractions.Services.Ui;
using VSSL.Domains.Models;
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
    private readonly IOverviewLinkageService? _overviewLinkageService;
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

    [ObservableProperty] private string _linkageStatusMessage = string.Empty;
    [ObservableProperty] private string _linkageListenPrefix = "http://127.0.0.1:18089/";
    [ObservableProperty] private bool _linkageEnabled = true;
    [ObservableProperty] private bool _linkageAllowInsecureHttp;
    [ObservableProperty] private int _linkageRequestTimeoutSec = 8;
    [ObservableProperty] private bool _linkageIncludeServerInfo = true;
    [ObservableProperty] private bool _linkageIncludePlayers = true;
    [ObservableProperty] private bool _linkageIncludePlayerEvents = true;
    [ObservableProperty] private bool _linkageIncludeChats = true;
    [ObservableProperty] private bool _linkageIncludeNotifications = true;
    [ObservableProperty] private bool _linkageIncludeMapData = true;
    [ObservableProperty] private bool _linkageIncludeImages = true;
    [ObservableProperty] private string _newEndpointHost = string.Empty;
    [ObservableProperty] private string _newEndpointToken = string.Empty;
    [ObservableProperty] private bool _linkageListening;

    public ObservableCollection<HomeLinkageEndpointItemViewModel> LinkageEndpoints { get; } = [];
    public ObservableCollection<string> LinkageLogs { get; } = [];

    public string ServerStateText => ServerRunning ? L("CommonRunningState") : L("CommonStoppedState");

    public string RobotStateText => RobotRunning ? L("CommonRunningState") : L("CommonStoppedState");

    public string ServerMemoryLineText => LF("HomeMemoryFormat", ServerMemoryText);

    public string RobotMemoryLineText => LF("HomeMemoryFormat", RobotMemoryText);

    public string ServerUptimeLineText => LF("HomeUptimeFormat", ServerUptimeText);

    public string RobotUptimeLineText => LF("HomeUptimeFormat", RobotUptimeText);

    public string LinkageRuntimeStateText => LinkageListening ? L("CommonRunningState") : L("CommonStoppedState");

    partial void OnLinkageListeningChanged(bool value) => OnPropertyChanged(nameof(LinkageRuntimeStateText));

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

    [RelayCommand]
    private async Task RefreshLinkageAsync()
    {
        if (_overviewLinkageService is null)
        {
            return;
        }

        try
        {
            var settings = await _overviewLinkageService.LoadSettingsAsync();
            ApplyLinkageSettings(settings);
            ApplyLinkageRuntime(_overviewLinkageService.GetRuntimeStatus());
            LinkageStatusMessage = L("HomeLinkageStatusLoaded");
        }
        catch (Exception ex)
        {
            LinkageStatusMessage = LF("HomeLinkageStatusLoadFailedFormat", ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveLinkageAsync()
    {
        if (_overviewLinkageService is null)
        {
            return;
        }

        try
        {
            var settings = BuildLinkageSettings();
            await _overviewLinkageService.SaveSettingsAsync(settings);
            LinkageStatusMessage = L("HomeLinkageStatusSaved");
        }
        catch (Exception ex)
        {
            LinkageStatusMessage = LF("HomeLinkageStatusSaveFailedFormat", ex.Message);
        }
    }

    [RelayCommand]
    private async Task StartLinkageAsync()
    {
        if (_overviewLinkageService is null)
        {
            return;
        }

        try
        {
            var settings = BuildLinkageSettings();
            await _overviewLinkageService.StartAsync(settings);
            ApplyLinkageRuntime(_overviewLinkageService.GetRuntimeStatus());
            LinkageStatusMessage = L("HomeLinkageStatusStarted");
        }
        catch (Exception ex)
        {
            LinkageStatusMessage = LF("HomeLinkageStatusStartFailedFormat", ex.Message);
        }
    }

    [RelayCommand]
    private async Task StopLinkageAsync()
    {
        if (_overviewLinkageService is null)
        {
            return;
        }

        try
        {
            await _overviewLinkageService.StopAsync(TimeSpan.FromSeconds(8));
            ApplyLinkageRuntime(_overviewLinkageService.GetRuntimeStatus());
            LinkageStatusMessage = L("HomeLinkageStatusStopped");
        }
        catch (Exception ex)
        {
            LinkageStatusMessage = LF("HomeLinkageStatusStopFailedFormat", ex.Message);
        }
    }

    [RelayCommand]
    private void AddLinkageEndpoint()
    {
        var host = NewEndpointHost.Trim();
        var token = NewEndpointToken.Trim();
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(token))
        {
            LinkageStatusMessage = L("HomeLinkageStatusEndpointInputRequired");
            return;
        }

        if (LinkageEndpoints.Any(x => string.Equals(x.ServerHost.Trim(), host, StringComparison.OrdinalIgnoreCase)))
        {
            LinkageStatusMessage = L("HomeLinkageStatusEndpointDuplicate");
            return;
        }

        LinkageEndpoints.Add(new HomeLinkageEndpointItemViewModel
        {
            ServerHost = host,
            Token = token,
            Enabled = true
        });
        NewEndpointHost = string.Empty;
        NewEndpointToken = string.Empty;
        LinkageStatusMessage = L("HomeLinkageStatusEndpointAdded");
    }

    [RelayCommand]
    private void RemoveLinkageEndpoint(HomeLinkageEndpointItemViewModel? endpoint)
    {
        if (endpoint is null)
        {
            return;
        }

        LinkageEndpoints.Remove(endpoint);
        LinkageStatusMessage = L("HomeLinkageStatusEndpointRemoved");
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

    private void ApplyLinkageSettings(OverviewLinkageSettings settings)
    {
        LinkageEnabled = settings.Enabled;
        LinkageListenPrefix = settings.ListenPrefix;
        LinkageAllowInsecureHttp = settings.AllowInsecureHttp;
        LinkageRequestTimeoutSec = settings.RequestTimeoutSec;
        LinkageIncludeServerInfo = settings.IncludeServerInfo;
        LinkageIncludePlayers = settings.IncludePlayers;
        LinkageIncludePlayerEvents = settings.IncludePlayerEvents;
        LinkageIncludeChats = settings.IncludeChats;
        LinkageIncludeNotifications = settings.IncludeNotifications;
        LinkageIncludeMapData = settings.IncludeMapData;
        LinkageIncludeImages = settings.IncludeImages;

        LinkageEndpoints.Clear();
        foreach (var endpoint in settings.Endpoints ?? [])
        {
            LinkageEndpoints.Add(new HomeLinkageEndpointItemViewModel
            {
                ServerHost = endpoint.ServerHost,
                Token = endpoint.Token,
                Enabled = endpoint.Enabled
            });
        }
    }

    private OverviewLinkageSettings BuildLinkageSettings()
    {
        var endpoints = LinkageEndpoints
            .Select(x => new OverviewLinkageEndpointSettings
            {
                ServerHost = x.ServerHost.Trim(),
                Token = x.Token.Trim(),
                Enabled = x.Enabled
            })
            .ToList();

        return new OverviewLinkageSettings
        {
            Enabled = LinkageEnabled,
            ListenPrefix = LinkageListenPrefix.Trim(),
            AllowInsecureHttp = LinkageAllowInsecureHttp,
            RequestTimeoutSec = LinkageRequestTimeoutSec,
            IncludeServerInfo = LinkageIncludeServerInfo,
            IncludePlayers = LinkageIncludePlayers,
            IncludePlayerEvents = LinkageIncludePlayerEvents,
            IncludeChats = LinkageIncludeChats,
            IncludeNotifications = LinkageIncludeNotifications,
            IncludeMapData = LinkageIncludeMapData,
            IncludeImages = LinkageIncludeImages,
            Endpoints = endpoints
        };
    }

    private void ApplyLinkageRuntime(OverviewLinkageRuntimeStatus runtime)
    {
        LinkageListening = runtime.IsListening;

        var runtimeByHost = (runtime.Endpoints ?? [])
            .ToDictionary(x => x.ServerHost, x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var endpoint in LinkageEndpoints)
        {
            if (!runtimeByHost.TryGetValue(endpoint.ServerHost, out var status))
            {
                endpoint.LastServerName = string.Empty;
                endpoint.LastServerStatus = string.Empty;
                endpoint.LastPlayers = string.Empty;
                endpoint.LastPayloadTimeUtc = string.Empty;
                endpoint.LastReceivedUtc = string.Empty;
                endpoint.LastError = string.Empty;
                continue;
            }

            endpoint.LastServerName = status.LastServerName;
            endpoint.LastServerStatus = status.LastServerStatus;
            endpoint.LastPlayers = $"{status.LastOnlinePlayers}/{status.LastMaxPlayers}";
            endpoint.LastPayloadTimeUtc = status.LastPayloadTimeUtc;
            endpoint.LastReceivedUtc = FormatUtcDisplay(status.LastReceivedUtc);
            endpoint.LastError = status.LastError;
        }
    }

    private void OnLinkageOutput(object? sender, string line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LinkageLogs.Add(line);
            while (LinkageLogs.Count > 300)
            {
                LinkageLogs.RemoveAt(0);
            }
        });
    }

    private static string FormatUtcDisplay(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
        {
            return value;
        }

        var local = time.ToLocalTime();
        return local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
    }

    public void RefreshLinkageRuntime()
    {
        if (_overviewLinkageService is null)
        {
            return;
        }

        ApplyLinkageRuntime(_overviewLinkageService.GetRuntimeStatus());
    }

    #region Constructors

    public HomeViewModel()
    {
    }

    public HomeViewModel(ISystemStatusService systemStatusService, IOverviewLinkageService overviewLinkageService)
    {
        _systemStatusService = systemStatusService;
        _overviewLinkageService = overviewLinkageService;
        _overviewLinkageService.OutputReceived += OnLinkageOutput;
        RefreshMetrics();
        _ = RefreshLinkageAsync();
    }

    public HomeViewModel(ISystemStatusService systemStatusService, IThemeService themeService, IOverviewLinkageService overviewLinkageService)
    {
        _systemStatusService = systemStatusService;
        _overviewLinkageService = overviewLinkageService;
        _overviewLinkageService.OutputReceived += OnLinkageOutput;
        IsDarkMode = themeService.IsDarkMode;
        RefreshMetrics();
        _ = RefreshLinkageAsync();
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing && _overviewLinkageService is not null)
        {
            _overviewLinkageService.OutputReceived -= OnLinkageOutput;
        }

        base.Dispose(disposing);
    }
}
