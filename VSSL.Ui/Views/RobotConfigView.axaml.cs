using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class RobotConfigView : UserControl
{
    public RobotConfigView()
    {
    }

    public RobotConfigView(RobotConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

