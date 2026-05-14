using CommunityToolkit.Mvvm.ComponentModel;

namespace VSSL.Ui.ViewModels;

public partial class HomeLinkageEndpointItemViewModel : ObservableObject
{
    [ObservableProperty] private string _serverHost = string.Empty;

    [ObservableProperty] private string _token = string.Empty;

    [ObservableProperty] private bool _enabled = true;

    [ObservableProperty] private string _lastServerName = string.Empty;

    [ObservableProperty] private string _lastServerStatus = string.Empty;

    [ObservableProperty] private string _lastPlayers = string.Empty;

    [ObservableProperty] private string _lastPayloadTimeUtc = string.Empty;

    [ObservableProperty] private string _lastReceivedUtc = string.Empty;

    [ObservableProperty] private string _lastError = string.Empty;

    public string StatusLine
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(LastServerName) ? "-" : LastServerName;
            var status = string.IsNullOrWhiteSpace(LastServerStatus) ? "-" : LastServerStatus;
            var players = string.IsNullOrWhiteSpace(LastPlayers) ? "-" : LastPlayers;
            return $"{name} | {status} | {players}";
        }
    }

    partial void OnLastServerNameChanged(string value) => OnPropertyChanged(nameof(StatusLine));

    partial void OnLastServerStatusChanged(string value) => OnPropertyChanged(nameof(StatusLine));

    partial void OnLastPlayersChanged(string value) => OnPropertyChanged(nameof(StatusLine));
}
