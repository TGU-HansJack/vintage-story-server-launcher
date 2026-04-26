using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Controls;

public partial class AppMenu : UserControl
{
    public AppMenu()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<AppMenuViewModel>();
    }
}
