using Avalonia.Controls;
using VSSL.Ui.ViewModels;
using Microsoft.Extensions.Logging;

namespace VSSL.Ui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
    }

    public MainWindow(MainWindowViewModel viewModel, ILogger<MainWindow> logger)
    {
        InitializeComponent();
        DataContext = viewModel;
        logger.LogInformation("MainWindow created");
    }
}
