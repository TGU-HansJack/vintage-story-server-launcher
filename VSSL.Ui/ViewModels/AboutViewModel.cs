using System.Text;
using System.Globalization;
using Avalonia.Platform;
using VSSL.Abstractions.Services;
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

    public string RepositoryUrl { get; }

    public string IssuesUrl { get; }

    public string ProjectReadme { get; }

    public AboutViewModel()
    {
        RepositoryUrl = "https://github.com/TGU-HansJack/vintage-story-server-launcher";
        IssuesUrl = $"{RepositoryUrl}/issues";
        ProjectReadme = LoadReadmeText();
    }

    public AboutViewModel(IConfiguration configuration, IBrowserService browserService)
    {
        _browserService = browserService;
        RepositoryUrl = configuration["Links:Repository"]
            ?? "https://github.com/TGU-HansJack/vintage-story-server-launcher";
        IssuesUrl = configuration["Links:BugReport"]
            ?? "https://github.com/TGU-HansJack/vintage-story-server-launcher/issues";
        ProjectReadme = LoadReadmeText();
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
