using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class AutomationView : UserControl
{
    public AutomationView()
    {
    }

    public AutomationView(AutomationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
