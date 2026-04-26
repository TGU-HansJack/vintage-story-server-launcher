using VSSL.Abstractions.ViewModels;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace VSSL.Ui.Messages;

/// <summary>
///     当前页面变更消息
/// </summary>
public class CurrentPageChangedMessage(IViewModel? currentPage) : ValueChangedMessage<IViewModel?>(currentPage);