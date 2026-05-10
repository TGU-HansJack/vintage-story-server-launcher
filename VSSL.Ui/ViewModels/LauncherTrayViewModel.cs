using System;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSSL.Abstractions.Services;
using VSSL.Abstractions.Services.Ui;
using VSSL.Domains.Models;
using Microsoft.Extensions.Logging;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     托盘与启动器控制
/// </summary>
public partial class LauncherTrayViewModel : RecipientViewModelBase
{
    private readonly IAutomationSettingsService _automationSettingsService;
    private readonly IInstanceProfileService _instanceProfileService;
    private readonly ILauncherPreferencesService _launcherPreferencesService;
    private readonly IMainWindowService _mainWindowService;
    private readonly IRobotService _robotService;
    private readonly IServerProcessService _serverProcessService;
    private readonly ILogger<LauncherTrayViewModel> _logger;

    public string TrayTooltipText => L("AppName");

    public string ShowWindowText => L("TrayShowWindowText");

    public string StartServerText => L("TrayStartServerText");

    public string StopServerText => L("TrayStopServerText");

    public string StartRobotText => L("TrayStartRobotText");

    public string StopRobotText => L("TrayStopRobotText");

    public string ExitText => L("TrayExitText");

    public LauncherTrayViewModel(
        IAutomationSettingsService automationSettingsService,
        IInstanceProfileService instanceProfileService,
        ILauncherPreferencesService launcherPreferencesService,
        IMainWindowService mainWindowService,
        IRobotService robotService,
        IServerProcessService serverProcessService,
        ILogger<LauncherTrayViewModel> logger)
    {
        _automationSettingsService = automationSettingsService;
        _instanceProfileService = instanceProfileService;
        _launcherPreferencesService = launcherPreferencesService;
        _mainWindowService = mainWindowService;
        _robotService = robotService;
        _serverProcessService = serverProcessService;
        _logger = logger;
    }

    public async Task RunAutoStartActionsAsync(CancellationToken cancellationToken = default)
    {
        var preferences = _launcherPreferencesService.Load();

        if (preferences.StartHiddenOnLaunch)
        {
            _mainWindowService.Hide();
        }

        var tasks = new List<Task>();
        if (preferences.AutoStartServerOnLaunch)
        {
            tasks.Add(StartServerAsync(preferences.AutoStartServerProfileId, cancellationToken));
        }

        if (preferences.AutoStartRobotOnLaunch)
        {
            tasks.Add(StartRobotAsync(cancellationToken));
        }

        if (tasks.Count > 0) await Task.WhenAll(tasks);
    }

    [RelayCommand]
    private void ShowWindow()
    {
        _mainWindowService.Show();
    }

    [RelayCommand]
    private async Task StartServerAsync()
    {
        await StartServerAsync(_launcherPreferencesService.Load().AutoStartServerProfileId, CancellationToken.None);
    }

    [RelayCommand]
    private async Task StopServerAsync()
    {
        await StopServerAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task StartRobotAsync()
    {
        await StartRobotAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task StopRobotAsync()
    {
        await StopRobotAsync(CancellationToken.None);
    }

    [RelayCommand]
    private void Exit()
    {
        _mainWindowService.Shutdown();
    }

    private async Task StartServerAsync(string? preferredProfileId, CancellationToken cancellationToken)
    {
        var status = _serverProcessService.GetCurrentStatus();
        if (status.IsRunning)
        {
            return;
        }

        var profile = await ResolveProfileAsync(preferredProfileId, cancellationToken);
        if (profile is null)
        {
            _logger.LogWarning("Launcher start skipped: no profile found.");
            return;
        }

        try
        {
            await _serverProcessService.StartAsync(profile, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Launcher failed to start server.");
        }
    }

    private async Task StopServerAsync(CancellationToken cancellationToken)
    {
        var status = _serverProcessService.GetCurrentStatus();
        if (!status.IsRunning)
        {
            return;
        }

        try
        {
            await _serverProcessService.StopAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Launcher failed to stop server.");
        }
    }

    private async Task StartRobotAsync(CancellationToken cancellationToken)
    {
        var status = _robotService.GetCurrentStatus();
        if (status.IsRunning)
        {
            return;
        }

        try
        {
            var settings = await _robotService.LoadSettingsAsync(cancellationToken);
            await _robotService.StartAsync(settings, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Launcher failed to start QQ robot.");
        }
    }

    private async Task StopRobotAsync(CancellationToken cancellationToken)
    {
        var status = _robotService.GetCurrentStatus();
        if (!status.IsRunning)
        {
            return;
        }

        try
        {
            await _robotService.StopAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Launcher failed to stop QQ robot.");
        }
    }

    private async Task<InstanceProfile?> ResolveProfileAsync(string? preferredProfileId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(preferredProfileId))
        {
            var profile = _instanceProfileService.GetProfileById(preferredProfileId);
            if (profile is not null)
            {
                return profile;
            }
        }

        var automationSettings = await _automationSettingsService.LoadAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(automationSettings.TargetProfileId))
        {
            var profile = _instanceProfileService.GetProfileById(automationSettings.TargetProfileId);
            if (profile is not null)
            {
                return profile;
            }
        }

        return _instanceProfileService.GetProfiles().FirstOrDefault();
    }
}
