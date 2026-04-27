using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
    }

    public AboutView(AboutViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }
}
