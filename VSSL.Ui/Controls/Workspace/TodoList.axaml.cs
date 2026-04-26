using Avalonia.Controls;
using VSSL.Ui.ViewModels.Workspace;

namespace VSSL.Ui.Controls.Workspace;

public partial class TodoList : UserControl
{
    public TodoList()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRequiredService<TodoListViewModel>();
    }
}
