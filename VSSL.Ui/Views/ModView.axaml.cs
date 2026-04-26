using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class ModView : UserControl
{
    public ModView()
    {
    }

    public ModView(ModViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

