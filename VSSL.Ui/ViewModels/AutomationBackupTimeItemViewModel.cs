using CommunityToolkit.Mvvm.ComponentModel;

namespace VSSL.Ui.ViewModels;

public partial class AutomationBackupTimeItemViewModel : ObservableObject
{
    [ObservableProperty] private string _time = "03:00";
}
