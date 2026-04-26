using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class InstanceCreateView : UserControl
{
    public InstanceCreateView()
    {
    }

    public InstanceCreateView(InstanceCreateViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

