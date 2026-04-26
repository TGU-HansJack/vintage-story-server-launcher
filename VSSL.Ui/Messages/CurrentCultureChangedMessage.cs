using System.Globalization;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace VSSL.Ui.Messages;

/// <summary>
/// 
/// </summary>
public class CurrentCultureChangedMessage(CultureInfo cultureInfo) : ValueChangedMessage<CultureInfo>(cultureInfo);