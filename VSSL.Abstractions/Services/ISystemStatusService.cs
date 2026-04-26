using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
///     系统状态采样服务
/// </summary>
public interface ISystemStatusService
{
    LauncherMetricSample GetLatestSample();

    IReadOnlyList<LauncherMetricSample> GetRecentSamples(int maxCount = 60);
}
