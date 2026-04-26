using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class RobotConsoleView : UserControl
{
    public RobotConsoleView()
    {
    }

    public RobotConsoleView(RobotConsoleViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

