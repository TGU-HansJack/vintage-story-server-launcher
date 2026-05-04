namespace VSSL.Domains.Models;

/// <summary>
///     启动器偏好设置
/// </summary>
public class LauncherPreferences
{
    /// <summary>
    ///     是否已完成首启引导
    /// </summary>
    public bool IsOnboardingCompleted { get; set; }

    /// <summary>
    ///     默认主题是否为深色
    /// </summary>
    public bool IsDarkMode { get; set; } = true;

    /// <summary>
    ///     默认语言（culture name）
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    ///     工作区根目录（留空使用默认）
    /// </summary>
    public string WorkspaceRoot { get; set; } = string.Empty;
}
