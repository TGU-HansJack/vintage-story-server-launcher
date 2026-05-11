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

    private async void ShowcaseImages_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ConfigViewModel vm)
        {
            return;
        }

        if (vm.IsBusy)
        {
            return;
        }

        if (sender is not ListBox listBox || listBox.SelectedItem is not ConfigServerImageItemViewModel image)
        {
            return;
        }

        await vm.PreviewShowcaseImageCommand.ExecuteAsync(image);
    }
}
