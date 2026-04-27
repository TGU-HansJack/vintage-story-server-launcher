namespace VSSL.Abstractions.Services.Ui;

/// <summary>
///     文件选择服务
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    ///     打开单文件选择对话框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="filterName">筛选器名称</param>
    /// <param name="patterns">文件模式（如 *.zip）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>选中的本地文件路径；取消时返回 null</returns>
    Task<string?> PickSingleFileAsync(
        string title,
        string filterName,
        IReadOnlyList<string> patterns,
        CancellationToken cancellationToken = default);
}

