using VSSL.Ui.ViewModels;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace VSSL.Ui.Messages;

/// <summary>
/// </summary>
public class SubMenusChangedMessage(List<MenuItemViewModel> subMenus)
    : ValueChangedMessage<List<MenuItemViewModel>>(subMenus);
