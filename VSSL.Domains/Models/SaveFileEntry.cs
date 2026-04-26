namespace VSSL.Domains.Models;

/// <summary>
///     存档文件项
/// </summary>
public class SaveFileEntry
{
    public required string FullPath { get; init; }

    public required string FileName { get; init; }

    public DateTimeOffset LastWriteTimeUtc { get; init; }
}
