namespace VSSL.Abstractions.Services.Ui;

/// <summary>
///     图片预览弹窗服务
/// </summary>
public interface IImagePreviewDialogService
{
    /// <summary>
    ///     打开图片预览弹窗
    /// </summary>
    /// <param name="title">弹窗标题</param>
    /// <param name="imagePath">图片绝对路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ShowAsync(
        string title,
        string imagePath,
        CancellationToken cancellationToken = default);
}
