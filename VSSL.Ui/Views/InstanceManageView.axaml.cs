using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class InstanceManageView : UserControl
{
    public InstanceManageView()
    {
    }

    public InstanceManageView(InstanceManageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

