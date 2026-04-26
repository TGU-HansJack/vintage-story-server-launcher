using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class InstanceDownloadView : UserControl
{
    public InstanceDownloadView()
    {
    }

    public InstanceDownloadView(InstanceDownloadViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
