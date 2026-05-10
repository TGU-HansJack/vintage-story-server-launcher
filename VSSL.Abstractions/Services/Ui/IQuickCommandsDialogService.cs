namespace VSSL.Abstractions.Services.Ui;

/// <summary>
///     快捷指令编辑弹窗服务
/// </summary>
public interface IQuickCommandsDialogService
{
    Task<IReadOnlyList<string>?> ShowEditorAsync(
        string title,
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken = default);
}
