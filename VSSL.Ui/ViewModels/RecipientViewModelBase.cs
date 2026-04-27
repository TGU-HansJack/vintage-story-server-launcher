using System.Globalization;
using VSSL.Abstractions.Services.I18n;
using VSSL.Abstractions.ViewModels;
using VSSL.Ui.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace VSSL.Ui.ViewModels;

/// <summary>
///     带消息订阅功能的 ViewModel 基类
/// </summary>
public class RecipientViewModelBase : ObservableRecipient, IDisposable, IViewModel,IRecipient<CurrentCultureChangedMessage>
{
    /// <summary>
    ///     资源是否已释放
    /// </summary>
    private bool _disposed;
    private ILocalizationService? _localizationService;

    protected RecipientViewModelBase()
    {
        // 注册事件
        IsActive = true;
    }

    #region 释放资源

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing) IsActive = false;

        _disposed = true;
    }

    #endregion

    /// <inheritdoc />
    public void Receive(CurrentCultureChangedMessage message)
    {
        // Make all properties change
        OnPropertyChanged(string.Empty);
    }

    public string this[string key] => ResolveLocalizationService()?[key] ?? key;

    protected string L(string key)
    {
        return this[key];
    }

    protected string LF(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, this[key], args);
    }

    private ILocalizationService? ResolveLocalizationService()
    {
        if (_localizationService is not null) return _localizationService;

        try
        {
            _localizationService = ServiceLocator.GetRequiredService<ILocalizationService>();
        }
        catch
        {
            _localizationService = null;
        }

        return _localizationService;
    }
}
