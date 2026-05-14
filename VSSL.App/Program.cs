using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using VSSL.App.Extensions;
using Serilog;
using Serilog.Events;
using VSSL.Services;

namespace VSSL.App;

internal static class Program
{
    private const string LogOutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{ThreadName}-{ThreadId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        ConfigureBootstrapLogger();
        RegisterUnhandledExceptionLogging();

        try
        {
            Thread.CurrentThread.Name ??= "MainThread";

            if (ServerProcessRelay.IsRelayInvocation(args))
            {
                Environment.ExitCode = ServerProcessRelay.RunAsync(args).GetAwaiter().GetResult();
                return;
            }

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "VSSL terminated unexpectedly.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
    {
        return CustomAppBuilderExtension.BuildAvaloniaAppWithDi();
    }

    private static void ConfigureBootstrapLogger()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VSSL",
            "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithThreadId()
            .Enrich.WithThreadName()
            .WriteTo.File(
                Path.Combine(logDirectory, "VSSL-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: LogOutputTemplate)
            .CreateLogger();

        Log.Information("VSSL bootstrap logger initialized. LogDirectory={LogDirectory}", logDirectory);
    }

    private static void RegisterUnhandledExceptionLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
                Log.Fatal(exception, "Unhandled AppDomain exception. IsTerminating={IsTerminating}", eventArgs.IsTerminating);
            else
                Log.Fatal("Unhandled AppDomain exception object: {ExceptionObject}", eventArgs.ExceptionObject);
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            Log.Error(eventArgs.Exception, "Unobserved task exception.");
            eventArgs.SetObserved();
        };
    }
}
