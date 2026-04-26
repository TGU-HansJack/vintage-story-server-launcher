using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace VSSL.Ui.Views.Dialogs;

/// <summary>
///     高级 JSON 编辑弹窗
/// </summary>
public class AdvancedJsonEditorWindow : Window
{
    private readonly TextBox _editor;

    public AdvancedJsonEditorWindow(string title, string jsonText)
    {
        Title = title;
        Width = 980;
        Height = 680;
        MinWidth = 720;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _editor = new TextBox
        {
            Text = jsonText,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap
        };

        var editorScroller = new ScrollViewer
        {
            Content = _editor,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var cancelButton = new Button
        {
            Content = "取消",
            MinWidth = 90
        };
        cancelButton.Click += (_, _) => Close(null);

        var saveButton = new Button
        {
            Content = "保存",
            MinWidth = 90
        };
        saveButton.Click += (_, _) => Close(_editor.Text ?? string.Empty);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                cancelButton,
                saveButton
            }
        };

        var root = new Grid
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 10
        };
        root.Children.Add(editorScroller);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);

        Content = root;
    }
}
