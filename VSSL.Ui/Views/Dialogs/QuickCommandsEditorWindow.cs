using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using System.Globalization;

namespace VSSL.Ui.Views.Dialogs;

/// <summary>
///     快捷指令编辑弹窗
/// </summary>
public class QuickCommandsEditorWindow : Window
{
    private readonly TextBox _newCommandInput;
    private readonly ListBox _commandsListBox;
    private readonly List<string> _commands;

    public QuickCommandsEditorWindow(string title, IReadOnlyList<string> commands)
    {
        var isZh = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

        Title = title;
        Width = 720;
        Height = 520;
        MinWidth = 600;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _commands = (commands ?? [])
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Select(command => command.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _newCommandInput = new TextBox
        {
            Watermark = isZh ? "输入快捷指令，例如：/players" : "Enter quick command, e.g. /players"
        };
        _newCommandInput.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                AddCommand();
                e.Handled = true;
            }
        };

        var addButton = new Button
        {
            Content = isZh ? "添加" : "Add",
            MinWidth = 90
        };
        addButton.Click += (_, _) => AddCommand();

        _commandsListBox = new ListBox
        {
            ItemsSource = _commands
        };

        var removeButton = new Button
        {
            Content = isZh ? "删除选中" : "Remove Selected",
            MinWidth = 110
        };
        removeButton.Click += (_, _) => RemoveSelected();

        var cancelButton = new Button
        {
            Content = isZh ? "取消" : "Cancel",
            MinWidth = 90
        };
        cancelButton.Click += (_, _) => Close(null);

        var saveButton = new Button
        {
            Content = isZh ? "保存" : "Save",
            MinWidth = 90
        };
        saveButton.Click += (_, _) => Close(_commands.ToList());

        var inputRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                _newCommandInput,
                addButton
            }
        };
        Grid.SetColumn(addButton, 1);

        var header = new TextBlock
        {
            Text = isZh ? "快捷指令列表" : "Quick Commands",
            FontSize = 16,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        };

        var listRow = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 8,
            Children =
            {
                _commandsListBox,
                removeButton
            }
        };
        Grid.SetRow(removeButton, 1);
        removeButton.HorizontalAlignment = HorizontalAlignment.Right;

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
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            RowSpacing = 10
        };
        root.Children.Add(header);
        Grid.SetRow(inputRow, 1);
        root.Children.Add(inputRow);
        Grid.SetRow(listRow, 2);
        root.Children.Add(listRow);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;
    }

    private void AddCommand()
    {
        var raw = _newCommandInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        if (!raw.StartsWith('/'))
        {
            raw = "/" + raw;
        }

        if (_commands.Any(existing => existing.Equals(raw, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _commands.Add(raw);
        RefreshCommands();
        _newCommandInput.Text = string.Empty;
    }

    private void RemoveSelected()
    {
        var selected = _commandsListBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        var index = _commands.FindIndex(command => command.Equals(selected, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        _commands.RemoveAt(index);
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        _commandsListBox.ItemsSource = null;
        _commandsListBox.ItemsSource = _commands.ToList();
    }
}
