using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
///     实例档案服务
/// </summary>
public interface IInstanceProfileService
{
    /// <summary>
    ///     默认工作区目录（参考 VSSL 默认 C 盘路径）
    /// </summary>
    string GetWorkspaceRoot();

    /// <summary>
    ///     已安装服务端版本列表
    /// </summary>
    IReadOnlyList<string> GetInstalledVersions();

    /// <summary>
    ///     档案列表
    /// </summary>
    IReadOnlyList<InstanceProfile> GetProfiles();

    /// <summary>
    ///     创建档案
    /// </summary>
    /// <param name="profileName">档案名称</param>
    /// <param name="version">服务端版本</param>
    InstanceProfile CreateProfile(string profileName, string version);

    /// <summary>
    ///     批量删除档案
    /// </summary>
    /// <param name="profileIds">档案 Id 集合</param>
    /// <returns>删除数量</returns>
    int DeleteProfiles(IReadOnlyCollection<string> profileIds);
}
