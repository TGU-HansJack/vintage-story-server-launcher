using CommunityToolkit.Mvvm.Messaging.Messages;

namespace VSSL.Ui.Messages;

/// <summary>
/// </summary>
public class SidebarToggleMessage(bool isSidebarOpened) : ValueChangedMessage<bool>(isSidebarOpened);