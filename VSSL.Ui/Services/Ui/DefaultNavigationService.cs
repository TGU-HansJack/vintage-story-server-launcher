using System.Collections.Immutable;
using VSSL.Abstractions.Factories;
using VSSL.Abstractions.Services.Ui;
using VSSL.Abstractions.ViewModels;
using VSSL.Domains.Enums;
using VSSL.Ui.Messages;
using VSSL.Ui.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace VSSL.Ui.Services.Ui;

/// <summary>
///     导航服务的默认实现
/// </summary>
public class DefaultNavigationService(
    IViewModelFactory viewModelFactory,
    IMessenger messenger,
    ILogger<DefaultNavigationService> logger) : INavigationService
{
    private static readonly ImmutableDictionary<ViewName, Type> ViewMappings = ImmutableDictionary
        .Create<ViewName, Type>()
        .Add(ViewName.Home, typeof(HomeViewModel))
        .Add(ViewName.Workspace, typeof(WorkspaceViewModel))
        .Add(ViewName.MapPreview, typeof(MapPreviewViewModel))
        .Add(ViewName.Automation, typeof(AutomationViewModel))
        .Add(ViewName.About, typeof(AboutViewModel))
        .Add(ViewName.BugReport, typeof(BugReportViewModel))
        .Add(ViewName.InstanceDownload, typeof(InstanceDownloadViewModel))
        .Add(ViewName.InstanceCreate, typeof(InstanceCreateViewModel))
        .Add(ViewName.InstanceManage, typeof(InstanceManageViewModel))
        .Add(ViewName.DirectorySettings, typeof(DirectorySettingsViewModel))
        .Add(ViewName.Config, typeof(ConfigViewModel))
        .Add(ViewName.Save, typeof(SaveViewModel))
        .Add(ViewName.Mod, typeof(ModViewModel))
        .Add(ViewName.RobotConfig, typeof(RobotConfigViewModel))
        .Add(ViewName.RobotConsole, typeof(RobotConsoleViewModel));

    /// <summary>
    ///     当前页面对应的 view model
    /// </summary>
    private IViewModel? CurrentPage { get; set; }

    /// <inheritdoc />
    public void NavigateTo(ViewName? viewName)
    {
        logger.LogInformation("Navigate to {ViewName}", viewName);
        if (viewName is null) return;

        var vmType = ViewMappings[viewName.Value];
        if (!typeof(IViewModel).IsAssignableFrom(vmType))
        {
            logger.LogError("Failed to navigate to {ViewName}, {IViewModel} is not assignable from {VmType}", viewName,
                nameof(IViewModel), vmType);
            return;
        }

        CurrentPage = viewModelFactory.Create(vmType);
        messenger.Send(new CurrentPageChangedMessage(CurrentPage));
    }
}
