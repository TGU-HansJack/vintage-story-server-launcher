using System.Collections.ObjectModel;
using System.Globalization;
using VSSL.Abstractions.Services;
using VSSL.Abstractions.Services.I18n;
using VSSL.Abstractions.Services.Ui;
using VSSL.Domains.Enums;
using VSSL.Domains.Models;
using VSSL.Ui.Assets.I18n;
using VSSL.Ui.Controls;
using VSSL.Ui.Messages;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     view model of <see cref="AppSidebar" />
/// </summary>
public partial class AppSidebarViewModel : RecipientViewModelBase, IRecipient<ThemeChangedMessage>
{
    private readonly ILauncherPreferencesService _launcherPreferencesService;
    private readonly ILocalizationService _localizationService;
    private readonly IMainWindowService? _mainWindowService;
    private readonly IMenuService? _menuService;
    private readonly IMessenger _messenger;
    private readonly INavigationService? _navigationService;
    private readonly ISidebarService? _sidebarService;
    private readonly IThemeService? _themeService;

    [ObservableProperty] private string _currentCultureName;

    [ObservableProperty] private bool _isDarkMode = true;

    /// <summary>
    ///     固定展示在设置按钮上方的关于按钮
    /// </summary>
    [ObservableProperty] private MenuItemViewModel? _aboutMenu;

    /// <summary>
    ///     菜单列表
    /// </summary>
    public ObservableCollection<MenuItemViewModel> Menus { get; set; } = [];

    /// <inheritdoc />
    public void Receive(ThemeChangedMessage message)
    {
        IsDarkMode = message.Value;
    }

    /// <summary>
    ///     Init menu data
    /// </summary>
    private void InitMenus()
    {
        var menuItems = _menuService?.GetMenuItems().Select(item => new MenuItemViewModel(item, _localizationService))
            .ToList() ?? [];
        AboutMenu = menuItems.FirstOrDefault(menu =>
            menu.ViewName == ViewName.About || menu.Children.Any(child => child.ViewName == ViewName.About));
        if (AboutMenu is not null) menuItems.Remove(AboutMenu);
        Menus = new ObservableCollection<MenuItemViewModel>(menuItems);

        if (Menus.Count == 0)
        {
            if (AboutMenu is null) return;

            Dispatcher.UIThread.Post(() => Navigate(AboutMenu));
            return;
        }

        // navigate to first menu
        Dispatcher.UIThread.Post(() =>
        {
            var firstMenu = Menus[0];
            firstMenu.IsActive = true;

            _messenger.Send(new SubMenusChangedMessage(firstMenu.Children));

            if (firstMenu.Children.Count == 0) _navigationService?.NavigateTo(firstMenu.ViewName);
        });
    }

    #region Properties

    public string ExitButtonText => _localizationService[nameof(Resources.ExitButtonText)];

    public string LanguageButtonText => _localizationService[nameof(Resources.LanguageButtonText)];

    public string ThemesButtonText => _localizationService[nameof(Resources.ThemesButtonText)];

    public string LightThemeName => _localizationService[nameof(Resources.LightThemeName)];

    public string DarkThemeName => _localizationService[nameof(Resources.DarkThemeName)];

    public string ZhLanguageOptionName => _localizationService["ZhLanguageOptionName"];

    public string EnLanguageOptionName => _localizationService["EnLanguageOptionName"];

    #endregion

    #region Commands

    [RelayCommand]
    private void ToggleSidebar()
    {
        _sidebarService?.ToggleSidebar();
    }

    [RelayCommand]
    private void Navigate(MenuItemViewModel? clickMenu)
    {
        if (clickMenu is null || clickMenu.IsActive) return;

        clickMenu.IsActive = true;

        foreach (var menuItemViewModel in Menus)
            menuItemViewModel.IsActive = menuItemViewModel == clickMenu;

        if (AboutMenu is not null && AboutMenu != clickMenu) AboutMenu.IsActive = false;

        _messenger.Send(new SubMenusChangedMessage(clickMenu.Children));

        if (clickMenu.Children.Count == 0) _navigationService?.NavigateTo(clickMenu.ViewName);
    }

    [RelayCommand]
    private void NavigateAbout()
    {
        if (AboutMenu is null)
        {
            _navigationService?.NavigateTo(ViewName.About);
            return;
        }

        Navigate(AboutMenu);

        var aboutPage = AboutMenu.Children.FirstOrDefault(child => child.ViewName == ViewName.About);
        if (aboutPage is not null)
        {
            foreach (var child in AboutMenu.Children)
                child.IsActive = child == aboutPage;

            _navigationService?.NavigateTo(aboutPage.ViewName);
            return;
        }

        _navigationService?.NavigateTo(ViewName.About);
    }

    [RelayCommand]
    private void ToggleTheme(string value)
    {
        var isDarkMode = bool.Parse(value);
        _themeService?.ToggleTheme(isDarkMode);
        SavePreferences(preferences => preferences.IsDarkMode = isDarkMode);
    }

    [RelayCommand]
    private void ChangeLanguage(string language)
    {
        _localizationService.CurrentCulture = CultureInfo.GetCultureInfo(language);
        CurrentCultureName = _localizationService.CurrentCulture.Name;
        SavePreferences(preferences => preferences.Language = CurrentCultureName);
    }

    [RelayCommand]
    private void Exit()
    {
        _mainWindowService?.Close();
    }

    #endregion

    #region Constructors

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public AppSidebarViewModel()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    {
    }

    public AppSidebarViewModel(ISidebarService sidebarService, IThemeService? themeService,
        INavigationService? navigationService,
        IMainWindowService mainWindowService, IMenuService? menuService, ILocalizationService localizationService,
        IMessenger messenger, ILauncherPreferencesService launcherPreferencesService)
    {
        _launcherPreferencesService = launcherPreferencesService;
        _sidebarService = sidebarService;
        _themeService = themeService;
        _navigationService = navigationService;
        _mainWindowService = mainWindowService;
        _menuService = menuService;
        _localizationService = localizationService;
        _messenger = messenger;

        CurrentCultureName = _localizationService.CurrentCulture.Name;
        IsDarkMode = _themeService?.IsDarkMode ?? IsDarkMode;

        InitMenus();
    }

    #endregion

    private void SavePreferences(Action<LauncherPreferences> updateAction)
    {
        var preferences = _launcherPreferencesService.Load();
        updateAction(preferences);
        preferences.IsOnboardingCompleted = true;
        _launcherPreferencesService.Save(preferences);
    }
}
