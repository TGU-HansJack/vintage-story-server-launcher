using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class ConfigView : UserControl
{
    public ConfigView()
    {
    }

    public ConfigView(ConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

