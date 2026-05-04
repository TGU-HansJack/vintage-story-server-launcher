using System.Text.Json.Nodes;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     实例下载服务默认实现
/// </summary>
public class InstanceDownloadService : IInstanceDownloadService
{
    private const string StableUnstableApiUrl = "https://api.vintagestory.at/stable-unstable.json";
    private static readonly TimeSpan CatalogRequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly HttpClient HttpClient = new();

    /// <inheritdoc />
    public string GetDefaultDownloadDirectory()
    {
        return WorkspacePathHelper.PackagesRoot;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServerDownloadEntry>> GetServerDownloadEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(GetDefaultDownloadDirectory());

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(CatalogRequestTimeout);

        await using var stream = await HttpClient.GetStreamAsync(StableUnstableApiUrl, timeoutCts.Token);
        var rootNode = await JsonNode.ParseAsync(stream, cancellationToken: timeoutCts.Token);
        if (rootNode is not JsonObject rootObject) return [];

        var result = new List<ServerDownloadEntry>();
        var downloadDirectory = GetDefaultDownloadDirectory();

        // Keep API JSON iteration order.
        foreach (var versionNode in rootObject)
        {
            if (versionNode.Value is not JsonObject versionObject) continue;

            // Keep platform order from API.
            foreach (var platformNode in versionObject)
            {
                var platformKey = platformNode.Key;
                if (!platformKey.Contains("windows", StringComparison.OrdinalIgnoreCase) ||
                    !platformKey.Contains("server", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (platformNode.Value is not JsonObject artifactObject) continue;

                var fileName = artifactObject["filename"]?.GetValue<string>();
                var fileSize = artifactObject["filesize"]?.GetValue<string>() ?? string.Empty;
                var cdnUrl = artifactObject["urls"]?["cdn"]?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(cdnUrl)) continue;

                result.Add(new ServerDownloadEntry
                {
                    Version = versionNode.Key,
                    Platform = platformKey,
                    FileSize = fileSize,
                    FileName = fileName,
                    CdnUrl = cdnUrl,
                    TargetFilePath = Path.Combine(downloadDirectory, fileName)
                });
            }
        }

        return result;
    }

    /// <inheritdoc />
    public bool IsDownloaded(string targetFilePath)
    {
        return !string.IsNullOrWhiteSpace(targetFilePath) && File.Exists(targetFilePath);
    }

    /// <inheritdoc />
    public async Task DownloadByCdnAsync(
        string cdnUrl,
        string targetFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cdnUrl))
            throw new ArgumentException("Download url cannot be empty.", nameof(cdnUrl));
        if (string.IsNullOrWhiteSpace(targetFilePath))
            throw new ArgumentException("Target file path cannot be empty.", nameof(targetFilePath));

        var fullFilePath = Path.GetFullPath(targetFilePath);
        var parentDirectory = Path.GetDirectoryName(fullFilePath);
        if (!string.IsNullOrWhiteSpace(parentDirectory))
            Directory.CreateDirectory(parentDirectory);

        using var response = await HttpClient.GetAsync(cdnUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(fullFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[1024 * 128];
        long totalRead = 0;
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;

            if (contentLength is > 0)
                progress?.Report((double)totalRead / contentLength.Value);
        }

        progress?.Report(1.0d);
    }
}
