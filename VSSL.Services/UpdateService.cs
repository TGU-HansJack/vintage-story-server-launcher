using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     基于 GitHub Releases 的更新检查服务
/// </summary>
public class UpdateService : IUpdateService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    /// <inheritdoc />
    public async Task<AppUpdateInfo> CheckLatestReleaseAsync(
        string repositoryUrl,
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseGithubRepository(repositoryUrl, out var owner, out var repo))
            throw new InvalidOperationException("Invalid GitHub repository URL.");

        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var latest = await JsonSerializer.DeserializeAsync<GithubLatestReleaseResponse>(stream, cancellationToken: cancellationToken);
        if (latest is null || string.IsNullOrWhiteSpace(latest.TagName))
            throw new InvalidOperationException("Failed to parse latest GitHub release.");

        var normalizedCurrent = NormalizeVersion(currentVersion);
        var normalizedLatest = NormalizeVersion(latest.TagName);

        var isUpdateAvailable = CompareVersions(normalizedLatest, normalizedCurrent) > 0;
        var releasePageUrl = !string.IsNullOrWhiteSpace(latest.HtmlUrl)
            ? latest.HtmlUrl
            : BuildLatestReleaseUrl(owner, repo);

        var downloadUrl = latest.Assets?
            .FirstOrDefault(asset => !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))?
            .BrowserDownloadUrl;

        return new AppUpdateInfo
        {
            CurrentVersion = normalizedCurrent,
            LatestTag = latest.TagName,
            LatestVersion = normalizedLatest,
            ReleasePageUrl = releasePageUrl,
            DownloadUrl = downloadUrl,
            IsUpdateAvailable = isUpdateAvailable,
            IsPreRelease = latest.PreRelease
        };
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VSSL-Updater", "1.0"));
        return client;
    }

    private static string BuildLatestReleaseUrl(string owner, string repo)
    {
        return $"https://github.com/{owner}/{repo}/releases/latest";
    }

    private static bool TryParseGithubRepository(string repositoryUrl, out string owner, out string repo)
    {
        owner = string.Empty;
        repo = string.Empty;

        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var uri))
            return false;

        if (!uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var segments = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return false;

        owner = segments[0];
        repo = segments[1];
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            repo = repo[..^4];

        return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo);
    }

    private static string NormalizeVersion(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
            trimmed = trimmed[1..];

        var plusIndex = trimmed.IndexOf('+');
        if (plusIndex >= 0)
            trimmed = trimmed[..plusIndex];

        return trimmed;
    }

    private static int CompareVersions(string latest, string current)
    {
        if (TryParseVersion(latest, out var latestVersion, out var latestPreRelease) &&
            TryParseVersion(current, out var currentVersion, out var currentPreRelease))
        {
            var coreCompare = latestVersion.CompareTo(currentVersion);
            if (coreCompare != 0)
                return coreCompare;

            if (latestPreRelease == currentPreRelease)
                return 0;

            // Same core version: stable release is newer than pre-release.
            return latestPreRelease ? -1 : 1;
        }

        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseVersion(string value, out Version version, out bool isPreRelease)
    {
        version = new Version(0, 0, 0, 0);
        isPreRelease = false;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        var dashIndex = trimmed.IndexOf('-');
        if (dashIndex >= 0)
        {
            isPreRelease = true;
            trimmed = trimmed[..dashIndex];
        }

        if (Version.TryParse(trimmed, out var parsed))
        {
            version = parsed;
            return true;
        }

        var parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        var numbers = new[] { 0, 0, 0, 0 };
        for (var i = 0; i < Math.Min(parts.Length, 4); i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i]))
                return false;
        }

        version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        return true;
    }

    private sealed class GithubLatestReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("prerelease")]
        public bool PreRelease { get; init; }

        [JsonPropertyName("assets")]
        public List<GithubReleaseAssetResponse> Assets { get; init; } = [];
    }

    private sealed class GithubReleaseAssetResponse
    {
        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }
}

