using System.Diagnostics;
using System.Text.Json.Nodes;

namespace VSSL.Services;

internal static class ServerConfigBootstrapper
{
    public static void EnsureGenerated(string installPath, string profileDataPath, bool forceRegenerate = false)
    {
        var configPath = WorkspacePathHelper.GetProfileConfigPath(profileDataPath);
        var shouldGenerate = forceRegenerate || !File.Exists(configPath) || IsLegacyMinimalConfig(configPath);
        if (!shouldGenerate) return;

        var serverExe = Path.Combine(installPath, "VintagestoryServer.exe");
        if (!File.Exists(serverExe))
            throw new InvalidOperationException($"未找到服务端程序：{serverExe}");

        Directory.CreateDirectory(profileDataPath);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = serverExe,
                WorkingDirectory = installPath,
                Arguments = $"--genconfig --dataPath \"{profileDataPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            }
        };

        process.Start();
        if (!process.WaitForExit(30000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }

            throw new InvalidOperationException("生成 serverconfig 超时。");
        }

        if (process.ExitCode != 0)
        {
            var stdError = process.StandardError.ReadToEnd();
            throw new InvalidOperationException(
                $"生成 serverconfig 失败，退出码 {process.ExitCode}。{stdError}");
        }

        if (!File.Exists(configPath))
            throw new InvalidOperationException("服务端未生成 serverconfig.json。");
    }

    private static bool IsLegacyMinimalConfig(string configPath)
    {
        try
        {
            var json = File.ReadAllText(configPath);
            if (string.IsNullOrWhiteSpace(json)) return true;
            if (json.Length < 1500) return true;

            var root = JsonNode.Parse(json) as JsonObject;
            if (root is null) return true;

            // 兼容之前旧版本 Launcher 生成的极简配置：
            // 只有基本字段 + WorldConfig，缺少完整角色/权限结构，启动时会触发 suplayer 组缺失。
            var hasWorldConfig = root.ContainsKey("WorldConfig");
            var hasServerLanguage = root.ContainsKey("ServerLanguage");
            var hasDefaultRoleCode = root.ContainsKey("DefaultRoleCode");
            if (hasWorldConfig && !hasServerLanguage && !hasDefaultRoleCode)
                return true;

            if (!HasUsableRoleConfig(root))
                return true;

            return false;
        }
        catch
        {
            return true;
        }
    }

    private static bool HasUsableRoleConfig(JsonObject root)
    {
        if (root["Roles"] is not JsonArray roles || roles.Count == 0)
            return false;

        static bool ContainsRoleCode(JsonArray roleArray, string expectedCode)
        {
            foreach (var roleNode in roleArray)
            {
                if (roleNode is not JsonObject roleObject) continue;
                var code = roleObject["Code"]?.GetValue<string>();
                if (string.Equals(code, expectedCode, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        var defaultRoleCode = root["DefaultRoleCode"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(defaultRoleCode))
        {
            // 服务端默认会回落到 suplayer，缺失时仍会直接致命退出。
            return ContainsRoleCode(roles, "suplayer");
        }

        return ContainsRoleCode(roles, defaultRoleCode);
    }
}
