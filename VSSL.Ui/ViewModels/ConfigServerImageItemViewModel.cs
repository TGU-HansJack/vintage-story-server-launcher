using VSSL.Domains.Models;

namespace VSSL.Ui.ViewModels;

public class ConfigServerImageItemViewModel
{
    public required ServerImageKind Kind { get; init; }

    public required string FullPath { get; init; }

    public required string RelativePath { get; init; }

    public required string FileName { get; init; }

    public required long SizeBytes { get; init; }

    public string SizeLabel => $"{Math.Max(0, SizeBytes) / 1024.0:0.0} KB";

    public string KindLabel => Kind == ServerImageKind.Cover ? "cover" : "showcase";
}
