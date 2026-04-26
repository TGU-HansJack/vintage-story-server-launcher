using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Controls;

public partial class AppSidebar : UserControl
{
    public AppSidebar()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<AppSidebarViewModel>();
    }
}