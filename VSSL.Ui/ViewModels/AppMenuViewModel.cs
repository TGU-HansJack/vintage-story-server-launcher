using System.Collections.ObjectModel;
using VSSL.Abstractions.Services.Ui;
using VSSL.Ui.Messages;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     view model of <see cref="VSSL.Ui.Controls.AppMenu" />
/// </summary>
public partial class AppMenuViewModel : RecipientViewModelBase, IRecipient<SubMenusChangedMessage>
{
    private readonly INavigationService _navigationService;

    /// <summary>
    ///     Sub menus filter source
    /// </summary>
    public ObservableCollection<MenuItemViewModel> MenuSources { get; set; } = [];

    /// <summary>
    ///     Sub menus
    /// </summary>
    public ObservableCollection<MenuItemViewModel> Menus { get; set; } = [];

    /// <summary>
    ///     Sub menus changed callback
    /// </summary>
    /// <param name="message">message</param>
    public void Receive(SubMenusChangedMessage message)
    {
        if (message.Value.Count == 0)
        {
            MenuSources.Clear();
            Menus.Clear();
            return;
        }

        MenuSources.Clear();
        var menuToNavigate = message.Value.FirstOrDefault(item => item.IsActive, message.Value[0]);
        foreach (var menuItemViewModel in message.Value)
        {
            menuItemViewModel.IsActive =
                menuItemViewModel.Title.Equals(menuToNavigate.Title, StringComparison.CurrentCulture);
            MenuSources.Add(menuItemViewModel);
        }

        Menus.Clear();
        foreach (var menuSource in MenuSources) Menus.Add(menuSource);

        // navigate to first sub menu
        _navigationService.NavigateTo(menuToNavigate.ViewName);
    }

    #region Commands

    [RelayCommand]
    private void Navigate(MenuItemViewModel? clickMenu)
    {
        if (clickMenu is null || clickMenu.IsActive) return;

        clickMenu.IsActive = true;

        foreach (var menuItemViewModel in MenuSources)
        {
            if (menuItemViewModel == clickMenu) continue;

            menuItemViewModel.IsActive = false;
        }

        _navigationService.NavigateTo(clickMenu.ViewName);
    }

    #endregion

    #region Constructors

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public AppMenuViewModel()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    {
    }

    public AppMenuViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    #endregion
}
