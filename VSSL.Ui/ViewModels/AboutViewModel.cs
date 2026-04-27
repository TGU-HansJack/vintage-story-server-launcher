using System.Globalization;
using System.Reflection;
using System.Text;
using Avalonia.Platform;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     关于页面
/// </summary>
public partial class AboutViewModel : ViewModelBase
{
    private const string CommunityUrl = "https://vintagestory.top/";
    private const string ReadmeAssetUri = "avares://VSSL.Ui/Assets/Docs/README.md";

    private readonly IBrowserService? _browserService;
    private readonly IUpdateService? _updateService;
    private string? _latestReleaseUrl;

    public string RepositoryUrl { get; }

    public string IssuesUrl { get; }

    public string ProjectReadme { get; }

    [ObservableProperty] private string _currentVersion = GetCurrentVersion();
    [ObservableProperty] private string _updateStatusMessage = string.Empty;
    [ObservableProperty] private bool _isCheckingUpdate;
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private string _latestVersion = string.Empty;

    public AboutViewModel()
    {
        RepositoryUrl = "https://github.com/TGU-HansJack/vintage-story-server-launcher";
        IssuesUrl = $"{RepositoryUrl}/issues";
        ProjectReadme = LoadReadmeText();
        UpdateStatusMessage = "Click \"Check Updates\" to fetch latest version.";
    }

    public AboutViewModel(
        IConfiguration configuration,
        IBrowserService browserService,
        IUpdateService updateService)
    {
        _browserService = browserService;
        _updateService = updateService;
        RepositoryUrl = configuration["Links:Repository"]
            ?? "https://github.com/TGU-HansJack/vintage-story-server-launcher";
        IssuesUrl = configuration["Links:BugReport"]
            ?? "https://github.com/TGU-HansJack/vintage-story-server-launcher/issues";
        ProjectReadme = LoadReadmeText();
        UpdateStatusMessage = L("AboutUpdateStatusIdle");
    }

    [RelayCommand]
    private void OpenCommunity()
    {
        _browserService?.OpenPage(CommunityUrl);
    }

    [RelayCommand]
    private void OpenIssues()
    {
        _browserService?.OpenPage(IssuesUrl);
    }

    [RelayCommand]
    private void OpenRepository()
    {
        _browserService?.OpenPage(RepositoryUrl);
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        if (_updateService is null || IsCheckingUpdate)
            return;

        try
        {
            IsCheckingUpdate = true;
            IsUpdateAvailable = false;
            UpdateStatusMessage = L("AboutUpdateStatusChecking");

            AppUpdateInfo latest = await _updateService.CheckLatestReleaseAsync(RepositoryUrl, CurrentVersion);
            _latestReleaseUrl = latest.ReleasePageUrl;
            LatestVersion = latest.LatestVersion;
            IsUpdateAvailable = latest.IsUpdateAvailable;

            UpdateStatusMessage = latest.IsUpdateAvailable
                ? LF("AboutUpdateStatusAvailableFormat", latest.LatestVersion, latest.CurrentVersion)
                : LF("AboutUpdateStatusLatestFormat", latest.CurrentVersion);
        }
        catch (Exception ex)
        {
            IsUpdateAvailable = false;
            UpdateStatusMessage = LF("AboutUpdateStatusFailedFormat", ex.Message);
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private void OpenLatestRelease()
    {
        var target = string.IsNullOrWhiteSpace(_latestReleaseUrl)
            ? $"{RepositoryUrl.TrimEnd('/')}/releases/latest"
            : _latestReleaseUrl;

        _browserService?.OpenPage(target);
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AboutViewModel).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return NormalizeVersion(informational);

        var assemblyVersion = assembly.GetName().Version?.ToString();
        return NormalizeVersion(assemblyVersion ?? "0.0.0");
    }

    private static string NormalizeVersion(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
            trimmed = trimmed[1..];

        var plusIndex = trimmed.IndexOf('+');
        if (plusIndex >= 0)
            trimmed = trimmed[..plusIndex];

        return string.IsNullOrWhiteSpace(trimmed) ? "0.0.0" : trimmed;
    }

    private static string LoadReadmeText()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(ReadmeAssetUri));
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            return CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? $"README.md 加载失败：{ex.Message}"
                : $"Failed to load README.md: {ex.Message}";
        }
    }
}
