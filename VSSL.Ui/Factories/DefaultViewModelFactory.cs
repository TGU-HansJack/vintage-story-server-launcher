using VSSL.Abstractions.Factories;
using VSSL.Abstractions.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace VSSL.Ui.Factories;

/// <summary>
///     Default implementation of <see cref="IViewModelFactory" />
/// </summary>
public class DefaultViewModelFactory(IServiceProvider serviceProvider) : IViewModelFactory
{
    /// <inheritdoc />
    public IViewModel? Create(Type vmType)
    {
        return serviceProvider.GetRequiredService(vmType) as IViewModel;
    }
}