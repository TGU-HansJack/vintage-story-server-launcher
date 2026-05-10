namespace VSSL.Abstractions.Services;

/// <summary>
///     启动器开机自启服务
/// </summary>
public interface ILauncherStartupService
{
    /// <summary>
    ///     当前系统是否支持开机自启设置
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    ///     是否已启用开机自启
    /// </summary>
    bool IsEnabled();

    /// <summary>
    ///     设置开机自启
    /// </summary>
    void SetEnabled(bool enabled);
}
