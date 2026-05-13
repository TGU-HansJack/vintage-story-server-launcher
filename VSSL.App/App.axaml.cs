using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using VSSL.Abstractions.Services;
using VSSL.Abstractions.Services.I18n;
using VSSL.Abstractions.Services.Ui;
using VSSL.Domains.Models;
using VSSL.Ui;
using VSSL.Ui.ViewModels;
using VSSL.Ui.Views.Dialogs;
using VSSL.Ui.Views;
using Avalonia.Data.Core.Plugins;
using Avalonia.Diagnostics;
using Avalonia.Markup.Xaml;
using Serilog;

namespace VSSL.App;

public class App : Application
{
    private bool _startupActionsExecuted;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += (_, eventArgs) =>
        {
            Log.Error(eventArgs.Exception, "Unhandled UI dispatcher exception.");
        };

        var launcherPreferencesService = ServiceLocator.GetRequiredService<ILauncherPreferencesService>();
        var localizationService = ServiceLocator.GetRequiredService<ILocalizationService>();
        var themeService = ServiceLocator.GetRequiredService<IThemeService>();
        var launcherTrayViewModel = ServiceLocator.GetRequiredService<LauncherTrayViewModel>();

        DataContext = launcherTrayViewModel;

        var preferences = launcherPreferencesService.Load();
        ApplyPreferences(preferences, localizationService, themeService);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var mainWindow = ServiceLocator.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
            mainWindow.Opened += async (_, _) =>
            {
                if (_startupActionsExecuted)
                {
                    return;
                }

                try
                {
                    _startupActionsExecuted = true;

                    if (!launcherPreferencesService.Load().IsOnboardingCompleted)
                    {
                        await ShowFirstRunSetupAsync(mainWindow, launcherPreferencesService, localizationService, themeService);
                    }

                    await launcherTrayViewModel.RunAutoStartActionsAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Launcher startup sequence failed.");
                }
            };

#if DEBUG
            mainWindow.AttachDevTools(new DevToolsOptions
            {
                StartupScreenIndex = 1
            });
#endif
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove) BindingPlugins.DataValidators.Remove(plugin);
    }

    private static void ApplyPreferences(
        LauncherPreferences preferences,
        ILocalizationService localizationService,
        IThemeService themeService)
    {
        localizationService.CurrentCulture = NormalizeCulture(preferences.Language);
        themeService.ToggleTheme(preferences.IsDarkMode);
    }

    private static CultureInfo NormalizeCulture(string? language)
    {
        var normalized = string.IsNullOrWhiteSpace(language)
            ? CultureInfo.CurrentCulture.Name
            : language.Trim();

        try
        {
            return CultureInfo.GetCultureInfo(normalized);
        }
        catch
        {
            return CultureInfo.GetCultureInfo("en-US");
        }
    }

    private static async Task ShowFirstRunSetupAsync(
        MainWindow owner,
        ILauncherPreferencesService launcherPreferencesService,
        ILocalizationService localizationService,
        IThemeService themeService)
    {
        // Wait one UI tick so owner window is fully activated before showing modal.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        var current = launcherPreferencesService.Load();
        var dialog = new FirstRunSetupWindow(
            current.IsDarkMode,
            NormalizeCulture(current.Language).Name);
        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (!confirmed) return;

        var updated = new LauncherPreferences
        {
            WorkspaceRoot = current.WorkspaceRoot,
            IsOnboardingCompleted = true,
            IsDarkMode = dialog.IsDarkMode,
            Language = dialog.Language,
            StartWithWindows = current.StartWithWindows,
            StartHiddenOnLaunch = current.StartHiddenOnLaunch,
            CloseToTrayOnExit = current.CloseToTrayOnExit,
            AutoStartServerOnLaunch = current.AutoStartServerOnLaunch,
            AutoStartRobotOnLaunch = current.AutoStartRobotOnLaunch,
            AutoStartServerProfileId = current.AutoStartServerProfileId
        };
        launcherPreferencesService.Save(updated);
        ApplyPreferences(updated, localizationService, themeService);
    }
}
