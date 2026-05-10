using System.Globalization;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     机器人配置页面视图模型
/// </summary>
public partial class RobotConfigViewModel : ViewModelBase
{
    private readonly IRobotService? _robotService;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _oneBotWsUrl = "ws://127.0.0.1:3001/";
    [ObservableProperty] private string _accessToken = string.Empty;
    [ObservableProperty] private int _reconnectIntervalSec = 5;
    [ObservableProperty] private string _databasePath = string.Empty;
    [ObservableProperty] private double _pollIntervalSec = 1.0;
    [ObservableProperty] private string _defaultEncoding = "utf-8";
    [ObservableProperty] private string _fallbackEncoding = "gbk";
    [ObservableProperty] private string _superUsersText = string.Empty;
    [ObservableProperty] private string _ownersText = string.Empty;
    [ObservableProperty] private int _osqPollIntervalSec = 20;
    [ObservableProperty] private int _osqRequestTimeoutSec = 8;
    [ObservableProperty] private bool _osqAllowInsecureHttp;
    [ObservableProperty] private string _osqListenPrefix = "http://127.0.0.1:18089/";

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_robotService is null) return;

        try
        {
            IsBusy = true;
            var settings = await _robotService.LoadSettingsAsync();
            ApplySettings(settings);
            StatusMessage = L("RobotConfigStatusLoaded");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("RobotConfigStatusLoadFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_robotService is null) return;

        try
        {
            IsBusy = true;
            var settings = BuildSettings();
            await _robotService.SaveSettingsAsync(settings);
            StatusMessage = L("RobotConfigStatusSaved");
        }
        catch (Exception ex)
        {
            StatusMessage = LF("RobotConfigStatusSaveFailedFormat", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private RobotSettings BuildSettings()
    {
        return new RobotSettings
        {
            OneBotWsUrl = OneBotWsUrl.Trim(),
            AccessToken = AccessToken.Trim(),
            ReconnectIntervalSec = ReconnectIntervalSec,
            DatabasePath = DatabasePath.Trim(),
            PollIntervalSec = PollIntervalSec,
            DefaultEncoding = DefaultEncoding.Trim(),
            FallbackEncoding = FallbackEncoding.Trim(),
            SuperUsers = ParseSuperUsers(SuperUsersText),
            Owners = ParseOwners(OwnersText),
            OsqPollIntervalSec = OsqPollIntervalSec,
            OsqRequestTimeoutSec = OsqRequestTimeoutSec,
            OsqAllowInsecureHttp = OsqAllowInsecureHttp,
            OsqListenPrefix = OsqListenPrefix.Trim()
        };
    }

    private void ApplySettings(RobotSettings settings)
    {
        OneBotWsUrl = settings.OneBotWsUrl;
        AccessToken = settings.AccessToken ?? string.Empty;
        ReconnectIntervalSec = settings.ReconnectIntervalSec;
        DatabasePath = settings.DatabasePath;
        PollIntervalSec = settings.PollIntervalSec;
        DefaultEncoding = settings.DefaultEncoding;
        FallbackEncoding = settings.FallbackEncoding;
        SuperUsersText = string.Join(Environment.NewLine,
            settings.SuperUsers.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        OwnersText = string.Join(
            Environment.NewLine,
            (settings.Owners ?? [])
            .Where(o => o is not null && o.QqId > 0 && !string.IsNullOrWhiteSpace(o.ServerHost))
            .Select(o => $"{o.ServerHost} {o.QqId}"));
        OsqPollIntervalSec = settings.OsqPollIntervalSec;
        OsqRequestTimeoutSec = settings.OsqRequestTimeoutSec;
        OsqAllowInsecureHttp = settings.OsqAllowInsecureHttp;
        OsqListenPrefix = settings.OsqListenPrefix;
    }

    private static IReadOnlyList<long> ParseSuperUsers(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var tokens = text
            .Split(['\r', '\n', ',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim());

        var result = new List<long>();
        foreach (var token in tokens)
        {
            if (!long.TryParse(token, out var id)) continue;
            if (id <= 0) continue;
            result.Add(id);
        }

        return result.Distinct().ToList();
    }

    private static IReadOnlyList<RobotOwnerBinding> ParseOwners(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        List<RobotOwnerBinding> result = [];
        string[] lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] parts = line.Split([' ', '\t', ',', ';', '|'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            string serverHost = parts[0].Trim();
            if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long qqId))
            {
                continue;
            }

            if (serverHost.Length == 0 || qqId <= 0)
            {
                continue;
            }

            result.Add(new RobotOwnerBinding
            {
                ServerHost = serverHost,
                QqId = qqId
            });
        }

        return result
            .GroupBy(x => $"{x.ServerHost.ToLowerInvariant()}|{x.QqId}")
            .Select(g => g.First())
            .ToList();
    }

    #region Constructors

    public RobotConfigViewModel()
    {
    }

    public RobotConfigViewModel(IRobotService robotService)
    {
        _robotService = robotService;
        _ = RefreshAsync();
    }

    #endregion
}
