using Avalonia.Controls;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class BugReportView : UserControl
{
    public BugReportView()
    {
    }

    public BugReportView(BugReportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
