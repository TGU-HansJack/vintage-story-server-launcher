using VSSL.Domains.Models;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     模组列表项视图模型
/// </summary>
public class ModItemViewModel : ViewModelBase
{
    public required string ModId { get; init; }

    public required string Version { get; init; }

    public required string FilePath { get; init; }

    public required string Status { get; init; }

    public bool IsDisabled { get; init; }

    public string DependenciesText { get; init; } = "-";

    public string IssuesText { get; init; } = "-";

    public static ModItemViewModel FromModel(ModEntry model)
    {
        return new ModItemViewModel
        {
            ModId = model.ModId,
            Version = model.Version,
            FilePath = model.FilePath,
            Status = model.Status,
            IsDisabled = model.IsDisabled,
            DependenciesText = model.DependenciesText,
            IssuesText = model.IssuesText
        };
    }
}
