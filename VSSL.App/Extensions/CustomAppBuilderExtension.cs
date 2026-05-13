using System;
using System.IO;
using Avalonia;
using VSSL.Services.Extensions;
using VSSL.Ui;
using VSSL.Ui.Extensions;
using VSSL.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Path = System.IO.Path;

namespace VSSL.App.Extensions;

/// <summary>
/// </summary>
public static class CustomAppBuilderExtension
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaAppWithDi()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddAppConfiguration();

                services.AddLogging(builder =>
                {
                    builder.ClearProviders();

                    var logDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "VSSL",
                        "logs");
                    Directory.CreateDirectory(logDirectory);
                    var logPath = Path.Combine(logDirectory, "VSSL-.log");
                    var outputTemplate = context.Configuration["Serilog:WriteTo:1:Args:outputTemplate"]
                                         ?? "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{ThreadName}-{ThreadId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .Enrich.FromLogContext()
                        .Enrich.WithThreadId()
                        .Enrich.WithThreadName()
                        .WriteTo.File(
                            logPath,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 14,
                            shared: true,
                            restrictedToMinimumLevel: LogEventLevel.Debug,
                            outputTemplate: outputTemplate)
                        .CreateLogger();
                    builder.AddSerilog();
                    Log.Information("VSSL application logger configured. LogPath={LogPath}", logPath);
                });

                services.AddServices();

                services.AddViewModels<ViewModelBase>();
                services.AddViewModels<RecipientViewModelBase>();
                services.AddUiServices();
                services.AddViews();
            })
            .Build();

        ServiceLocator.Host = host;

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
