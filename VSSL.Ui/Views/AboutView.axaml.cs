using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
    }

    public AboutView(AboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}