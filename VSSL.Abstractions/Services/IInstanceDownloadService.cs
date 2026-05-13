using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
///     实例下载服务
/// </summary>
public interface IInstanceDownloadService
{
    /// <summary>
    ///     获取默认下载目录（参考 VSSL 默认 C 盘路径）
    /// </summary>
    /// <returns>默认下载目录</returns>
    string GetDefaultDownloadDirectory();

    /// <summary>
    ///     拉取服务端下载列表（仅 server 条目）
    /// </summary>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>下载条目列表</returns>
    Task<IReadOnlyList<ServerDownloadEntry>> GetServerDownloadEntriesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     判断本地文件是否已下载
    /// </summary>
    /// <param name="targetFilePath">本地目标文件路径</param>
    /// <returns>是否已下载</returns>
    bool IsDownloaded(string targetFilePath);

    /// <summary>
    ///     从 CDN 下载文件到指定路径
    /// </summary>
    /// <param name="cdnUrl">cdn url</param>
    /// <param name="targetFilePath">目标文件路径</param>
    /// <param name="progress">下载进度（0-1）</param>
    /// <param name="cancellationToken">cancellation token</param>
    Task DownloadByCdnAsync(
        string cdnUrl,
        string targetFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     从本地导入服务端压缩包到 packages 目录
    /// </summary>
    /// <param name="sourceFilePath">本地压缩包路径</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>导入后的目标文件路径</returns>
    Task<string> ImportServerPackageAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default);
}
