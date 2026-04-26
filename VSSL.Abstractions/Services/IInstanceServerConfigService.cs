using VSSL.Domains.Models;

namespace VSSL.Abstractions.Services;

/// <summary>
///     实例服务器配置服务
/// </summary>
public interface IInstanceServerConfigService
{
    Task<ServerCommonSettings> LoadServerSettingsAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default);

    Task<WorldSettings> LoadWorldSettingsAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorldRuleValue>> LoadWorldRulesAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(
        InstanceProfile profile,
        ServerCommonSettings serverSettings,
        WorldSettings worldSettings,
        IReadOnlyList<WorldRuleValue> rules,
        CancellationToken cancellationToken = default);

    Task<string> LoadRawJsonAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default);

    Task SaveRawJsonAsync(
        InstanceProfile profile,
        string json,
        CancellationToken cancellationToken = default);
}
