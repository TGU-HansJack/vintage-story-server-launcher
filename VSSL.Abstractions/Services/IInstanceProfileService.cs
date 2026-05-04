using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
///     实例档案服务
/// </summary>
public interface IInstanceProfileService
{
    /// <summary>
    ///     默认工作区目录（不受用户设置影响）
    /// </summary>
    string GetDefaultWorkspaceRoot();

    /// <summary>
    ///     默认工作区目录（参考 VSSL 默认 C 盘路径）
    /// </summary>
    string GetWorkspaceRoot();

    /// <summary>
    ///     指定档案的默认存档文件路径
    /// </summary>
    string GetDefaultSaveFilePath(string profileId);

    /// <summary>
    ///     已安装服务端版本列表
    /// </summary>
    IReadOnlyList<string> GetInstalledVersions();

    /// <summary>
    ///     档案列表
    /// </summary>
    IReadOnlyList<InstanceProfile> GetProfiles();

    /// <summary>
    ///     根据 Id 获取档案
    /// </summary>
    InstanceProfile? GetProfileById(string profileId);

    /// <summary>
    ///     创建档案
    /// </summary>
    /// <param name="profileName">档案名称</param>
    /// <param name="version">服务端版本</param>
    InstanceProfile CreateProfile(string profileName, string version);

    /// <summary>
    ///     更新档案
    /// </summary>
    /// <param name="profile">待更新档案</param>
    void UpdateProfile(InstanceProfile profile);

    /// <summary>
    ///     批量删除档案
    /// </summary>
    /// <param name="profileIds">档案 Id 集合</param>
    /// <returns>删除数量</returns>
    int DeleteProfiles(IReadOnlyCollection<string> profileIds);
}
