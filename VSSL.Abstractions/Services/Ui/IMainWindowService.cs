namespace VSSL.Abstractions.Services.Ui;

/// <summary>
///     主窗口管理服务
/// </summary>
public interface IMainWindowService
{
    /// <summary>
    ///     最小化主窗口
    /// </summary>
    void Minimize();

    /// <summary>
    ///     显示主窗口
    /// </summary>
    void Show();

    /// <summary>
    ///     隐藏主窗口
    /// </summary>
    void Hide();

    /// <summary>
    ///     主窗口放大/缩小
    /// </summary>
    void ToggleMaximize();

    /// <summary>
    ///     关闭主窗口
    /// </summary>
    void Close();

    /// <summary>
    ///     直接退出应用
    /// </summary>
    void Shutdown();
}
