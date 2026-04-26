using Avalonia;
using Avalonia.Controls;
using VSSL.Ui.ViewModels.Workspace;
using Avalonia.Markup.Xaml;

namespace VSSL.Ui.Controls.Workspace;

public partial class TeamActivity : UserControl
{
    public TeamActivity()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<TeamActivityViewModel>();
    }
}

