using CommunityToolkit.Mvvm.ComponentModel;

namespace VSSL.Ui.ViewModels;

public partial class AutomationExportTimeItemViewModel : ObservableObject
{
    [ObservableProperty] private string _time = "12:00";
}
