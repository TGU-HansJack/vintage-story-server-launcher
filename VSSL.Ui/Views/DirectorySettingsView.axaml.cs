using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class DirectorySettingsView : UserControl
{
    public DirectorySettingsView()
    {
    }

    public DirectorySettingsView(DirectorySettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
