using System;
using System.IO;
using System.Reflection;
using VSSL.Common.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace VSSL.App.Extensions;

/// <summary>
/// </summary>
public static class ServiceCollectionExtension
{
    /// <summary>
    ///     Add <see cref="IConfiguration" /> as a singleton service
    ///     <param name="serviceCollection">
    ///         <see cref="IServiceCollection" />
    ///     </param>
    /// </summary>
    public static void AddAppConfiguration(this IServiceCollection serviceCollection)
    {
        var processDirectory = Path.GetDirectoryName(Environment.ProcessPath)!;
        var builder = new ConfigurationBuilder()
            .SetBasePath(processDirectory);

        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VSSL.App.appsettings.json");
        if (stream is not null) builder.AddJsonStream(stream);

        // External appsettings.json can override embedded defaults.
        builder.AddJsonFile(GlobalConstants.AppSettingsFilename, optional: true, reloadOnChange: false);

        IConfiguration config = builder.Build();
        stream?.Dispose();
        serviceCollection.AddSingleton(config);
    }
}
