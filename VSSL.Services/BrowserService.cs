using System.Diagnostics;
using VSSL.Abstractions.Services;

namespace VSSL.Services;

/// <summary>
///     Default implementation of <see cref="IBrowserService" />
/// </summary>
public class BrowserService : IBrowserService
{
    /// <inheritdoc />
    public void OpenPage(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
