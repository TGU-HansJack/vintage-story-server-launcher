using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class OverviewRestrictionsView : UserControl
{
    public OverviewRestrictionsView()
    {
        InitializeComponent();
    }

    public OverviewRestrictionsView(OverviewRestrictionsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
