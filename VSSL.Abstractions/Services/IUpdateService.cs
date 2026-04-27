using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
///     应用更新服务
/// </summary>
public interface IUpdateService
{
    /// <summary>
    ///     检查 GitHub 最新发布版本
    /// </summary>
    /// <param name="repositoryUrl">仓库地址（如 https://github.com/owner/repo）</param>
    /// <param name="currentVersion">当前应用版本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新检查结果</returns>
    Task<AppUpdateInfo> CheckLatestReleaseAsync(
        string repositoryUrl,
        string currentVersion,
        CancellationToken cancellationToken = default);
}

