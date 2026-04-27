namespace VSSL.Domains.Models;

/// <summary>
///     应用更新检查结果
/// </summary>
public class AppUpdateInfo
{
    /// <summary>
    ///     当前应用版本（标准化）
    /// </summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>
    ///     GitHub 最新发布标签（原始 tag）
    /// </summary>
    public string LatestTag { get; init; } = string.Empty;

    /// <summary>
    ///     GitHub 最新发布版本（标准化）
    /// </summary>
    public string LatestVersion { get; init; } = string.Empty;

    /// <summary>
    ///     最新发布页面地址
    /// </summary>
    public string ReleasePageUrl { get; init; } = string.Empty;

    /// <summary>
    ///     推荐下载地址（如果可用）
    /// </summary>
    public string? DownloadUrl { get; init; }

    /// <summary>
    ///     是否检测到新版本
    /// </summary>
    public bool IsUpdateAvailable { get; init; }

    /// <summary>
    ///     最新发布是否为预发布版本
    /// </summary>
    public bool IsPreRelease { get; init; }
}

