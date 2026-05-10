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

    /// <summary>
    ///     是否开机自启启动器
    /// </summary>
    public bool StartWithWindows { get; set; }

    /// <summary>
    ///     是否启动时隐藏到托盘
    /// </summary>
    public bool StartHiddenOnLaunch { get; set; }

    /// <summary>
    ///     是否关闭窗口时隐藏到托盘
    /// </summary>
    public bool CloseToTrayOnExit { get; set; }

    /// <summary>
    ///     是否启动时自动启动服务器
    /// </summary>
    public bool AutoStartServerOnLaunch { get; set; }

    /// <summary>
    ///     自动启动服务器时使用的档案 Id
    /// </summary>
    public string AutoStartServerProfileId { get; set; } = string.Empty;

    /// <summary>
    ///     是否启动时自动启动 QQ 机器人
    /// </summary>
    public bool AutoStartRobotOnLaunch { get; set; }

    /// <summary>
    ///     控制台快捷指令
    /// </summary>
    public List<string> QuickCommands { get; set; } = [];
}
