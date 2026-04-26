namespace VSSL.Domains.Models;

/// <summary>
///     实例档案
/// </summary>
public class InstanceProfile
{
    /// <summary>
    ///     档案 Id
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    ///     档案名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     服务端版本
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    ///     档案目录
    /// </summary>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>
    ///     创建时间
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
