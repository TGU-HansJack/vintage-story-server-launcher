using System.Reflection;
using Microsoft.Win32;
using VSSL.Abstractions.Services;

namespace VSSL.Services;

/// <summary>
///     Windows 开机自启设置
/// </summary>
public sealed class LauncherStartupService : ILauncherStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "VSSL";

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsEnabled()
    {
        if (!IsSupported)
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(RunValueName)?.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (!IsSupported)
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                var command = BuildLaunchCommand();
                if (string.IsNullOrWhiteSpace(command))
                {
                    return;
                }

                key.SetValue(RunValueName, command, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static string BuildLaunchCommand()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            return $"\"{processPath}\"";
        }

        var entryLocation = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrWhiteSpace(entryLocation))
        {
            return $"\"{entryLocation}\"";
        }

        return string.Empty;
    }
}
