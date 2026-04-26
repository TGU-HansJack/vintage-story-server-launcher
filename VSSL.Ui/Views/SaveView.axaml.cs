using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class SaveView : UserControl
{
    public SaveView()
    {
    }

    public SaveView(SaveViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

