namespace VSSL.Domains.Models;

/// <summary>
///     服务端下载条目
/// </summary>
public class ServerDownloadEntry
{
    /// <summary>
    ///     版本号
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    ///     平台标识（如 windowsserver / linuxserver）
    /// </summary>
    public required string Platform { get; init; }

    /// <summary>
    ///     文件大小文本（源接口原始值）
    /// </summary>
    public required string FileSize { get; init; }

    /// <summary>
    ///     包文件名
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    ///     CDN 下载地址
    /// </summary>
    public required string CdnUrl { get; init; }

    /// <summary>
    ///     本地目标文件路径
    /// </summary>
    public required string TargetFilePath { get; init; }
}
