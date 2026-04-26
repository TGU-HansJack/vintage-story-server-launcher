using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class WorkspaceView : UserControl
{
    public WorkspaceView()
    {
    }

    public WorkspaceView(WorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
