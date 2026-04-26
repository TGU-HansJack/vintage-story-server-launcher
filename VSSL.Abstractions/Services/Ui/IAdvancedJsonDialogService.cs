namespace VSSL.Abstractions.Services.Ui;

/// <summary>
///     高级 JSON 编辑弹窗服务
/// </summary>
public interface IAdvancedJsonDialogService
{
    Task<string?> ShowEditorAsync(
        string title,
        string jsonText,
        CancellationToken cancellationToken = default);
}
