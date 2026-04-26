using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
///     启动器偏好设置读写服务
/// </summary>
public interface ILauncherPreferencesService
{
    /// <summary>
    ///     加载偏好设置
    /// </summary>
    LauncherPreferences Load();

    /// <summary>
    ///     保存偏好设置
    /// </summary>
    void Save(LauncherPreferences preferences);
}

